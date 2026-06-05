using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Application.Services
{
    public class QuoteListReadModelService : IQuoteListReadModelService
    {
        public const string StateKey = "QuoteList";

        private readonly IAppDbContext _dbContext;

        public QuoteListReadModelService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task RefreshAllAsync()
        {
            var state = await _dbContext.ReadModelStates.FirstOrDefaultAsync(s => s.Key == StateKey);
            if (state == null)
            {
                state = new ReadModelState { Key = StateKey };
                _dbContext.ReadModelStates.Add(state);
            }

            state.IsRefreshing = true;
            state.LastError = null;
            await _dbContext.SaveChangesAsync();

            try
            {
                var now = DateTime.UtcNow;

                var quotes = await _dbContext.Quotes
                    .AsNoTracking()
                    .Include(q => q.Owner)
                    .Include(q => q.Client)
                    .Include(q => q.FollowUps.OrderByDescending(f => f.CreatedAt).Take(1))
                    .ToListAsync();

                var existing = await _dbContext.QuoteListItems.ToListAsync();
                var existingById = existing.ToDictionary(x => x.QuoteId);
                var incomingIds = new HashSet<Guid>();

                foreach (var quote in quotes)
                {
                    incomingIds.Add(quote.Id);

                    if (!existingById.TryGetValue(quote.Id, out var item))
                    {
                        item = new QuoteListItem { QuoteId = quote.Id };
                        _dbContext.QuoteListItems.Add(item);
                    }

                    ApplyQuote(item, quote, now);
                }

                var removed = existing.Where(x => !incomingIds.Contains(x.QuoteId)).ToList();
                if (removed.Count > 0)
                    _dbContext.QuoteListItems.RemoveRange(removed);

                state.IsRefreshing = false;
                state.IsStale = false;
                state.LastRefreshedAt = now;
                state.LastError = null;
            }
            catch (Exception ex)
            {
                state.IsRefreshing = false;
                state.IsStale = true;
                state.LastError = ex.Message;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task MarkStaleAsync()
        {
            var state = await _dbContext.ReadModelStates.FirstOrDefaultAsync(s => s.Key == StateKey);
            if (state == null)
            {
                state = new ReadModelState { Key = StateKey };
                _dbContext.ReadModelStates.Add(state);
            }

            state.IsStale = true;
            await _dbContext.SaveChangesAsync();
        }

        private static void ApplyQuote(QuoteListItem item, Quote quote, DateTime refreshedAt)
        {
            var lastNote = quote.FollowUps?
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefault();

            var clientName = quote.Client?.CompanyName ?? quote.ClientName ?? quote.SenderEmail ?? "Unknown";
            var ownerName = quote.Owner?.FullName ?? "";
            var refText = quote.QuoteReference ?? "";
            var subject = quote.Subject ?? "";
            var senderEmail = quote.SenderEmail ?? "";
            var notePreview = (lastNote?.Notes ?? "").Trim();
            if (notePreview.Length > 120)
                notePreview = notePreview.Substring(0, 120) + "...";

            item.RecordType = quote.RecordType;
            item.Status = quote.Status;
            item.IsDeleteRequested = quote.IsDeleteRequested;
            item.OwnerId = string.IsNullOrWhiteSpace(quote.OwnerId) ? null : quote.OwnerId;
            item.OwnerName = ownerName;
            item.ClientId = quote.ClientId;
            item.ClientName = clientName;
            item.SenderEmail = senderEmail;
            item.SenderName = quote.SenderName ?? "";
            item.Subject = subject;
            item.QuoteReference = refText;
            item.QuoteValue = quote.QuoteValue;
            item.WinProbability = quote.WinProbability;
            item.Currency = quote.Currency ?? "";
            item.NextFollowUpDate = quote.NextFollowUpDate;
            item.CreatedAt = quote.CreatedAt;
            item.UpdatedAt = quote.UpdatedAt;
            item.EmailReceivedDateTime = quote.EmailReceivedDateTime;
            item.RepCompletedAt = quote.RepCompletedAt;
            item.LastNoteAt = lastNote?.CreatedAt;
            item.LastNotePreview = notePreview;
            item.IsClosed = IsClosedStatus(quote.Status);
            item.IsUnassigned = string.IsNullOrWhiteSpace(quote.OwnerId);
            item.MissingFollowUp = !quote.NextFollowUpDate.HasValue;
            item.ValueTbd = !quote.QuoteValue.HasValue || quote.QuoteValue.Value <= 0m;
            item.MissingClientLink = !quote.ClientId.HasValue;
            item.DeleteRequestReason = quote.DeleteRequestReason ?? "";
            item.DeleteRequestedByUserId = quote.DeleteRequestedByUserId ?? "";
            item.LeadSource = quote.LeadSource ?? "";
            item.SearchText = $"{clientName} {senderEmail} {refText} {subject} {ownerName} {quote.LeadSource}".ToLowerInvariant();
            item.RefreshedAt = refreshedAt;
        }

        private static bool IsClosedStatus(QuoteStatus status)
        {
            return status == QuoteStatus.Won ||
                   status == QuoteStatus.Lost ||
                   status == QuoteStatus.Cancelled ||
                   status == QuoteStatus.LeadClosed ||
                   status == QuoteStatus.Merged;
        }
    }
}

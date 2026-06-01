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
    public class QuoteService : IQuoteService
    {
        private readonly IAppDbContext _dbContext;

        public QuoteService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Quote>> GetAllQuotesAsync(string? userId, bool isAdmin)
        {
            var query = _dbContext.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Include(q => q.Client)
                .Include(q => q.FollowUps)
                .Include(q => q.Attachments)
                .AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(userId))
                    query = query.Where(q => false);
                else
                    query = query.Where(q => q.OwnerId == userId);
            }

            return await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
        }

        public async Task<int> GetTotalQuotesCountAsync(string? userId, bool isAdmin)
        {
            var query = _dbContext.Quotes.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(userId)) return 0;
                query = query.Where(q => q.OwnerId == userId);
            }

            return await query.CountAsync();
        }

        public async Task<decimal> GetTotalQuoteValueAsync(string? userId, bool isAdmin)
        {
            var query = _dbContext.Quotes.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(userId)) return 0;
                query = query.Where(q => q.OwnerId == userId);
            }

            return await query.SumAsync(q => q.QuoteValue ?? 0);
        }

        public async Task<int> GetFollowUpsDueCountAsync(string? userId, bool isAdmin)
        {
            var query = _dbContext.Quotes.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(userId)) return 0;
                query = query.Where(q => q.OwnerId == userId);
            }

            return await query.CountAsync(q => q.NextFollowUpDate != null && q.NextFollowUpDate <= DateTime.UtcNow);
        }

        public async Task<List<ApplicationUser>> GetSystemUsersAsync()
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.Email)
                .ToListAsync();
        }

        public async Task<Quote?> GetQuoteByIdAsync(Guid id)
        {
            return await _dbContext.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Include(q => q.Client)
                .Include(q => q.FollowUps)
                .Include(q => q.Attachments)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task AddQuoteAsync(Quote quote)
        {
            _dbContext.Quotes.Add(quote);

            var logUserId = !string.IsNullOrWhiteSpace(quote.OwnerId) ? quote.OwnerId : quote.ClosedByUserId;

            if (!string.IsNullOrWhiteSpace(logUserId))
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    UserId = logUserId!,
                    Action = "Quote Created",
                    Details = $"Created quote/lead. Ref='{quote.QuoteReference ?? "-"}', Client='{quote.ClientName ?? "-"}', Id={quote.Id}",
                    Timestamp = DateTime.UtcNow,
                    RelatedQuoteId = quote.Id
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateQuoteAsync(Quote quote, string? actorUserId = null)
        {
            var existing = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == quote.Id);
            if (existing == null)
                throw new Exception("Quote not found.");

            existing.SenderEmail = quote.SenderEmail;
            existing.SenderName = quote.SenderName;
            existing.Subject = quote.Subject;
            existing.EmailReceivedDateTime = quote.EmailReceivedDateTime;
            existing.EmailSentDateTime = quote.EmailSentDateTime;
            existing.EmailMessageId = quote.EmailMessageId;

            existing.RecordType = quote.RecordType;
            existing.QuoteValue = quote.QuoteValue;
            existing.Currency = quote.Currency;
            existing.QuoteReference = quote.QuoteReference;
            existing.ClientName = quote.ClientName;
            existing.SolutionSummary = quote.SolutionSummary;
            existing.QuoteDate = quote.QuoteDate;

            existing.DiscoveryNotes = quote.DiscoveryNotes;
            existing.DiscoveryNotesUpdatedAt = quote.DiscoveryNotesUpdatedAt;
            existing.DiscoveryNotesUpdatedByUserId = quote.DiscoveryNotesUpdatedByUserId;

            existing.Status = quote.Status;
            existing.NextFollowUpDate = quote.NextFollowUpDate;
            existing.WinProbability = quote.WinProbability;

            existing.IsDeleteRequested = quote.IsDeleteRequested;
            existing.DeleteRequestReason = quote.DeleteRequestReason;
            existing.DeleteRequestedByUserId = quote.DeleteRequestedByUserId;

            existing.LeadSource = quote.LeadSource;

            existing.RepAttachedQuoteReference = quote.RepAttachedQuoteReference;
            existing.RepCompletionNotes = quote.RepCompletionNotes;
            existing.RepCompletedAt = quote.RepCompletedAt;

            existing.ClosedByUserId = quote.ClosedByUserId;
            existing.ClosedAt = quote.ClosedAt;
            existing.ClosureNotes = quote.ClosureNotes;

            existing.IsMerged = quote.IsMerged;
            existing.MergedIntoQuoteId = quote.MergedIntoQuoteId;
            existing.MergeNotes = quote.MergeNotes;

            existing.ClientId = quote.ClientId;
            existing.OwnerId = string.IsNullOrWhiteSpace(quote.OwnerId) ? null : quote.OwnerId;
            existing.UpdatedAt = DateTime.UtcNow;

            var effectiveActorUserId = !string.IsNullOrWhiteSpace(actorUserId)
                ? actorUserId
                : (!string.IsNullOrWhiteSpace(existing.OwnerId) ? existing.OwnerId : existing.ClosedByUserId);

            if (!string.IsNullOrWhiteSpace(effectiveActorUserId))
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    UserId = effectiveActorUserId!,
                    Action = "Quote Updated",
                    Details = $"Updated quote. Ref='{existing.QuoteReference ?? "-"}', Status='{existing.Status}', NextDue='{existing.NextFollowUpDate?.ToString("u") ?? "-"}', Id={existing.Id}",
                    Timestamp = DateTime.UtcNow,
                    RelatedQuoteId = existing.Id
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task AddFollowUpAsync(FollowUp followUp)
        {
            followUp.CreatedAt = DateTime.UtcNow;
            _dbContext.FollowUps.Add(followUp);

            var q = await _dbContext.Quotes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == followUp.QuoteId);

            var logUserId =
                !string.IsNullOrWhiteSpace(followUp.CreatedByUserId) ? followUp.CreatedByUserId :
                !string.IsNullOrWhiteSpace(q?.OwnerId) ? q!.OwnerId :
                null;

            if (!string.IsNullOrWhiteSpace(logUserId))
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    UserId = logUserId!,
                    Action = "FollowUp Added",
                    Details = $"Follow-up added. Ref='{q?.QuoteReference ?? "-"}', Due='{followUp.DueDate:u}', Note='{(followUp.Notes ?? "").Trim()}', QuoteId={followUp.QuoteId}",
                    Timestamp = DateTime.UtcNow,
                    RelatedQuoteId = followUp.QuoteId
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task SnoozeQuoteAsync(Guid id, int days)
        {
            var quote = await _dbContext.Quotes.FindAsync(id);
            if (quote == null) return;

            var baseDate = (quote.NextFollowUpDate.HasValue && quote.NextFollowUpDate.Value > DateTime.UtcNow)
                ? quote.NextFollowUpDate.Value
                : DateTime.UtcNow;

            var newDue = baseDate.AddDays(days);
            quote.NextFollowUpDate = newDue;
            quote.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(quote.OwnerId))
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    UserId = quote.OwnerId,
                    Action = "Task Snoozed",
                    Details = $"Snoozed quote. Ref='{quote.QuoteReference ?? "-"}', Days={days}, NewDue='{newDue:u}', Id={quote.Id}",
                    Timestamp = DateTime.UtcNow,
                    RelatedQuoteId = quote.Id
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteQuoteAsync(Guid id)
        {
            var quote = await _dbContext.Quotes.FindAsync(id);
            if (quote == null) return;

            _dbContext.Quotes.Remove(quote);
            await _dbContext.SaveChangesAsync();
        }

        public async Task SubmitLeadAsRepCompleteAsync(Guid leadId, string repUserId, string outgoingQuoteRef, string? notes)
        {
            var lead = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == leadId);
            if (lead == null) throw new Exception("Lead not found.");

            lead.RepAttachedQuoteReference = outgoingQuoteRef;
            lead.RepCompletionNotes = notes;
            lead.RepCompletedAt = DateTime.UtcNow;
            lead.Status = QuoteStatus.LeadRepCompleted;
            lead.UpdatedAt = DateTime.UtcNow;

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                UserId = repUserId,
                Action = "Lead Marked Complete (Rep)",
                Details = $"Rep submitted lead for closure. LeadRef='{lead.QuoteReference ?? "-"}', AttachedQuoteRef='{outgoingQuoteRef}', Notes='{notes ?? ""}', LeadId={lead.Id}",
                Timestamp = DateTime.UtcNow,
                RelatedQuoteId = lead.Id
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task CloseLeadAsAdminAsync(Guid leadId, string adminUserId, string? closureNotes)
        {
            var lead = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == leadId);
            if (lead == null) throw new Exception("Lead not found.");

            lead.Status = QuoteStatus.LeadClosed;
            lead.ClosedByUserId = adminUserId;
            lead.ClosedAt = DateTime.UtcNow;
            lead.ClosureNotes = closureNotes;
            lead.UpdatedAt = DateTime.UtcNow;

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                UserId = adminUserId,
                Action = "Lead Closed (Admin/Head)",
                Details = $"Admin closed lead. LeadRef='{lead.QuoteReference ?? "-"}', Notes='{closureNotes ?? ""}', LeadId={lead.Id}",
                Timestamp = DateTime.UtcNow,
                RelatedQuoteId = lead.Id
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task CreateMergeRequestAsync(Guid sourceId, Guid targetId, string requestedByUserId, string? reason)
        {
            var req = new MergeRequest
            {
                Id = Guid.NewGuid(),
                SourceQuoteId = sourceId,
                TargetQuoteId = targetId,
                RequestedByUserId = requestedByUserId,
                Reason = reason,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.MergeRequests.Add(req);

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                UserId = requestedByUserId,
                Action = "Merge Requested",
                Details = $"Merge requested. SourceId={sourceId}, TargetId={targetId}, Reason='{reason ?? ""}', MergeRequestId={req.Id}",
                Timestamp = DateTime.UtcNow,
                RelatedQuoteId = targetId
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<MergeRequest>> GetPendingMergeRequestsAsync()
        {
            return await _dbContext.MergeRequests
                .AsNoTracking()
                .Where(m => m.Status == "Pending")
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task ApproveMergeRequestAsync(Guid mergeRequestId, string adminUserId, string? notes)
        {
            var req = await _dbContext.MergeRequests.FirstOrDefaultAsync(m => m.Id == mergeRequestId);
            if (req == null) throw new Exception("Merge request not found.");

            req.Status = "Approved";
            req.ReviewedByUserId = adminUserId;
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewNotes = notes;

            var source = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == req.SourceQuoteId);

            if (source != null)
            {
                source.IsMerged = true;
                source.MergedIntoQuoteId = req.TargetQuoteId;
                source.MergeNotes = notes;
                source.UpdatedAt = DateTime.UtcNow;
            }

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                UserId = adminUserId,
                Action = "Merge Approved",
                Details = $"Merge approved. SourceId={req.SourceQuoteId}, TargetId={req.TargetQuoteId}, Notes='{notes ?? ""}', MergeRequestId={req.Id}",
                Timestamp = DateTime.UtcNow,
                RelatedQuoteId = req.TargetQuoteId
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task RejectMergeRequestAsync(Guid mergeRequestId, string adminUserId, string? notes)
        {
            var req = await _dbContext.MergeRequests.FirstOrDefaultAsync(m => m.Id == mergeRequestId);
            if (req == null) throw new Exception("Merge request not found.");

            req.Status = "Rejected";
            req.ReviewedByUserId = adminUserId;
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewNotes = notes;

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                UserId = adminUserId,
                Action = "Merge Rejected",
                Details = $"Merge rejected. SourceId={req.SourceQuoteId}, TargetId={req.TargetQuoteId}, Notes='{notes ?? ""}', MergeRequestId={req.Id}",
                Timestamp = DateTime.UtcNow,
                RelatedQuoteId = req.TargetQuoteId
            });

            await _dbContext.SaveChangesAsync();
        }
    }
}
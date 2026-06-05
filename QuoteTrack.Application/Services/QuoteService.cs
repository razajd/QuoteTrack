using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Application.DTOs;
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
            if (await _dbContext.QuoteListItems.AsNoTracking().AnyAsync())
            {
                var readModelQuery = _dbContext.QuoteListItems.AsNoTracking().AsQueryable();

                if (!isAdmin)
                {
                    if (string.IsNullOrWhiteSpace(userId))
                        readModelQuery = readModelQuery.Where(q => false);
                    else
                        readModelQuery = readModelQuery.Where(q => q.OwnerId == userId);
                }

                var rows = await readModelQuery
                    .OrderByDescending(q => q.CreatedAt)
                    .ToListAsync();

                return rows.Select(MapQuoteListItemToQuote).ToList();
            }

            var query = _dbContext.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Include(q => q.Client)
                .Include(q => q.FollowUps)
                .AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    query = query.Where(q => false);
                else
                    query = query.Where(q => q.OwnerId == userId);
            }

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Quote>> GetDashboardQuotesAsync(string? userId, bool isAdmin, string? ownerId = null)
        {
            var query = _dbContext.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Include(q => q.Client)
                .Include(q => q.FollowUps
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(5))
                .AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    query = query.Where(q => false);
                else
                    query = query.Where(q => q.OwnerId == userId);
            }
            else if (!string.IsNullOrWhiteSpace(ownerId))
            {
                query = query.Where(q => q.OwnerId == ownerId);
            }

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Quote>> GetQuotesPageAsync(
            string? userId,
            bool isAdmin,
            int pageNumber,
            int pageSize,
            string? ownerId,
            string? searchText,
            string sortColumn,
            bool sortAsc)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _dbContext.QuoteListItems
                .AsNoTracking()
                .Where(q => q.RecordType != QuoteRecordType.Lead)
                .AsQueryable();

            query = ApplyQuotesReadModelFilters(query, userId, isAdmin, ownerId, searchText);
            query = ApplyQuotesReadModelSorting(query, sortColumn, sortAsc);

            var rows = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return rows.Select(MapQuoteListItemToQuote).ToList();
        }

        public async Task<int> GetQuotesPageCountAsync(
            string? userId,
            bool isAdmin,
            string? ownerId,
            string? searchText)
        {
            var query = _dbContext.QuoteListItems
                .AsNoTracking()
                .Where(q => q.RecordType != QuoteRecordType.Lead)
                .AsQueryable();

            query = ApplyQuotesReadModelFilters(query, userId, isAdmin, ownerId, searchText);

            return await query.CountAsync();
        }

        public async Task<List<Quote>> GetActiveLeadsPageAsync(
            string? userId,
            bool canViewAllLeads,
            int pageNumber,
            int pageSize,
            string? ownerId,
            string? searchText,
            string sortColumn,
            bool sortAsc)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _dbContext.QuoteListItems
                .AsNoTracking()
                .Where(q => q.RecordType == QuoteRecordType.Lead)
                .Where(q => !q.IsDeleteRequested)
                .Where(q =>
                    q.Status != QuoteStatus.LeadClosed &&
                    q.Status != QuoteStatus.LeadRepCompleted &&
                    q.Status != QuoteStatus.Won &&
                    q.Status != QuoteStatus.Lost &&
                    q.Status != QuoteStatus.Cancelled &&
                    q.Status != QuoteStatus.Merged)
                .AsQueryable();

            query = ApplyActiveLeadsReadModelFilters(query, userId, canViewAllLeads, ownerId, searchText);
            query = ApplyActiveLeadsReadModelSorting(query, sortColumn, sortAsc);

            var rows = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return rows.Select(MapQuoteListItemToQuote).ToList();
        }

        public async Task<int> GetActiveLeadsPageCountAsync(
            string? userId,
            bool canViewAllLeads,
            string? ownerId,
            string? searchText)
        {
            var query = _dbContext.QuoteListItems
                .AsNoTracking()
                .Where(q => q.RecordType == QuoteRecordType.Lead)
                .Where(q => !q.IsDeleteRequested)
                .Where(q =>
                    q.Status != QuoteStatus.LeadClosed &&
                    q.Status != QuoteStatus.LeadRepCompleted &&
                    q.Status != QuoteStatus.Won &&
                    q.Status != QuoteStatus.Lost &&
                    q.Status != QuoteStatus.Cancelled &&
                    q.Status != QuoteStatus.Merged)
                .AsQueryable();

            query = ApplyActiveLeadsReadModelFilters(query, userId, canViewAllLeads, ownerId, searchText);

            return await query.CountAsync();
        }

        private static IQueryable<QuoteListItem> ApplyQuotesReadModelFilters(
            IQueryable<QuoteListItem> query,
            string? userId,
            bool isAdmin,
            string? ownerId,
            string? searchText)
        {
            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return query.Where(q => false);

                query = query.Where(q => q.OwnerId == userId);
            }
            else if (!string.IsNullOrWhiteSpace(ownerId))
            {
                query = query.Where(q => q.OwnerId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var s = searchText.Trim().ToLower();
                query = query.Where(x => x.SearchText.Contains(s));
            }

            return query;
        }

        private static IQueryable<QuoteListItem> ApplyQuotesReadModelSorting(
            IQueryable<QuoteListItem> query,
            string? sortColumn,
            bool sortAsc)
        {
            sortColumn = (sortColumn ?? "Created").Trim();

            return (sortColumn, sortAsc) switch
            {
                ("Client", true) => query.OrderBy(x => x.ClientName),
                ("Client", false) => query.OrderByDescending(x => x.ClientName),

                ("Reference", true) => query.OrderBy(x => x.QuoteReference),
                ("Reference", false) => query.OrderByDescending(x => x.QuoteReference),

                ("Value", true) => query.OrderBy(x => x.QuoteValue ?? 0m),
                ("Value", false) => query.OrderByDescending(x => x.QuoteValue ?? 0m),

                ("Status", true) => query.OrderBy(x => x.Status),
                ("Status", false) => query.OrderByDescending(x => x.Status),

                ("Owner", true) => query.OrderBy(x => x.OwnerName),
                ("Owner", false) => query.OrderByDescending(x => x.OwnerName),

                ("NextDue", true) => query.OrderBy(x => x.NextFollowUpDate ?? DateTime.MaxValue),
                ("NextDue", false) => query.OrderByDescending(x => x.NextFollowUpDate ?? DateTime.MaxValue),

                ("Created", true) => query.OrderBy(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt),
            };
        }

        private static IQueryable<QuoteListItem> ApplyActiveLeadsReadModelFilters(
            IQueryable<QuoteListItem> query,
            string? userId,
            bool canViewAllLeads,
            string? ownerId,
            string? searchText)
        {
            if (!canViewAllLeads)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return query.Where(q => false);

                query = query.Where(q => q.OwnerId == userId);
            }
            else if (!string.IsNullOrWhiteSpace(ownerId))
            {
                if (ownerId == "__unassigned__")
                    query = query.Where(q => q.OwnerId == null || q.OwnerId == "");
                else
                    query = query.Where(q => q.OwnerId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var s = searchText.Trim().ToLower();
                query = query.Where(x => x.SearchText.Contains(s));
            }

            return query;
        }

        private static IQueryable<QuoteListItem> ApplyActiveLeadsReadModelSorting(
            IQueryable<QuoteListItem> query,
            string? sortColumn,
            bool sortAsc)
        {
            sortColumn = (sortColumn ?? "Received").Trim();

            return (sortColumn, sortAsc) switch
            {
                ("ClientRef", true) => query.OrderBy(x => x.ClientName),
                ("ClientRef", false) => query.OrderByDescending(x => x.ClientName),

                ("Owner", true) => query.OrderBy(x => x.OwnerName),
                ("Owner", false) => query.OrderByDescending(x => x.OwnerName),

                ("Value", true) => query.OrderBy(x => x.QuoteValue ?? 0m),
                ("Value", false) => query.OrderByDescending(x => x.QuoteValue ?? 0m),

                ("Status", true) => query.OrderBy(x => x.Status),
                ("Status", false) => query.OrderByDescending(x => x.Status),

                ("NextDue", true) => query.OrderBy(x => x.NextFollowUpDate ?? DateTime.MaxValue),
                ("NextDue", false) => query.OrderByDescending(x => x.NextFollowUpDate ?? DateTime.MaxValue),

                ("LastNote", true) => query.OrderBy(x => x.LastNoteAt ?? DateTime.MinValue),
                ("LastNote", false) => query.OrderByDescending(x => x.LastNoteAt ?? DateTime.MinValue),

                ("Received", true) => query.OrderBy(x => x.EmailReceivedDateTime),
                _ => query.OrderByDescending(x => x.EmailReceivedDateTime),
            };
        }

        private static Quote MapQuoteListItemToQuote(QuoteListItem item)
        {
            var quote = new Quote
            {
                Id = item.QuoteId,
                RecordType = item.RecordType,
                Status = item.Status,
                IsDeleteRequested = item.IsDeleteRequested,
                OwnerId = item.OwnerId,
                ClientId = item.ClientId,
                ClientName = item.ClientName,
                SenderEmail = item.SenderEmail,
                SenderName = item.SenderName,
                Subject = item.Subject,
                QuoteReference = item.QuoteReference,
                QuoteValue = item.QuoteValue,
                WinProbability = item.WinProbability,
                Currency = item.Currency,
                NextFollowUpDate = item.NextFollowUpDate,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                EmailReceivedDateTime = item.EmailReceivedDateTime,
                RepCompletedAt = item.RepCompletedAt,
                DeleteRequestReason = item.DeleteRequestReason,
                DeleteRequestedByUserId = string.IsNullOrWhiteSpace(item.DeleteRequestedByUserId) ? null : item.DeleteRequestedByUserId,
                LeadSource = item.LeadSource
            };

            if (!string.IsNullOrWhiteSpace(item.OwnerId) || !string.IsNullOrWhiteSpace(item.OwnerName))
            {
                quote.Owner = new ApplicationUser
                {
                    Id = item.OwnerId ?? "",
                    FullName = item.OwnerName
                };
            }

            if (item.ClientId.HasValue || !string.IsNullOrWhiteSpace(item.ClientName))
            {
                quote.Client = new Client
                {
                    Id = item.ClientId ?? Guid.Empty,
                    CompanyName = item.ClientName
                };
            }

            if (item.LastNoteAt.HasValue || !string.IsNullOrWhiteSpace(item.LastNotePreview))
            {
                quote.FollowUps = new List<FollowUp>
                {
                    new FollowUp
                    {
                        QuoteId = item.QuoteId,
                        CreatedAt = item.LastNoteAt ?? DateTime.MinValue,
                        Notes = item.LastNotePreview,
                        DueDate = item.NextFollowUpDate ?? DateTime.UtcNow
                    }
                };
            }

            return quote;
        }


        private static IQueryable<Quote> ApplyQuotesListFilters(
            IQueryable<Quote> query,
            string? userId,
            bool isAdmin,
            string? ownerId,
            string? searchText)
        {
            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return query.Where(q => false);

                query = query.Where(q => q.OwnerId == userId);
            }
            else if (!string.IsNullOrWhiteSpace(ownerId))
            {
                query = query.Where(q => q.OwnerId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var s = searchText.Trim().ToLower();

                query = query.Where(x =>
                    (x.Client != null && x.Client.CompanyName != null && x.Client.CompanyName.ToLower().Contains(s)) ||
                    (x.ClientName != null && x.ClientName.ToLower().Contains(s)) ||
                    (x.SenderEmail != null && x.SenderEmail.ToLower().Contains(s)) ||
                    (x.QuoteReference != null && x.QuoteReference.ToLower().Contains(s)) ||
                    (x.Subject != null && x.Subject.ToLower().Contains(s)));
            }

            return query;
        }

        private static IQueryable<Quote> ApplyQuotesListSorting(
            IQueryable<Quote> query,
            string? sortColumn,
            bool sortAsc)
        {
            sortColumn = (sortColumn ?? "Created").Trim();

            return (sortColumn, sortAsc) switch
            {
                ("Client", true) => query.OrderBy(x => x.Client != null ? x.Client.CompanyName : (x.ClientName ?? x.SenderEmail ?? "")),
                ("Client", false) => query.OrderByDescending(x => x.Client != null ? x.Client.CompanyName : (x.ClientName ?? x.SenderEmail ?? "")),

                ("Reference", true) => query.OrderBy(x => x.QuoteReference ?? ""),
                ("Reference", false) => query.OrderByDescending(x => x.QuoteReference ?? ""),

                ("Value", true) => query.OrderBy(x => x.QuoteValue ?? 0m),
                ("Value", false) => query.OrderByDescending(x => x.QuoteValue ?? 0m),

                ("Status", true) => query.OrderBy(x => x.Status),
                ("Status", false) => query.OrderByDescending(x => x.Status),

                ("Owner", true) => query.OrderBy(x => x.Owner != null ? x.Owner.FullName : ""),
                ("Owner", false) => query.OrderByDescending(x => x.Owner != null ? x.Owner.FullName : ""),

                ("NextDue", true) => query.OrderBy(x => x.NextFollowUpDate ?? DateTime.MaxValue),
                ("NextDue", false) => query.OrderByDescending(x => x.NextFollowUpDate ?? DateTime.MaxValue),

                ("Created", true) => query.OrderBy(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt),
            };
        }

        private static IQueryable<Quote> ApplyActiveLeadsFilters(
            IQueryable<Quote> query,
            string? userId,
            bool canViewAllLeads,
            string? ownerId,
            string? searchText)
        {
            if (!canViewAllLeads)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return query.Where(q => false);

                query = query.Where(q => q.OwnerId == userId);
            }
            else if (!string.IsNullOrWhiteSpace(ownerId))
            {
                if (ownerId == "__unassigned__")
                    query = query.Where(q => q.OwnerId == null || q.OwnerId == "");
                else
                    query = query.Where(q => q.OwnerId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var s = searchText.Trim().ToLower();

                query = query.Where(x =>
                    (x.Client != null && x.Client.CompanyName != null && x.Client.CompanyName.ToLower().Contains(s)) ||
                    (x.ClientName != null && x.ClientName.ToLower().Contains(s)) ||
                    (x.SenderEmail != null && x.SenderEmail.ToLower().Contains(s)) ||
                    (x.QuoteReference != null && x.QuoteReference.ToLower().Contains(s)) ||
                    (x.Subject != null && x.Subject.ToLower().Contains(s)));
            }

            return query;
        }

        private static IQueryable<Quote> ApplyActiveLeadsSorting(
            IQueryable<Quote> query,
            string? sortColumn,
            bool sortAsc)
        {
            sortColumn = (sortColumn ?? "Received").Trim();

            return (sortColumn, sortAsc) switch
            {
                ("ClientRef", true) => query.OrderBy(x => x.Client != null ? x.Client.CompanyName : (x.ClientName ?? x.SenderEmail ?? "")),
                ("ClientRef", false) => query.OrderByDescending(x => x.Client != null ? x.Client.CompanyName : (x.ClientName ?? x.SenderEmail ?? "")),

                ("Owner", true) => query.OrderBy(x => x.Owner != null ? x.Owner.FullName : ""),
                ("Owner", false) => query.OrderByDescending(x => x.Owner != null ? x.Owner.FullName : ""),

                ("Value", true) => query.OrderBy(x => x.QuoteValue ?? 0m),
                ("Value", false) => query.OrderByDescending(x => x.QuoteValue ?? 0m),

                ("Status", true) => query.OrderBy(x => x.Status),
                ("Status", false) => query.OrderByDescending(x => x.Status),

                ("NextDue", true) => query.OrderBy(x => x.NextFollowUpDate ?? DateTime.MaxValue),
                ("NextDue", false) => query.OrderByDescending(x => x.NextFollowUpDate ?? DateTime.MaxValue),

                ("LastNote", true) => query.OrderBy(x => x.FollowUps.Max(f => (DateTime?)f.CreatedAt) ?? DateTime.MinValue),
                ("LastNote", false) => query.OrderByDescending(x => x.FollowUps.Max(f => (DateTime?)f.CreatedAt) ?? DateTime.MinValue),

                ("Received", true) => query.OrderBy(x => x.EmailReceivedDateTime),
                _ => query.OrderByDescending(x => x.EmailReceivedDateTime),
            };
        }

        public async Task<int> GetTotalQuotesCountAsync(string? userId, bool isAdmin)
        {
            if (await _dbContext.QuoteListItems.AsNoTracking().AnyAsync())
            {
                var readModelQuery = _dbContext.QuoteListItems.AsNoTracking().AsQueryable();

                if (!isAdmin)
                {
                    if (string.IsNullOrWhiteSpace(userId)) return 0;
                    readModelQuery = readModelQuery.Where(q => q.OwnerId == userId);
                }

                return await readModelQuery.CountAsync();
            }

            var query = _dbContext.Quotes.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId)) return 0;
                query = query.Where(q => q.OwnerId == userId);
            }

            return await query.CountAsync();
        }

        public async Task<decimal> GetTotalQuoteValueAsync(string? userId, bool isAdmin)
        {
            if (await _dbContext.QuoteListItems.AsNoTracking().AnyAsync())
            {
                var readModelQuery = _dbContext.QuoteListItems.AsNoTracking().AsQueryable();

                if (!isAdmin)
                {
                    if (string.IsNullOrWhiteSpace(userId)) return 0;
                    readModelQuery = readModelQuery.Where(q => q.OwnerId == userId);
                }

                return await readModelQuery.SumAsync(q => q.QuoteValue ?? 0);
            }

            var query = _dbContext.Quotes.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId)) return 0;
                query = query.Where(q => q.OwnerId == userId);
            }

            return await query.SumAsync(q => q.QuoteValue ?? 0);
        }

        public async Task<int> GetFollowUpsDueCountAsync(string? userId, bool isAdmin)
        {
            if (await _dbContext.QuoteListItems.AsNoTracking().AnyAsync())
            {
                var readModelQuery = _dbContext.QuoteListItems.AsNoTracking().AsQueryable();

                if (!isAdmin)
                {
                    if (string.IsNullOrWhiteSpace(userId)) return 0;
                    readModelQuery = readModelQuery.Where(q => q.OwnerId == userId);
                }

                return await readModelQuery.CountAsync(q => q.NextFollowUpDate != null && q.NextFollowUpDate <= DateTime.UtcNow);
            }

            var query = _dbContext.Quotes.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                if (string.IsNullOrWhiteSpace(userId)) return 0;
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

        public async Task<NotificationCountsDto> GetNotificationCountsAsync()
        {
            var activeLeadStatusesToExclude = new[]
            {
                QuoteStatus.Won,
                QuoteStatus.Lost,
                QuoteStatus.Cancelled,
                QuoteStatus.LeadClosed,
                QuoteStatus.Merged
            };

            var pendingClientCount = await _dbContext.Clients
                .AsNoTracking()
                .CountAsync(c => c.ApprovalStatus == ClientApprovalStatus.Pending);

            var useReadModel = await _dbContext.QuoteListItems.AsNoTracking().AnyAsync();

            var unassignedLeadCount = useReadModel
                ? await _dbContext.QuoteListItems
                    .AsNoTracking()
                    .CountAsync(q =>
                        q.RecordType == QuoteRecordType.Lead &&
                        string.IsNullOrEmpty(q.OwnerId) &&
                        !q.IsDeleteRequested &&
                        !activeLeadStatusesToExclude.Contains(q.Status))
                : await _dbContext.Quotes
                    .AsNoTracking()
                    .CountAsync(q =>
                        q.RecordType == QuoteRecordType.Lead &&
                        string.IsNullOrEmpty(q.OwnerId) &&
                        !q.IsDeleteRequested &&
                        !activeLeadStatusesToExclude.Contains(q.Status));

            var pendingDeletionCount = useReadModel
                ? await _dbContext.QuoteListItems
                    .AsNoTracking()
                    .CountAsync(q => q.IsDeleteRequested)
                : await _dbContext.Quotes
                    .AsNoTracking()
                    .CountAsync(q => q.IsDeleteRequested);

            return new NotificationCountsDto
            {
                PendingClientCount = pendingClientCount,
                UnassignedLeadCount = unassignedLeadCount,
                PendingDeletionCount = pendingDeletionCount
            };
        }

        public async Task<List<Quote>> GetLeadDispatchBoardQuotesAsync()
        {
            var closedStatuses = new[]
            {
                QuoteStatus.Won,
                QuoteStatus.Lost,
                QuoteStatus.Cancelled,
                QuoteStatus.LeadClosed,
                QuoteStatus.Merged
            };

            if (await _dbContext.QuoteListItems.AsNoTracking().AnyAsync())
            {
                var rows = await _dbContext.QuoteListItems
                    .AsNoTracking()
                    .Where(q => q.RecordType == QuoteRecordType.Lead)
                    .Where(q => !q.IsDeleteRequested)
                    .Where(q => !closedStatuses.Contains(q.Status))
                    .OrderByDescending(q => q.CreatedAt)
                    .ToListAsync();

                return rows.Select(MapQuoteListItemToQuote).ToList();
            }

            return await _dbContext.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Include(q => q.Client)
                .Where(q => q.RecordType == QuoteRecordType.Lead)
                .Where(q => !q.IsDeleteRequested)
                .Where(q => !closedStatuses.Contains(q.Status))
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Quote>> GetPendingLeadClosuresAsync()
        {
            if (await _dbContext.QuoteListItems.AsNoTracking().AnyAsync())
            {
                var rows = await _dbContext.QuoteListItems
                    .AsNoTracking()
                    .Where(q => q.RecordType == QuoteRecordType.Lead)
                    .Where(q => !q.IsDeleteRequested)
                    .Where(q => q.Status == QuoteStatus.LeadRepCompleted)
                    .OrderByDescending(q => q.RepCompletedAt ?? q.UpdatedAt)
                    .ToListAsync();

                return rows.Select(MapQuoteListItemToQuote).ToList();
            }

            return await _dbContext.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Include(q => q.Client)
                .Where(q => q.RecordType == QuoteRecordType.Lead)
                .Where(q => !q.IsDeleteRequested)
                .Where(q => q.Status == QuoteStatus.LeadRepCompleted)
                .OrderByDescending(q => q.RepCompletedAt ?? q.UpdatedAt)
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
            var now = DateTime.UtcNow;
            quote.CreatedAt = quote.CreatedAt == default ? now : quote.CreatedAt;
            quote.UpdatedAt = quote.UpdatedAt == default ? now : quote.UpdatedAt;
            ApplyMilestonesForCurrentState(quote, now);

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

            AddQuoteEvent(
                quote.Id,
                QuoteEventType.Created,
                logUserId,
                toStatus: quote.Status,
                toOwnerId: quote.OwnerId,
                details: $"Created {quote.RecordType}. Ref='{quote.QuoteReference ?? "-"}', Client='{quote.ClientName ?? "-"}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateQuoteAsync(Quote quote, string? actorUserId = null)
        {
            var existing = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == quote.Id);
            if (existing == null)
                throw new Exception("Quote not found.");

            var now = DateTime.UtcNow;
            var previousStatus = existing.Status;
            var previousOwnerId = existing.OwnerId;
            var previousFollowUpDate = existing.NextFollowUpDate;
            var previousClientId = existing.ClientId;
            var previousQuoteValue = existing.QuoteValue;

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
            existing.UpdatedAt = now;

            ApplyMilestonesForCurrentState(existing, now, previousStatus, previousOwnerId);

            var effectiveActorUserId = !string.IsNullOrWhiteSpace(actorUserId)
                ? actorUserId
                : (!string.IsNullOrWhiteSpace(existing.OwnerId) ? existing.OwnerId : existing.ClosedByUserId);

            if (previousStatus != existing.Status)
            {
                AddQuoteEvent(
                    existing.Id,
                    QuoteEventType.StatusChanged,
                    effectiveActorUserId,
                    fromStatus: previousStatus,
                    toStatus: existing.Status,
                    details: $"Status changed from {previousStatus} to {existing.Status}.");
            }

            if (!StringEquals(previousOwnerId, existing.OwnerId))
            {
                AddQuoteEvent(
                    existing.Id,
                    QuoteEventType.OwnerChanged,
                    effectiveActorUserId,
                    fromOwnerId: previousOwnerId,
                    toOwnerId: existing.OwnerId,
                    details: "Owner assignment changed.");
            }

            if (previousFollowUpDate != existing.NextFollowUpDate)
            {
                AddQuoteEvent(
                    existing.Id,
                    QuoteEventType.FollowUpDateChanged,
                    effectiveActorUserId,
                    details: $"Follow-up date changed from '{previousFollowUpDate?.ToString("u") ?? "-"}' to '{existing.NextFollowUpDate?.ToString("u") ?? "-"}'.");
            }

            if (previousClientId != existing.ClientId)
            {
                AddQuoteEvent(
                    existing.Id,
                    QuoteEventType.ClientLinked,
                    effectiveActorUserId,
                    details: $"Client link changed from '{previousClientId?.ToString() ?? "-"}' to '{existing.ClientId?.ToString() ?? "-"}'.");
            }

            if (previousQuoteValue != existing.QuoteValue)
            {
                AddQuoteEvent(
                    existing.Id,
                    QuoteEventType.ValueChanged,
                    effectiveActorUserId,
                    details: $"Quote value changed from '{previousQuoteValue?.ToString() ?? "-"}' to '{existing.QuoteValue?.ToString() ?? "-"}'.");
            }

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

            await MarkCommandCenterSnapshotsStaleAsync();
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

            AddQuoteEvent(
                followUp.QuoteId,
                QuoteEventType.FollowUpAdded,
                logUserId,
                details: $"Follow-up added. Due='{followUp.DueDate:u}', Note='{(followUp.Notes ?? "").Trim()}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
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

            AddQuoteEvent(
                quote.Id,
                QuoteEventType.FollowUpDateChanged,
                quote.OwnerId,
                details: $"Snoozed by {days} day(s). New due='{newDue:u}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteQuoteAsync(Guid id)
        {
            var quote = await _dbContext.Quotes.FindAsync(id);
            if (quote == null) return;

            _dbContext.Quotes.Remove(quote);
            await MarkCommandCenterSnapshotsStaleAsync();
            await _dbContext.SaveChangesAsync();
        }

        public async Task SubmitLeadAsRepCompleteAsync(Guid leadId, string repUserId, string outgoingQuoteRef, string? notes)
        {
            var lead = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == leadId);
            if (lead == null) throw new Exception("Lead not found.");

            var previousStatus = lead.Status;
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

            AddQuoteEvent(
                lead.Id,
                QuoteEventType.LeadRepCompleted,
                repUserId,
                fromStatus: previousStatus,
                toStatus: lead.Status,
                details: $"Rep submitted lead for closure. Attached quote ref='{outgoingQuoteRef}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
            await _dbContext.SaveChangesAsync();
        }

        public async Task CloseLeadAsAdminAsync(Guid leadId, string adminUserId, string? closureNotes)
        {
            var lead = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == leadId);
            if (lead == null) throw new Exception("Lead not found.");

            var previousStatus = lead.Status;
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

            AddQuoteEvent(
                lead.Id,
                QuoteEventType.LeadClosed,
                adminUserId,
                fromStatus: previousStatus,
                toStatus: lead.Status,
                details: $"Admin closed lead. Notes='{closureNotes ?? ""}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
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

            AddQuoteEvent(
                targetId,
                QuoteEventType.MergeRequested,
                requestedByUserId,
                details: $"Merge requested. SourceId={sourceId}, TargetId={targetId}, Reason='{reason ?? ""}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
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

            AddQuoteEvent(
                req.TargetQuoteId,
                QuoteEventType.MergeApproved,
                adminUserId,
                details: $"Merge approved. SourceId={req.SourceQuoteId}, TargetId={req.TargetQuoteId}, Notes='{notes ?? ""}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
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

            AddQuoteEvent(
                req.TargetQuoteId,
                QuoteEventType.MergeRejected,
                adminUserId,
                details: $"Merge rejected. SourceId={req.SourceQuoteId}, TargetId={req.TargetQuoteId}, Notes='{notes ?? ""}'.");

            await MarkCommandCenterSnapshotsStaleAsync();
            await _dbContext.SaveChangesAsync();
        }

        private void AddQuoteEvent(
            Guid quoteId,
            QuoteEventType eventType,
            string? actorUserId,
            QuoteStatus? fromStatus = null,
            QuoteStatus? toStatus = null,
            string? fromOwnerId = null,
            string? toOwnerId = null,
            string? details = null,
            string? metadataJson = null)
        {
            _dbContext.QuoteEvents.Add(new QuoteEvent
            {
                Id = Guid.NewGuid(),
                QuoteId = quoteId,
                EventType = eventType,
                ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                FromOwnerId = string.IsNullOrWhiteSpace(fromOwnerId) ? null : fromOwnerId,
                ToOwnerId = string.IsNullOrWhiteSpace(toOwnerId) ? null : toOwnerId,
                OccurredAt = DateTime.UtcNow,
                Details = details,
                MetadataJson = metadataJson
            });
        }

        private static void ApplyMilestonesForCurrentState(
            Quote quote,
            DateTime now,
            QuoteStatus? previousStatus = null,
            string? previousOwnerId = null)
        {
            if (!string.IsNullOrWhiteSpace(quote.OwnerId) &&
                string.IsNullOrWhiteSpace(previousOwnerId) &&
                !quote.AssignedAt.HasValue)
            {
                quote.AssignedAt = now;
            }

            if (IsFirstContactStatus(quote.Status) && !quote.FirstContactedAt.HasValue)
                quote.FirstContactedAt = now;

            if (quote.RecordType == QuoteRecordType.OutgoingQuote &&
                IsQuotedStatus(quote.Status) &&
                !quote.QuotedAt.HasValue)
            {
                quote.QuotedAt = quote.EmailSentDateTime == default ? now : quote.EmailSentDateTime;
            }

            if (quote.Status == QuoteStatus.Won && !quote.WonAt.HasValue)
            {
                quote.WonAt = now;
                quote.ClosedAt ??= now;
            }

            if ((quote.Status == QuoteStatus.Lost || quote.Status == QuoteStatus.Cancelled) &&
                !quote.LostAt.HasValue)
            {
                quote.LostAt = now;
                quote.ClosedAt ??= now;
            }

            if (previousStatus.HasValue && previousStatus.Value == quote.Status)
                return;
        }

        private static bool IsFirstContactStatus(QuoteStatus status)
        {
            return status == QuoteStatus.ContactMade ||
                   status == QuoteStatus.WaitingClientResponse ||
                   status == QuoteStatus.QuotationInPreparation ||
                   status == QuoteStatus.InProgress ||
                   status == QuoteStatus.LeadInProgress ||
                   status == QuoteStatus.Sent;
        }

        private static bool IsQuotedStatus(QuoteStatus status)
        {
            return status == QuoteStatus.Sent ||
                   status == QuoteStatus.QuoteNew ||
                   status == QuoteStatus.QuoteReviewed ||
                   status == QuoteStatus.QuoteApproved ||
                   status == QuoteStatus.Won ||
                   status == QuoteStatus.Lost;
        }

        private static bool StringEquals(string? left, string? right)
        {
            return string.Equals(
                string.IsNullOrWhiteSpace(left) ? null : left,
                string.IsNullOrWhiteSpace(right) ? null : right,
                StringComparison.Ordinal);
        }

        private async Task MarkCommandCenterSnapshotsStaleAsync()
        {
            var snapshots = await _dbContext.CommandCenterSnapshots.ToListAsync();
            foreach (var snapshot in snapshots)
                snapshot.IsStale = true;

            var quoteListState = await _dbContext.ReadModelStates.FirstOrDefaultAsync(s => s.Key == QuoteListReadModelService.StateKey);
            if (quoteListState == null)
            {
                quoteListState = new ReadModelState { Key = QuoteListReadModelService.StateKey };
                _dbContext.ReadModelStates.Add(quoteListState);
            }

            quoteListState.IsStale = true;
        }

    }
}

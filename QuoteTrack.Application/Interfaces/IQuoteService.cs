using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteTrack.Application.DTOs;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IQuoteService
    {
        Task<List<Quote>> GetAllQuotesAsync(string? userId, bool isAdmin);

        // Dashboard-specific lighter load
        Task<List<Quote>> GetDashboardQuotesAsync(string? userId, bool isAdmin, string? ownerId = null);

        // Quotes page pagination
        Task<List<Quote>> GetQuotesPageAsync(
            string? userId,
            bool isAdmin,
            int pageNumber,
            int pageSize,
            string? ownerId,
            string? searchText,
            string sortColumn,
            bool sortAsc);

        Task<int> GetQuotesPageCountAsync(
            string? userId,
            bool isAdmin,
            string? ownerId,
            string? searchText);

        // Active leads pagination
        Task<List<Quote>> GetActiveLeadsPageAsync(
            string? userId,
            bool canViewAllLeads,
            int pageNumber,
            int pageSize,
            string? ownerId,
            string? searchText,
            string sortColumn,
            bool sortAsc);

        Task<int> GetActiveLeadsPageCountAsync(
            string? userId,
            bool canViewAllLeads,
            string? ownerId,
            string? searchText);

        Task<int> GetTotalQuotesCountAsync(string? userId, bool isAdmin);
        Task<decimal> GetTotalQuoteValueAsync(string? userId, bool isAdmin);
        Task<int> GetFollowUpsDueCountAsync(string? userId, bool isAdmin);
        Task<Quote?> GetQuoteByIdAsync(Guid id);
        Task AddQuoteAsync(Quote quote);
        Task UpdateQuoteAsync(Quote quote, string? actorUserId = null);
        Task AddFollowUpAsync(FollowUp followUp);
        Task<List<ApplicationUser>> GetSystemUsersAsync();
        Task<NotificationCountsDto> GetNotificationCountsAsync();
        Task<List<Quote>> GetLeadDispatchBoardQuotesAsync();
        Task<List<Quote>> GetPendingLeadClosuresAsync();
        Task DeleteQuoteAsync(Guid id);
        Task SnoozeQuoteAsync(Guid id, int days);

        Task SubmitLeadAsRepCompleteAsync(Guid leadId, string repUserId, string outgoingQuoteRef, string? notes);
        Task CloseLeadAsAdminAsync(Guid leadId, string adminUserId, string? closureNotes);

        Task CreateMergeRequestAsync(Guid sourceId, Guid targetId, string requestedByUserId, string? reason);
        Task<List<MergeRequest>> GetPendingMergeRequestsAsync();
        Task ApproveMergeRequestAsync(Guid mergeRequestId, string adminUserId, string? notes);
        Task RejectMergeRequestAsync(Guid mergeRequestId, string adminUserId, string? notes);
    }
}

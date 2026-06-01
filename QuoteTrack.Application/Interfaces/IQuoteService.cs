using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IQuoteService
    {
        Task<List<Quote>> GetAllQuotesAsync(string? userId, bool isAdmin);
        Task<int> GetTotalQuotesCountAsync(string? userId, bool isAdmin);
        Task<decimal> GetTotalQuoteValueAsync(string? userId, bool isAdmin);
        Task<int> GetFollowUpsDueCountAsync(string? userId, bool isAdmin);
        Task<Quote?> GetQuoteByIdAsync(Guid id);
        Task AddQuoteAsync(Quote quote);

        // actorUserId is optional so older pages still compile,
        // but pages that know the editor should pass it.
        Task UpdateQuoteAsync(Quote quote, string? actorUserId = null);

        Task AddFollowUpAsync(FollowUp followUp);
        Task<List<ApplicationUser>> GetSystemUsersAsync();
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

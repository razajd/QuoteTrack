using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IRfqService
    {
        Task<List<Rfq>> GetUnassignedRfqsAsync();
        Task<List<Rfq>> GetMyRfqsAsync(string userId);

        // Added so DeptHead/Admin can view and filter the lead inbox.
        Task<List<Rfq>> GetAllRfqsAsync();

        Task AssignRfqAsync(Guid rfqId, string userId);
    }
}

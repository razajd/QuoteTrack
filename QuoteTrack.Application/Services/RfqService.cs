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
    public class RfqService : IRfqService
    {
        private readonly IAppDbContext _dbContext;

        public RfqService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Rfq>> GetUnassignedRfqsAsync()
        {
            return await _dbContext.Rfqs
                .AsNoTracking()
                .Include(r => r.AssignedUser)
                .Where(r => string.IsNullOrEmpty(r.AssignedUserId))
                .OrderByDescending(r => r.ReceivedAt)
                .ToListAsync();
        }

        public async Task<List<Rfq>> GetMyRfqsAsync(string userId)
        {
            return await _dbContext.Rfqs
                .AsNoTracking()
                .Include(r => r.AssignedUser)
                .Where(r => r.AssignedUserId == userId)
                .OrderByDescending(r => r.ReceivedAt)
                .ToListAsync();
        }

        public async Task<List<Rfq>> GetAllRfqsAsync()
        {
            return await _dbContext.Rfqs
                .AsNoTracking()
                .Include(r => r.AssignedUser)
                .OrderByDescending(r => r.ReceivedAt)
                .ToListAsync();
        }

        public async Task AssignRfqAsync(Guid rfqId, string userId)
        {
            var rfq = await _dbContext.Rfqs.FindAsync(rfqId);
            if (rfq == null) return;

            rfq.AssignedUserId = userId;
            rfq.Status = RfqStatus.Assigned;

            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                Action = "Lead Assigned",
                Details = $"Incoming RFQ '{rfq.Subject}' was assigned.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }
    }
}

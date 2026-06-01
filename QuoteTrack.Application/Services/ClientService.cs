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
    public class ClientService : IClientService
    {
        private readonly IAppDbContext _dbContext;

        public ClientService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Client>> GetAllClientsAsync()
        {
            return await _dbContext.Clients
                .AsNoTracking()
                .Include(c => c.SubmittedBy)
                .Include(c => c.ApprovedBy)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Client>> GetVerifiedClientsAsync()
        {
            return await _dbContext.Clients
                .AsNoTracking()
                .Where(c => c.ApprovalStatus == ClientApprovalStatus.Approved)
                .OrderBy(c => c.CompanyName)
                .ToListAsync();
        }

        public async Task AddClientAsync(Client client)
        {
            _dbContext.Clients.Add(client);

            if (!string.IsNullOrWhiteSpace(client.SubmittedByUserId))
            {
                _dbContext.ActivityLogs.Add(new ActivityLog
                {
                    UserId = client.SubmittedByUserId!,
                    Action = "Client Added",
                    Details = $"Added client '{client.CompanyName}'",
                    Timestamp = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateClientAsync(Client client, string? actorUserId = null, string? action = null, string? details = null)
        {
            var existing = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == client.Id);
            if (existing == null)
                throw new Exception("Client not found.");

            existing.CompanyName = client.CompanyName;
            existing.Industry = client.Industry;
            existing.PrimaryContactName = client.PrimaryContactName;
            existing.Email = client.Email;
            existing.Phone = client.Phone;
            existing.CommercialRegistrationNumber = client.CommercialRegistrationNumber;
            existing.TaxRegistrationNumber = client.TaxRegistrationNumber;
            existing.BillingAddress = client.BillingAddress;

            existing.ApprovalStatus = client.ApprovalStatus;
            existing.SubmittedByUserId = client.SubmittedByUserId;
            existing.ApprovedByUserId = client.ApprovedByUserId;
            existing.UpdatedAt = DateTime.UtcNow;

            try
            {
                if (!string.IsNullOrWhiteSpace(actorUserId))
                {
                    _dbContext.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = actorUserId!,
                        Action = string.IsNullOrWhiteSpace(action) ? "Client Updated" : action,
                        Details = string.IsNullOrWhiteSpace(details)
                            ? $"Updated client '{existing.CompanyName}'"
                            : details,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch
            {
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IClientService
    {
        Task<List<Client>> GetAllClientsAsync();
        Task<List<Client>> GetVerifiedClientsAsync();
        Task AddClientAsync(Client client);
        Task UpdateClientAsync(Client client, string? actorUserId = null, string? action = null, string? details = null);
    }
}
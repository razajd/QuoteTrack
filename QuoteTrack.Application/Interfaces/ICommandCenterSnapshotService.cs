using System.Threading.Tasks;
using QuoteTrack.Application.DTOs;

namespace QuoteTrack.Application.Interfaces
{
    public interface ICommandCenterSnapshotService
    {
        string BuildScopeKey(string? userId, bool isAdmin, string? ownerId);
        Task<CommandCenterSnapshotDto> GetSnapshotAsync(string? userId, bool isAdmin, string? ownerId);
        Task RefreshSnapshotAsync(string? userId, bool isAdmin, string? ownerId);
        Task MarkAllSnapshotsStaleAsync();
    }
}

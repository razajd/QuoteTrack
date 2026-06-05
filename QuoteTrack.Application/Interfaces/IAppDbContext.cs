using Microsoft.EntityFrameworkCore;
using QuoteTrack.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace QuoteTrack.Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<Quote> Quotes { get; }
        DbSet<Rfq> Rfqs { get; }
        DbSet<Attachment> Attachments { get; }
        DbSet<FollowUp> FollowUps { get; }
        DbSet<Client> Clients { get; }
        DbSet<ActivityLog> ActivityLogs { get; }
        DbSet<QuoteEvent> QuoteEvents { get; }
        DbSet<CommandCenterSnapshot> CommandCenterSnapshots { get; }
        DbSet<CommandCenterQueueItem> CommandCenterQueueItems { get; }
        DbSet<CommandCenterActivityItem> CommandCenterActivityItems { get; }
        DbSet<CommandCenterRadarItem> CommandCenterRadarItems { get; }
        DbSet<QuoteListItem> QuoteListItems { get; }
        DbSet<ReadModelState> ReadModelStates { get; }

        // NEW
        DbSet<MergeRequest> MergeRequests { get; }

        DbSet<ApplicationUser> Users { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

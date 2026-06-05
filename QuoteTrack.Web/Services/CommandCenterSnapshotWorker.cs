using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteTrack.Application.Interfaces;

namespace QuoteTrack.Web.Services
{
    public class CommandCenterSnapshotWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CommandCenterSnapshotWorker> _logger;

        public CommandCenterSnapshotWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<CommandCenterSnapshotWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshSnapshotsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Command Center snapshot refresh failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task RefreshSnapshotsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var snapshotService = scope.ServiceProvider.GetRequiredService<ICommandCenterSnapshotService>();

            var users = await db.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new { u.Id })
                .ToListAsync(stoppingToken);

            foreach (var user in users)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await snapshotService.RefreshSnapshotAsync(user.Id, false, null);

                stoppingToken.ThrowIfCancellationRequested();
                await snapshotService.RefreshSnapshotAsync(user.Id, true, user.Id);
            }

            var shouldRefreshAdminAll = await db.CommandCenterSnapshots
                .AsNoTracking()
                .AnyAsync(s => s.ScopeKey == "admin:all" && s.IsStale, stoppingToken);

            if (shouldRefreshAdminAll)
                await snapshotService.RefreshSnapshotAsync(null, true, null);
        }
    }
}

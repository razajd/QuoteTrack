using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Application.DTOs;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Application.Services
{
    public class ManagementDashboardService : IManagementDashboardService
    {
        private readonly IAppDbContext _dbContext;

        public ManagementDashboardService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ManagementDashboardDto> GetOverviewAsync()
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);

            var closedStatuses = new[]
            {
                QuoteStatus.Won,
                QuoteStatus.Lost,
                QuoteStatus.Cancelled,
                QuoteStatus.LeadClosed,
                QuoteStatus.Merged
            };

            var quotes = _dbContext.Quotes.AsNoTracking();

            var summary = await quotes
                .GroupBy(q => 1)
                .Select(g => new
                {
                    TotalWon = g.Count(q => q.RecordType == QuoteRecordType.OutgoingQuote && q.Status == QuoteStatus.Won),
                    TotalLost = g.Count(q => q.RecordType == QuoteRecordType.OutgoingQuote && q.Status == QuoteStatus.Lost),
                    TotalPipelineValue = g.Sum(q =>
                        !q.IsDeleteRequested &&
                        q.RecordType == QuoteRecordType.OutgoingQuote &&
                        !closedStatuses.Contains(q.Status)
                            ? q.QuoteValue ?? 0m
                            : 0m),
                    UnassignedCount = g.Count(q =>
                        !q.IsDeleteRequested &&
                        q.RecordType == QuoteRecordType.Lead &&
                        !closedStatuses.Contains(q.Status) &&
                        string.IsNullOrWhiteSpace(q.OwnerId)),
                    LeadDueCount = g.Count(q =>
                        !q.IsDeleteRequested &&
                        q.RecordType == QuoteRecordType.Lead &&
                        !closedStatuses.Contains(q.Status) &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value <= now),
                    LeadOverdueCount = g.Count(q =>
                        !q.IsDeleteRequested &&
                        q.RecordType == QuoteRecordType.Lead &&
                        !closedStatuses.Contains(q.Status) &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value < now),
                    QuoteDueCount = g.Count(q =>
                        !q.IsDeleteRequested &&
                        q.RecordType == QuoteRecordType.OutgoingQuote &&
                        !closedStatuses.Contains(q.Status) &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value <= now),
                    QuoteOverdueCount = g.Count(q =>
                        !q.IsDeleteRequested &&
                        q.RecordType == QuoteRecordType.OutgoingQuote &&
                        !closedStatuses.Contains(q.Status) &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value < now),
                    OverdueTasksCount = g.Count(q =>
                        !q.IsDeleteRequested &&
                        !closedStatuses.Contains(q.Status) &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value < now),
                    PendingLeadClosureCount = g.Count(q =>
                        q.RecordType == QuoteRecordType.Lead &&
                        !q.IsDeleteRequested &&
                        q.Status == QuoteStatus.LeadRepCompleted)
                })
                .FirstOrDefaultAsync();

            var result = new ManagementDashboardDto();

            if (summary != null)
            {
                result.TotalPipelineValue = summary.TotalPipelineValue;
                result.UnassignedCount = summary.UnassignedCount;
                result.LeadDueCount = summary.LeadDueCount;
                result.LeadOverdueCount = summary.LeadOverdueCount;
                result.QuoteDueCount = summary.QuoteDueCount;
                result.QuoteOverdueCount = summary.QuoteOverdueCount;
                result.OverdueTasksCount = summary.OverdueTasksCount;
                result.PendingLeadClosureCount = summary.PendingLeadClosureCount;
            }

            var decided = (summary?.TotalWon ?? 0) + (summary?.TotalLost ?? 0);
            var totalWon = summary?.TotalWon ?? 0;
            result.DepartmentWinRate = decided > 0 ? ((double)totalWon / decided) * 100 : 0;

            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync();

            var groupedStats = await quotes
                .Where(q => !string.IsNullOrWhiteSpace(q.OwnerId))
                .Where(q => !q.IsDeleteRequested)
                .GroupBy(q => q.OwnerId!)
                .Select(g => new
                {
                    UserId = g.Key,
                    ActiveDeals = g.Count(q => !closedStatuses.Contains(q.Status) && q.Status != QuoteStatus.LeadRepCompleted),
                    WonDeals = g.Count(q => q.RecordType == QuoteRecordType.OutgoingQuote && q.Status == QuoteStatus.Won),
                    LostDeals = g.Count(q => q.RecordType == QuoteRecordType.OutgoingQuote && q.Status == QuoteStatus.Lost),
                    PipelineValue = g.Sum(q =>
                        q.RecordType == QuoteRecordType.OutgoingQuote &&
                        !closedStatuses.Contains(q.Status)
                            ? q.QuoteValue ?? 0m
                            : 0m),
                    OverdueTasks = g.Count(q =>
                        !closedStatuses.Contains(q.Status) &&
                        q.Status != QuoteStatus.LeadRepCompleted &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value < now),
                    LeadsDue = g.Count(q =>
                        q.RecordType == QuoteRecordType.Lead &&
                        !closedStatuses.Contains(q.Status) &&
                        q.Status != QuoteStatus.LeadRepCompleted &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value <= now),
                    QuotesDue = g.Count(q =>
                        q.RecordType == QuoteRecordType.OutgoingQuote &&
                        !closedStatuses.Contains(q.Status) &&
                        q.NextFollowUpDate.HasValue &&
                        q.NextFollowUpDate.Value <= now),
                    StaleDeals = g.Count(q =>
                        !closedStatuses.Contains(q.Status) &&
                        q.Status != QuoteStatus.LeadRepCompleted &&
                        q.CreatedAt < thirtyDaysAgo)
                })
                .ToListAsync();

            var statsByUser = groupedStats.ToDictionary(x => x.UserId, x => x);

            result.TeamStats = users.Select(user =>
            {
                statsByUser.TryGetValue(user.Id, out var stat);
                var won = stat?.WonDeals ?? 0;
                var lost = stat?.LostDeals ?? 0;
                var userDecided = won + lost;

                return new ManagementRepStatDto
                {
                    UserId = user.Id,
                    RepName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Unknown User" : user.FullName,
                    ActiveDeals = stat?.ActiveDeals ?? 0,
                    WonDeals = won,
                    WinRate = userDecided > 0 ? ((double)won / userDecided) * 100 : 0,
                    PipelineValue = stat?.PipelineValue ?? 0m,
                    OverdueTasks = stat?.OverdueTasks ?? 0,
                    LeadsDue = stat?.LeadsDue ?? 0,
                    QuotesDue = stat?.QuotesDue ?? 0,
                    StaleDeals = stat?.StaleDeals ?? 0
                };
            }).ToList();

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Application.DTOs;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Application.Services
{
    public class CommandCenterSnapshotService : ICommandCenterSnapshotService
    {
        private const int MaxQueueItemsPerSnapshot = 250;
        private const int MaxActivityItemsPerSnapshot = 80;

        private readonly IAppDbContext _dbContext;

        public CommandCenterSnapshotService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public string BuildScopeKey(string? userId, bool isAdmin, string? ownerId)
        {
            if (!isAdmin)
                return string.IsNullOrWhiteSpace(userId) ? "user:none" : $"user:{userId}";

            if (!string.IsNullOrWhiteSpace(ownerId))
                return $"owner:{ownerId}";

            return "admin:all";
        }

        public async Task<CommandCenterSnapshotDto> GetSnapshotAsync(string? userId, bool isAdmin, string? ownerId)
        {
            var scopeKey = BuildScopeKey(userId, isAdmin, ownerId);

            var snapshot = await _dbContext.CommandCenterSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ScopeKey == scopeKey);

            if (snapshot == null)
            {
                return new CommandCenterSnapshotDto
                {
                    ScopeKey = scopeKey,
                    LastRefreshedAt = DateTime.MinValue,
                    IsStale = true,
                    IsRefreshing = false,
                    LastError = "Snapshot is being prepared. Refresh will appear shortly."
                };
            }

            var queueItems = await _dbContext.CommandCenterQueueItems
                .AsNoTracking()
                .Where(i => i.ScopeKey == scopeKey)
                .OrderBy(i => i.SortRank)
                .Take(MaxQueueItemsPerSnapshot)
                .Select(i => new CommandCenterQueueItemDto
                {
                    QuoteId = i.QuoteId,
                    RecordType = i.RecordType,
                    RecordLabel = i.RecordLabel,
                    StatusLabel = i.StatusLabel,
                    OwnerId = i.OwnerId,
                    OwnerLabel = i.OwnerLabel,
                    ClientLabel = i.ClientLabel,
                    Subject = i.Subject,
                    DueUtc = i.DueUtc,
                    DueLocalText = i.DueLocalText,
                    Value = i.Value,
                    ValueText = i.ValueText,
                    CreatedAtUtc = i.CreatedAtUtc,
                    LastNotePreview = i.LastNotePreview,
                    SearchText = i.SearchText
                })
                .ToListAsync();

            var activities = await _dbContext.CommandCenterActivityItems
                .AsNoTracking()
                .Where(i => i.ScopeKey == scopeKey)
                .OrderBy(i => i.SortRank)
                .Take(MaxActivityItemsPerSnapshot)
                .Select(i => new CommandCenterActivityItemDto
                {
                    WhenUtc = i.WhenUtc,
                    WhenLocal = i.WhenLocal,
                    Title = i.Title,
                    Details = i.Details,
                    Link = i.Link
                })
                .ToListAsync();

            var radarRows = await _dbContext.CommandCenterRadarItems
                .AsNoTracking()
                .Where(i => i.ScopeKey == scopeKey)
                .OrderBy(i => i.SortRank)
                .Select(i => new { i.RadarType, Row = i })
                .ToListAsync();

            var dto = MapSnapshot(snapshot);
            dto.QueueItems = queueItems;
            dto.ActivityItems = activities;
            dto.StatusRadarRows = radarRows
                .Where(x => x.RadarType == "Status")
                .Select(x => MapRadar(x.Row))
                .ToList();
            dto.AgingRadarRows = radarRows
                .Where(x => x.RadarType == "Aging")
                .Select(x => MapRadar(x.Row))
                .ToList();

            return dto;
        }

        public async Task RefreshSnapshotAsync(string? userId, bool isAdmin, string? ownerId)
        {
            var scopeKey = BuildScopeKey(userId, isAdmin, ownerId);
            var now = DateTime.UtcNow;

            var snapshot = await _dbContext.CommandCenterSnapshots
                .FirstOrDefaultAsync(s => s.ScopeKey == scopeKey);

            if (snapshot == null)
            {
                snapshot = new CommandCenterSnapshot
                {
                    ScopeKey = scopeKey,
                    UserId = isAdmin ? null : userId,
                    OwnerId = isAdmin ? ownerId : userId,
                    IsAdminScope = isAdmin
                };
                _dbContext.CommandCenterSnapshots.Add(snapshot);
            }

            snapshot.IsRefreshing = true;
            snapshot.RefreshStartedAt = now;
            snapshot.LastError = null;
            await _dbContext.SaveChangesAsync();

            try
            {
                var query = _dbContext.Quotes
                    .AsNoTracking()
                    .Include(q => q.Owner)
                    .Include(q => q.Client)
                    .Include(q => q.FollowUps.OrderByDescending(f => f.CreatedAt).Take(5))
                    .AsQueryable();

                if (isAdmin)
                {
                    if (!string.IsNullOrWhiteSpace(ownerId))
                        query = query.Where(q => q.OwnerId == ownerId);
                }
                else if (!string.IsNullOrWhiteSpace(userId))
                {
                    query = query.Where(q => q.OwnerId == userId);
                }
                else
                {
                    query = query.Where(q => false);
                }

                var quotes = await query
                    .OrderByDescending(q => q.CreatedAt)
                    .ToListAsync();

                var activeQuotes = quotes
                    .Where(q => q.RecordType != QuoteRecordType.Lead)
                    .Where(q => !IsClosedStatus(q.Status))
                    .ToList();

                var todayLocal = DateTime.Now.Date;
                var todayUtcStart = DateTime.SpecifyKind(todayLocal, DateTimeKind.Local).ToUniversalTime();
                var todayUtcEnd = DateTime.SpecifyKind(todayLocal.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                var monthStartLocal = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var monthStartUtc = DateTime.SpecifyKind(monthStartLocal, DateTimeKind.Local).ToUniversalTime();
                var sevenDaysAgoUtc = now.AddDays(-7);

                var actionable = quotes.Where(q => !IsClosedStatus(q.Status)).ToList();
                var overdue = actionable
                    .Where(q => q.NextFollowUpDate is DateTime nextDate && nextDate < todayUtcStart)
                    .ToList();
                var dueToday = actionable
                    .Where(q => q.NextFollowUpDate is DateTime nextDate && nextDate >= todayUtcStart && nextDate < todayUtcEnd)
                    .ToList();
                var wonThisMonth = quotes
                    .Where(q => q.RecordType != QuoteRecordType.Lead)
                    .Where(q => q.Status == QuoteStatus.Won)
                    .Where(q => (q.WonAt ?? q.UpdatedAt) >= monthStartUtc)
                    .ToList();
                var leads7d = quotes
                    .Where(q => q.RecordType == QuoteRecordType.Lead)
                    .Where(q => q.CreatedAt >= sevenDaysAgoUtc)
                    .ToList();

                snapshot.PipelineValue = activeQuotes.Sum(q => q.QuoteValue ?? 0m);
                snapshot.ActiveQuotesCount = activeQuotes.Count;
                snapshot.WonThisMonth = wonThisMonth.Sum(q => q.QuoteValue ?? 0m);
                snapshot.WonCountThisMonth = wonThisMonth.Count;
                snapshot.OverdueCount = overdue.Count;
                snapshot.OverdueValue = overdue.Where(q => q.RecordType != QuoteRecordType.Lead).Sum(q => q.QuoteValue ?? 0m);
                snapshot.DueTodayCount = dueToday.Count;
                snapshot.DueTodayValue = dueToday.Where(q => q.RecordType != QuoteRecordType.Lead).Sum(q => q.QuoteValue ?? 0m);
                snapshot.NewLeads7d = leads7d.Count;
                snapshot.UnassignedLeads = leads7d.Count(q => string.IsNullOrWhiteSpace(q.OwnerId));
                snapshot.UnassignedCount = actionable.Count(q => string.IsNullOrWhiteSpace(q.OwnerId));
                snapshot.HighValueCount = activeQuotes.Count(q => (q.QuoteValue ?? 0m) >= 2000m);
                snapshot.MissingFollowUpCount = actionable.Count(q => !q.NextFollowUpDate.HasValue);
                snapshot.ValueTbdCount = activeQuotes.Count(q => !q.QuoteValue.HasValue || q.QuoteValue.Value <= 0);
                snapshot.MissingClientLinkCount = activeQuotes.Count(q => !q.ClientId.HasValue);

                var queueRows = BuildActionQueue(scopeKey, actionable, todayUtcStart, todayUtcEnd).ToList();
                var activityRows = BuildRecentActivity(scopeKey, quotes).ToList();
                var radarRows = BuildRadar(scopeKey, activeQuotes, now).ToList();

                var existingQueue = await _dbContext.CommandCenterQueueItems
                    .Where(i => i.ScopeKey == scopeKey)
                    .ToListAsync();
                _dbContext.CommandCenterQueueItems.RemoveRange(existingQueue);

                var existingActivity = await _dbContext.CommandCenterActivityItems
                    .Where(i => i.ScopeKey == scopeKey)
                    .ToListAsync();
                _dbContext.CommandCenterActivityItems.RemoveRange(existingActivity);

                var existingRadar = await _dbContext.CommandCenterRadarItems
                    .Where(i => i.ScopeKey == scopeKey)
                    .ToListAsync();
                _dbContext.CommandCenterRadarItems.RemoveRange(existingRadar);

                _dbContext.CommandCenterQueueItems.AddRange(queueRows);
                _dbContext.CommandCenterActivityItems.AddRange(activityRows);
                _dbContext.CommandCenterRadarItems.AddRange(radarRows);

                snapshot.LastRefreshedAt = DateTime.UtcNow;
                snapshot.IsRefreshing = false;
                snapshot.IsStale = false;
                snapshot.LastError = null;
            }
            catch (Exception ex)
            {
                snapshot.IsRefreshing = false;
                snapshot.IsStale = true;
                snapshot.LastError = ex.Message;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task MarkAllSnapshotsStaleAsync()
        {
            var snapshots = await _dbContext.CommandCenterSnapshots.ToListAsync();
            foreach (var snapshot in snapshots)
                snapshot.IsStale = true;

            await _dbContext.SaveChangesAsync();
        }

        private static IEnumerable<CommandCenterQueueItem> BuildActionQueue(
            string scopeKey,
            List<Quote> actionable,
            DateTime todayUtcStart,
            DateTime todayUtcEnd)
        {
            return actionable
                .Select(MapQueueItem)
                .OrderByDescending(i => i.DueUtc.HasValue && i.DueUtc.Value < todayUtcStart)
                .ThenByDescending(i => i.DueUtc.HasValue && i.DueUtc.Value >= todayUtcStart && i.DueUtc.Value < todayUtcEnd)
                .ThenByDescending(i => (i.Value ?? 0m) >= 2000m)
                .ThenByDescending(i => i.CreatedAtUtc)
                .Take(MaxQueueItemsPerSnapshot)
                .Select((i, index) =>
                {
                    i.ScopeKey = scopeKey;
                    i.SortRank = index;
                    return i;
                });
        }

        private static CommandCenterQueueItem MapQueueItem(Quote q)
        {
            var recordLabel = q.RecordType == QuoteRecordType.Lead ? "Lead" : "Quote";
            var ownerLabel = q.Owner?.FullName ?? (string.IsNullOrWhiteSpace(q.OwnerId) ? "Unassigned" : "Assigned");
            var clientLabel = q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail ?? "Unknown";
            var valueText = q.RecordType == QuoteRecordType.Lead
                ? "-"
                : (q.QuoteValue.HasValue && q.QuoteValue.Value > 0 ? $"{q.Currency} {q.QuoteValue.Value:N2}" : "TBD");
            var dueText = q.NextFollowUpDate.HasValue
                ? q.NextFollowUpDate.Value.ToLocalTime().ToString("dd MMM yyyy")
                : "Not set";
            var lastNote = "-";

            if (q.FollowUps != null && q.FollowUps.Count > 0)
            {
                lastNote = (q.FollowUps.OrderByDescending(x => x.CreatedAt).First().Notes ?? "").Trim();
                if (lastNote.Length > 60)
                    lastNote = lastNote.Substring(0, 60) + "...";
            }

            return new CommandCenterQueueItem
            {
                Id = Guid.NewGuid(),
                QuoteId = q.Id,
                RecordType = q.RecordType,
                RecordLabel = recordLabel,
                StatusLabel = q.Status.ToString(),
                OwnerId = q.OwnerId ?? "",
                OwnerLabel = ownerLabel,
                ClientLabel = clientLabel,
                Subject = q.Subject ?? "",
                DueUtc = q.NextFollowUpDate,
                DueLocalText = dueText,
                Value = q.QuoteValue,
                ValueText = valueText,
                CreatedAtUtc = q.CreatedAt,
                LastNotePreview = lastNote,
                SearchText = $"{clientLabel} {q.QuoteReference} {q.Subject} {q.SenderEmail}".ToLowerInvariant()
            };
        }

        private static IEnumerable<CommandCenterActivityItem> BuildRecentActivity(string scopeKey, List<Quote> quotes)
        {
            var events = new List<CommandCenterActivityItem>();

            foreach (var q in quotes)
            {
                if (q.FollowUps == null || q.FollowUps.Count == 0) continue;

                foreach (var f in q.FollowUps)
                {
                    var link = q.RecordType == QuoteRecordType.Lead ? "/lead/" + q.Id : "/quote/" + q.Id;
                    var title = (q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail ?? "Record") +
                                " - " +
                                (q.RecordType == QuoteRecordType.Lead ? "Lead" : "Quote");

                    events.Add(new CommandCenterActivityItem
                    {
                        Id = Guid.NewGuid(),
                        ScopeKey = scopeKey,
                        QuoteId = q.Id,
                        WhenUtc = f.CreatedAt,
                        WhenLocal = f.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt"),
                        Title = title,
                        Details = f.Notes ?? "",
                        Link = link
                    });
                }
            }

            return events
                .OrderByDescending(e => e.WhenUtc)
                .Take(MaxActivityItemsPerSnapshot)
                .Select((e, index) =>
                {
                    e.SortRank = index;
                    return e;
                });
        }

        private static IEnumerable<CommandCenterRadarItem> BuildRadar(string scopeKey, List<Quote> openQuotes, DateTime nowUtc)
        {
            var rows = new List<CommandCenterRadarItem>();
            decimal total = openQuotes.Sum(x => x.QuoteValue ?? 0m);
            if (total <= 0) total = 1;

            rows.AddRange(openQuotes
                .GroupBy(x => x.Status.ToString())
                .Select(g =>
                {
                    var value = g.Sum(x => x.QuoteValue ?? 0m);
                    return new CommandCenterRadarItem
                    {
                        Id = Guid.NewGuid(),
                        ScopeKey = scopeKey,
                        RadarType = "Status",
                        Label = g.Key,
                        Count = g.Count(),
                        Percent = Math.Round((double)(value / total) * 100.0, 0),
                        ValueText = "BHD " + value.ToString("N0")
                    };
                })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .Select((r, index) =>
                {
                    r.SortRank = index;
                    return r;
                }));

            var buckets = new[]
            {
                new { Label = "0-3 days", Min = 0, Max = 3 },
                new { Label = "4-7 days", Min = 4, Max = 7 },
                new { Label = "8-14 days", Min = 8, Max = 14 },
                new { Label = "15-30 days", Min = 15, Max = 30 },
                new { Label = "30+ days", Min = 31, Max = 9999 }
            };

            rows.AddRange(buckets.Select((b, index) =>
            {
                var inBucket = openQuotes.Where(q =>
                {
                    var age = (nowUtc - q.CreatedAt).TotalDays;
                    return age >= b.Min && age <= b.Max;
                }).ToList();

                var value = inBucket.Sum(x => x.QuoteValue ?? 0m);

                return new CommandCenterRadarItem
                {
                    Id = Guid.NewGuid(),
                    ScopeKey = scopeKey,
                    RadarType = "Aging",
                    Label = b.Label,
                    Count = inBucket.Count,
                    Percent = Math.Round((double)(value / total) * 100.0, 0),
                    ValueText = "BHD " + value.ToString("N0"),
                    SortRank = index
                };
            }));

            return rows;
        }

        private static bool IsClosedStatus(QuoteStatus status)
        {
            return status == QuoteStatus.Won ||
                   status == QuoteStatus.Lost ||
                   status == QuoteStatus.Cancelled ||
                   status == QuoteStatus.LeadClosed ||
                   status == QuoteStatus.Merged;
        }

        private static CommandCenterSnapshotDto MapSnapshot(CommandCenterSnapshot snapshot)
        {
            return new CommandCenterSnapshotDto
            {
                ScopeKey = snapshot.ScopeKey,
                LastRefreshedAt = snapshot.LastRefreshedAt,
                IsRefreshing = snapshot.IsRefreshing,
                IsStale = snapshot.IsStale,
                LastError = snapshot.LastError,
                PipelineValue = snapshot.PipelineValue,
                ActiveQuotesCount = snapshot.ActiveQuotesCount,
                WonThisMonth = snapshot.WonThisMonth,
                WonCountThisMonth = snapshot.WonCountThisMonth,
                OverdueCount = snapshot.OverdueCount,
                OverdueValue = snapshot.OverdueValue,
                DueTodayCount = snapshot.DueTodayCount,
                DueTodayValue = snapshot.DueTodayValue,
                NewLeads7d = snapshot.NewLeads7d,
                UnassignedLeads = snapshot.UnassignedLeads,
                UnassignedCount = snapshot.UnassignedCount,
                HighValueCount = snapshot.HighValueCount,
                MissingFollowUpCount = snapshot.MissingFollowUpCount,
                ValueTbdCount = snapshot.ValueTbdCount,
                MissingClientLinkCount = snapshot.MissingClientLinkCount
            };
        }

        private static CommandCenterRadarItemDto MapRadar(CommandCenterRadarItem item)
        {
            return new CommandCenterRadarItemDto
            {
                Label = item.Label,
                Count = item.Count,
                Percent = item.Percent,
                ValueText = item.ValueText
            };
        }
    }
}

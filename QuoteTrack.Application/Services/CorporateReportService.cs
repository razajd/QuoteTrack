using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Application.DTOs;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Application.Services
{
    public class CorporateReportService : ICorporateReportService
    {
        private readonly IAppDbContext _dbContext;

        public CorporateReportService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CorporateReportDto> GenerateAsync(
            string reportCode,
            string? userId,
            DateTime dateFrom,
            DateTime dateTo,
            string grouping)
        {
            var from = DateTime.SpecifyKind(dateFrom.Date, DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(dateTo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            if (to < from)
                (from, to) = (to.Date, from.Date.AddDays(1).AddTicks(-1));

            var normalizedCode = NormalizeReportCode(reportCode);
            var normalizedGrouping = string.Equals(grouping, "Monthly", StringComparison.OrdinalIgnoreCase)
                ? "Monthly"
                : "Weekly";

            return normalizedCode switch
            {
                "FollowUps" => await BuildFollowUpReportAsync(userId, from, to, normalizedGrouping),
                "Pipeline" => await BuildPipelineReportAsync(userId, from, to, normalizedGrouping),
                "Sla" => await BuildSlaReportAsync(userId, from, to, normalizedGrouping),
                "DataQuality" => await BuildDataQualityReportAsync(userId, from, to),
                "ElvOperations" => await BuildElvOperationsReportAsync(userId, from, to, normalizedGrouping),
                _ => await BuildUserActivityReportAsync(userId, from, to, normalizedGrouping)
            };
        }

        private async Task<CorporateReportDto> BuildUserActivityReportAsync(
            string? userId,
            DateTime from,
            DateTime to,
            string grouping)
        {
            var users = await GetUserNamesAsync();
            var logsQuery = _dbContext.ActivityLogs.AsNoTracking()
                .Where(a => a.Timestamp >= from && a.Timestamp <= to);
            var eventsQuery = _dbContext.QuoteEvents.AsNoTracking()
                .Where(e => e.OccurredAt >= from && e.OccurredAt <= to);
            var followUpsQuery = _dbContext.FollowUps.AsNoTracking()
                .Where(f => f.CreatedAt >= from && f.CreatedAt <= to);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                logsQuery = logsQuery.Where(a => a.UserId == userId);
                eventsQuery = eventsQuery.Where(e => e.ActorUserId == userId);
                followUpsQuery = followUpsQuery.Where(f => f.CreatedByUserId == userId);
            }

            var logs = await logsQuery.ToListAsync();
            var events = await eventsQuery.ToListAsync();
            var followUps = await followUpsQuery.ToListAsync();
            var ids = logs.Select(x => x.UserId)
                .Concat(events.Where(x => !string.IsNullOrWhiteSpace(x.ActorUserId)).Select(x => x.ActorUserId!))
                .Concat(followUps.Where(x => !string.IsNullOrWhiteSpace(x.CreatedByUserId)).Select(x => x.CreatedByUserId!))
                .Distinct()
                .ToList();

            var report = CreateReport(
                "UserActivity",
                "User Activity Log",
                "Logged actions, structured workflow changes, follow-ups, and records touched by each user.",
                from,
                to);

            report.Columns = new()
            {
                "User", "Logged Actions", "Updates Posted", "Structured Changes",
                "Follow-ups Added", "Records Touched", "Last Activity"
            };

            foreach (var id in ids.OrderBy(id => UserName(users, id)))
            {
                var userLogs = logs.Where(x => x.UserId == id).ToList();
                var userEvents = events.Where(x => x.ActorUserId == id).ToList();
                var userFollowUps = followUps.Where(x => x.CreatedByUserId == id).ToList();
                var lastActivity = userLogs.Select(x => x.Timestamp)
                    .Concat(userEvents.Select(x => x.OccurredAt))
                    .Concat(userFollowUps.Select(x => x.CreatedAt))
                    .DefaultIfEmpty()
                    .Max();

                report.Rows.Add(Row(
                    ("User", UserName(users, id)),
                    ("Logged Actions", userLogs.Count.ToString("N0")),
                    ("Updates Posted", userLogs.Count(IsUpdateAction).ToString("N0")),
                    ("Structured Changes", userEvents.Count.ToString("N0")),
                    ("Follow-ups Added", userFollowUps.Count.ToString("N0")),
                    ("Records Touched", userLogs.Where(x => x.RelatedQuoteId.HasValue).Select(x => x.RelatedQuoteId).Concat(userEvents.Select(x => (Guid?)x.QuoteId)).Distinct().Count().ToString("N0")),
                    ("Last Activity", lastActivity == default ? "-" : lastActivity.ToLocalTime().ToString("dd MMM yyyy HH:mm"))));
            }

            report.Cards = new()
            {
                Card("Logged Actions", logs.Count, "primary"),
                Card("Updates Posted", logs.Count(IsUpdateAction), "info"),
                Card("Structured Changes", events.Count, "warning"),
                Card("Follow-ups Added", followUps.Count, "success")
            };

            report.TrendColumns = new() { "Period", "Logged Actions", "Structured Changes", "Follow-ups" };
            var buckets = logs.Select(x => BucketStart(x.Timestamp, grouping))
                .Concat(events.Select(x => BucketStart(x.OccurredAt, grouping)))
                .Concat(followUps.Select(x => BucketStart(x.CreatedAt, grouping)))
                .Distinct()
                .OrderBy(x => x);

            foreach (var bucket in buckets)
            {
                report.TrendRows.Add(Row(
                    ("Period", BucketLabel(bucket, grouping)),
                    ("Logged Actions", logs.Count(x => BucketStart(x.Timestamp, grouping) == bucket).ToString("N0")),
                    ("Structured Changes", events.Count(x => BucketStart(x.OccurredAt, grouping) == bucket).ToString("N0")),
                    ("Follow-ups", followUps.Count(x => BucketStart(x.CreatedAt, grouping) == bucket).ToString("N0"))));
            }

            return report;
        }

        private async Task<CorporateReportDto> BuildFollowUpReportAsync(
            string? userId,
            DateTime from,
            DateTime to,
            string grouping)
        {
            var users = await GetUserNamesAsync();
            var query = _dbContext.FollowUps.AsNoTracking()
                .Where(f => f.CreatedAt >= from && f.CreatedAt <= to);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(f => f.CreatedByUserId == userId);

            var followUps = await query.ToListAsync();
            var ids = followUps.Where(x => !string.IsNullOrWhiteSpace(x.CreatedByUserId))
                .Select(x => x.CreatedByUserId!)
                .Distinct()
                .ToList();
            var now = DateTime.UtcNow;

            var report = CreateReport(
                "FollowUps",
                "Follow-up Performance",
                "Follow-ups created, completed, overdue, and completion discipline by user.",
                from,
                to);

            report.Columns = new()
            {
                "User", "Created", "Completed", "Open", "Overdue",
                "Completion Rate", "Avg Completion Days"
            };

            foreach (var id in ids.OrderBy(id => UserName(users, id)))
            {
                var items = followUps.Where(x => x.CreatedByUserId == id).ToList();
                var completed = items.Where(x => x.IsCompleted).ToList();
                var durations = completed
                    .Where(x => x.CompletedDate.HasValue && x.CompletedDate.Value >= x.CreatedAt)
                    .Select(x => (x.CompletedDate!.Value - x.CreatedAt).TotalDays)
                    .ToList();

                report.Rows.Add(Row(
                    ("User", UserName(users, id)),
                    ("Created", items.Count.ToString("N0")),
                    ("Completed", completed.Count.ToString("N0")),
                    ("Open", items.Count(x => !x.IsCompleted).ToString("N0")),
                    ("Overdue", items.Count(x => !x.IsCompleted && x.DueDate < now).ToString("N0")),
                    ("Completion Rate", Percent(completed.Count, items.Count)),
                    ("Avg Completion Days", durations.Count == 0 ? "-" : durations.Average().ToString("N1"))));
            }

            report.Cards = new()
            {
                Card("Follow-ups Created", followUps.Count, "primary"),
                Card("Completed", followUps.Count(x => x.IsCompleted), "success"),
                Card("Open", followUps.Count(x => !x.IsCompleted), "warning"),
                Card("Overdue", followUps.Count(x => !x.IsCompleted && x.DueDate < now), "danger")
            };

            report.TrendColumns = new() { "Period", "Created", "Completed", "Overdue" };
            foreach (var bucket in followUps.Select(x => BucketStart(x.CreatedAt, grouping)).Distinct().OrderBy(x => x))
            {
                var items = followUps.Where(x => BucketStart(x.CreatedAt, grouping) == bucket).ToList();
                report.TrendRows.Add(Row(
                    ("Period", BucketLabel(bucket, grouping)),
                    ("Created", items.Count.ToString("N0")),
                    ("Completed", items.Count(x => x.IsCompleted).ToString("N0")),
                    ("Overdue", items.Count(x => !x.IsCompleted && x.DueDate < now).ToString("N0"))));
            }

            report.Notes.Add("Overdue is evaluated against the current date for follow-ups created in the selected period.");
            return report;
        }

        private async Task<CorporateReportDto> BuildPipelineReportAsync(
            string? userId,
            DateTime from,
            DateTime to,
            string grouping)
        {
            var users = await GetUserNamesAsync();
            var query = _dbContext.Quotes.AsNoTracking().Where(q => !q.IsDeleteRequested);
            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(q => q.OwnerId == userId);

            var quotes = await query.Select(q => new
            {
                q.OwnerId,
                q.RecordType,
                q.Status,
                q.CreatedAt,
                q.WonAt,
                q.LostAt,
                q.UpdatedAt,
                q.QuoteValue
            }).ToListAsync();

            var ids = quotes.Where(x => !string.IsNullOrWhiteSpace(x.OwnerId)).Select(x => x.OwnerId!).Distinct().ToList();
            var report = CreateReport(
                "Pipeline",
                "Pipeline Outcomes",
                "Leads and quotes created, won/lost outcomes, values, and current active pipeline.",
                from,
                to);

            report.Columns = new()
            {
                "User", "Leads Created", "Quotes Created", "Won", "Lost",
                "Won Value", "Win Rate", "Active Pipeline", "Active Value"
            };

            foreach (var id in ids.OrderBy(id => UserName(users, id)))
            {
                var owned = quotes.Where(x => x.OwnerId == id).ToList();
                var created = owned.Where(x => x.CreatedAt >= from && x.CreatedAt <= to).ToList();
                var won = owned.Where(x => OutcomeDate(x.WonAt, x.Status == QuoteStatus.Won, x.UpdatedAt) is DateTime d && d >= from && d <= to).ToList();
                var lost = owned.Where(x => OutcomeDate(x.LostAt, x.Status == QuoteStatus.Lost, x.UpdatedAt) is DateTime d && d >= from && d <= to).ToList();
                var active = owned.Where(x => !IsClosed(x.Status)).ToList();

                report.Rows.Add(Row(
                    ("User", UserName(users, id)),
                    ("Leads Created", created.Count(x => x.RecordType == QuoteRecordType.Lead).ToString("N0")),
                    ("Quotes Created", created.Count(x => x.RecordType == QuoteRecordType.OutgoingQuote).ToString("N0")),
                    ("Won", won.Count.ToString("N0")),
                    ("Lost", lost.Count.ToString("N0")),
                    ("Won Value", Money(won.Sum(x => x.QuoteValue ?? 0m))),
                    ("Win Rate", Percent(won.Count, won.Count + lost.Count)),
                    ("Active Pipeline", active.Count.ToString("N0")),
                    ("Active Value", Money(active.Sum(x => x.QuoteValue ?? 0m)))));
            }

            var periodWon = quotes.Where(x => OutcomeDate(x.WonAt, x.Status == QuoteStatus.Won, x.UpdatedAt) is DateTime d && d >= from && d <= to).ToList();
            var periodLost = quotes.Where(x => OutcomeDate(x.LostAt, x.Status == QuoteStatus.Lost, x.UpdatedAt) is DateTime d && d >= from && d <= to).ToList();
            var periodCreated = quotes.Where(x => x.CreatedAt >= from && x.CreatedAt <= to).ToList();

            report.Cards = new()
            {
                Card("New Leads", periodCreated.Count(x => x.RecordType == QuoteRecordType.Lead), "primary"),
                Card("New Quotes", periodCreated.Count(x => x.RecordType == QuoteRecordType.OutgoingQuote), "info"),
                Card("Won Value", Money(periodWon.Sum(x => x.QuoteValue ?? 0m)), "success"),
                Card("Win Rate", Percent(periodWon.Count, periodWon.Count + periodLost.Count), "warning")
            };

            report.TrendColumns = new() { "Period", "Created", "Won", "Lost", "Won Value" };
            var outcomeDates = periodCreated.Select(x => x.CreatedAt)
                .Concat(periodWon.Select(x => OutcomeDate(x.WonAt, true, x.UpdatedAt)!.Value))
                .Concat(periodLost.Select(x => OutcomeDate(x.LostAt, true, x.UpdatedAt)!.Value));

            foreach (var bucket in outcomeDates.Select(x => BucketStart(x, grouping)).Distinct().OrderBy(x => x))
            {
                var won = periodWon.Where(x => BucketStart(OutcomeDate(x.WonAt, true, x.UpdatedAt)!.Value, grouping) == bucket).ToList();
                report.TrendRows.Add(Row(
                    ("Period", BucketLabel(bucket, grouping)),
                    ("Created", periodCreated.Count(x => BucketStart(x.CreatedAt, grouping) == bucket).ToString("N0")),
                    ("Won", won.Count.ToString("N0")),
                    ("Lost", periodLost.Count(x => BucketStart(OutcomeDate(x.LostAt, true, x.UpdatedAt)!.Value, grouping) == bucket).ToString("N0")),
                    ("Won Value", Money(won.Sum(x => x.QuoteValue ?? 0m)))));
            }

            report.Notes.Add("Active pipeline is a current-state measure; created and outcome metrics use the selected period.");
            return report;
        }

        private async Task<CorporateReportDto> BuildSlaReportAsync(
            string? userId,
            DateTime from,
            DateTime to,
            string grouping)
        {
            var users = await GetUserNamesAsync();
            var query = _dbContext.Quotes.AsNoTracking()
                .Where(q => !q.IsDeleteRequested)
                .Where(q => q.CreatedAt >= from && q.CreatedAt <= to);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(q => q.OwnerId == userId);

            var quotes = await query.Select(q => new
            {
                q.OwnerId,
                q.RecordType,
                q.CreatedAt,
                q.AssignedAt,
                q.FirstContactedAt,
                q.QuotedAt,
                q.EmailSentDateTime
            }).ToListAsync();

            var ids = quotes.Where(x => !string.IsNullOrWhiteSpace(x.OwnerId)).Select(x => x.OwnerId!).Distinct().ToList();
            var report = CreateReport(
                "Sla",
                "Lead & Proposal SLA",
                "Lead response time and proposal turnaround measured from recorded workflow milestones.",
                from,
                to);

            report.Columns = new()
            {
                "User", "Assigned Leads", "Contacted", "Within 24h", "Avg Response Hours",
                "Quoted", "Within 48h", "Avg Quote Hours"
            };

            foreach (var id in ids.OrderBy(id => UserName(users, id)))
            {
                var owned = quotes.Where(x => x.OwnerId == id).ToList();
                var leads = owned.Where(x => x.RecordType == QuoteRecordType.Lead && x.AssignedAt.HasValue).ToList();
                var contacted = leads.Where(x => x.FirstContactedAt.HasValue && x.FirstContactedAt >= x.AssignedAt).ToList();
                var quoteRows = owned.Where(x => x.RecordType == QuoteRecordType.OutgoingQuote && (x.QuotedAt.HasValue || x.EmailSentDateTime != default)).ToList();
                var responseHours = contacted.Select(x => (x.FirstContactedAt!.Value - x.AssignedAt!.Value).TotalHours).ToList();
                var quoteHours = quoteRows.Select(x =>
                {
                    var start = x.AssignedAt ?? x.CreatedAt;
                    var end = x.QuotedAt ?? x.EmailSentDateTime;
                    return (end - start).TotalHours;
                }).Where(x => x >= 0).ToList();

                report.Rows.Add(Row(
                    ("User", UserName(users, id)),
                    ("Assigned Leads", leads.Count.ToString("N0")),
                    ("Contacted", contacted.Count.ToString("N0")),
                    ("Within 24h", Percent(responseHours.Count(x => x <= 24), responseHours.Count)),
                    ("Avg Response Hours", responseHours.Count == 0 ? "-" : responseHours.Average().ToString("N1")),
                    ("Quoted", quoteRows.Count.ToString("N0")),
                    ("Within 48h", Percent(quoteHours.Count(x => x <= 48), quoteHours.Count)),
                    ("Avg Quote Hours", quoteHours.Count == 0 ? "-" : quoteHours.Average().ToString("N1"))));
            }

            var allResponseHours = quotes
                .Where(x => x.AssignedAt.HasValue && x.FirstContactedAt.HasValue && x.FirstContactedAt >= x.AssignedAt)
                .Select(x => (x.FirstContactedAt!.Value - x.AssignedAt!.Value).TotalHours)
                .ToList();
            var allQuoteHours = quotes
                .Where(x => x.RecordType == QuoteRecordType.OutgoingQuote && (x.QuotedAt.HasValue || x.EmailSentDateTime != default))
                .Select(x => ((x.QuotedAt ?? x.EmailSentDateTime) - (x.AssignedAt ?? x.CreatedAt)).TotalHours)
                .Where(x => x >= 0)
                .ToList();

            report.Cards = new()
            {
                Card("Lead Response ≤24h", Percent(allResponseHours.Count(x => x <= 24), allResponseHours.Count), "success"),
                Card("Avg Response Hours", allResponseHours.Count == 0 ? "-" : allResponseHours.Average().ToString("N1"), "primary"),
                Card("Proposal Turnaround ≤48h", Percent(allQuoteHours.Count(x => x <= 48), allQuoteHours.Count), "success"),
                Card("Avg Proposal Hours", allQuoteHours.Count == 0 ? "-" : allQuoteHours.Average().ToString("N1"), "info")
            };

            report.TrendColumns = new() { "Period", "Assigned Leads", "Contacted ≤24h", "Quoted ≤48h" };
            foreach (var bucket in quotes.Select(x => BucketStart(x.CreatedAt, grouping)).Distinct().OrderBy(x => x))
            {
                var items = quotes.Where(x => BucketStart(x.CreatedAt, grouping) == bucket).ToList();
                var responses = items
                    .Where(x => x.AssignedAt.HasValue && x.FirstContactedAt.HasValue && x.FirstContactedAt >= x.AssignedAt)
                    .Select(x => (x.FirstContactedAt!.Value - x.AssignedAt!.Value).TotalHours)
                    .ToList();
                var proposalHours = items
                    .Where(x => x.RecordType == QuoteRecordType.OutgoingQuote && (x.QuotedAt.HasValue || x.EmailSentDateTime != default))
                    .Select(x => ((x.QuotedAt ?? x.EmailSentDateTime) - (x.AssignedAt ?? x.CreatedAt)).TotalHours)
                    .Where(x => x >= 0)
                    .ToList();

                report.TrendRows.Add(Row(
                    ("Period", BucketLabel(bucket, grouping)),
                    ("Assigned Leads", items.Count(x => x.RecordType == QuoteRecordType.Lead && x.AssignedAt.HasValue).ToString("N0")),
                    ("Contacted ≤24h", Percent(responses.Count(x => x <= 24), responses.Count)),
                    ("Quoted ≤48h", Percent(proposalHours.Count(x => x <= 48), proposalHours.Count))));
            }

            report.Notes.Add("SLA accuracy improves as AssignedAt, FirstContactedAt, and QuotedAt milestones accumulate.");
            return report;
        }

        private async Task<CorporateReportDto> BuildDataQualityReportAsync(
            string? userId,
            DateTime from,
            DateTime to)
        {
            var query = _dbContext.QuoteListItems.AsNoTracking()
                .Where(q => q.UpdatedAt >= from && q.UpdatedAt <= to);
            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(q => q.OwnerId == userId);

            var rows = await query.ToListAsync();
            var groups = rows.GroupBy(x => new
            {
                Id = x.OwnerId ?? "",
                Name = string.IsNullOrWhiteSpace(x.OwnerName) ? "Unassigned" : x.OwnerName
            }).OrderBy(x => x.Key.Name);
            var now = DateTime.UtcNow;

            var report = CreateReport(
                "DataQuality",
                "CRM Data Quality & Hygiene",
                "Missing follow-ups, client links, values, overdue work, and stale records updated in the selected period.",
                from,
                to);

            report.Columns = new()
            {
                "Owner", "Records", "Missing Follow-up", "Value TBD", "Missing Client",
                "Overdue", "Stale >14d", "Delete Requests", "Quality Score"
            };

            foreach (var group in groups)
            {
                var items = group.ToList();
                var issues = items.Count(x => x.MissingFollowUp) +
                    items.Count(x => x.ValueTbd) +
                    items.Count(x => x.MissingClientLink);
                var possible = items.Count * 3;
                var score = possible == 0 ? 100m : Math.Max(0m, 100m - (decimal)issues / possible * 100m);

                report.Rows.Add(Row(
                    ("Owner", group.Key.Name),
                    ("Records", items.Count.ToString("N0")),
                    ("Missing Follow-up", items.Count(x => x.MissingFollowUp).ToString("N0")),
                    ("Value TBD", items.Count(x => x.ValueTbd).ToString("N0")),
                    ("Missing Client", items.Count(x => x.MissingClientLink).ToString("N0")),
                    ("Overdue", items.Count(x => !x.IsClosed && x.NextFollowUpDate.HasValue && x.NextFollowUpDate < now).ToString("N0")),
                    ("Stale >14d", items.Count(x => !x.IsClosed && x.UpdatedAt < now.AddDays(-14)).ToString("N0")),
                    ("Delete Requests", items.Count(x => x.IsDeleteRequested).ToString("N0")),
                    ("Quality Score", $"{score:N1}%")));
            }

            var totalIssues = rows.Count(x => x.MissingFollowUp) + rows.Count(x => x.ValueTbd) + rows.Count(x => x.MissingClientLink);
            var totalPossible = rows.Count * 3;
            var totalScore = totalPossible == 0 ? 100m : Math.Max(0m, 100m - (decimal)totalIssues / totalPossible * 100m);
            report.Cards = new()
            {
                Card("Quality Score", $"{totalScore:N1}%", "success"),
                Card("Missing Follow-up", rows.Count(x => x.MissingFollowUp), "warning"),
                Card("Missing Client", rows.Count(x => x.MissingClientLink), "danger"),
                Card("Stale Active Records", rows.Count(x => !x.IsClosed && x.UpdatedAt < now.AddDays(-14)), "danger")
            };
            report.Notes.Add("This is a current-state hygiene report limited to records updated in the selected date range.");
            return report;
        }

        private async Task<CorporateReportDto> BuildElvOperationsReportAsync(
            string? userId,
            DateTime from,
            DateTime to,
            string grouping)
        {
            var users = await GetUserNamesAsync();
            var eventsQuery = _dbContext.QuoteEvents.AsNoTracking()
                .Where(e => e.OccurredAt >= from && e.OccurredAt <= to);
            var followUpsQuery = _dbContext.FollowUps.AsNoTracking()
                .Where(f => f.CreatedAt >= from && f.CreatedAt <= to);
            var logsQuery = _dbContext.ActivityLogs.AsNoTracking()
                .Where(a => a.Timestamp >= from && a.Timestamp <= to);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                eventsQuery = eventsQuery.Where(e => e.ActorUserId == userId);
                followUpsQuery = followUpsQuery.Where(f => f.CreatedByUserId == userId);
                logsQuery = logsQuery.Where(a => a.UserId == userId);
            }

            var events = await eventsQuery.ToListAsync();
            var followUps = await followUpsQuery.ToListAsync();
            var logs = await logsQuery.ToListAsync();
            var ids = events.Where(x => !string.IsNullOrWhiteSpace(x.ActorUserId)).Select(x => x.ActorUserId!)
                .Concat(followUps.Where(x => !string.IsNullOrWhiteSpace(x.CreatedByUserId)).Select(x => x.CreatedByUserId!))
                .Concat(logs.Select(x => x.UserId))
                .Distinct()
                .ToList();

            var report = CreateReport(
                "ElvOperations",
                "ELV Operational Controls",
                "Workflow governance, follow-up discipline, handoffs, closures, and Thursday data-update evidence.",
                from,
                to);

            report.Columns = new()
            {
                "User", "Follow-ups", "Status Changes", "Owner Handoffs",
                "Due-Date Changes", "Rep Completions", "Lead Closures", "Thursday ≤16:00 Actions"
            };

            foreach (var id in ids.OrderBy(id => UserName(users, id)))
            {
                var userEvents = events.Where(x => x.ActorUserId == id).ToList();
                var userLogs = logs.Where(x => x.UserId == id).ToList();

                report.Rows.Add(Row(
                    ("User", UserName(users, id)),
                    ("Follow-ups", followUps.Count(x => x.CreatedByUserId == id).ToString("N0")),
                    ("Status Changes", userEvents.Count(x => x.EventType == QuoteEventType.StatusChanged).ToString("N0")),
                    ("Owner Handoffs", userEvents.Count(x => x.EventType == QuoteEventType.OwnerChanged).ToString("N0")),
                    ("Due-Date Changes", userEvents.Count(x => x.EventType == QuoteEventType.FollowUpDateChanged).ToString("N0")),
                    ("Rep Completions", userEvents.Count(x => x.EventType == QuoteEventType.LeadRepCompleted).ToString("N0")),
                    ("Lead Closures", userEvents.Count(x => x.EventType == QuoteEventType.LeadClosed).ToString("N0")),
                    ("Thursday ≤16:00 Actions", userLogs.Count(IsThursdayLockAction).ToString("N0"))));
            }

            report.Cards = new()
            {
                Card("Follow-ups", followUps.Count, "primary"),
                Card("Status Changes", events.Count(x => x.EventType == QuoteEventType.StatusChanged), "info"),
                Card("Rep Completions", events.Count(x => x.EventType == QuoteEventType.LeadRepCompleted), "success"),
                Card("Lead Closures", events.Count(x => x.EventType == QuoteEventType.LeadClosed), "warning")
            };

            report.TrendColumns = new() { "Period", "Follow-ups", "Status Changes", "Completions", "Closures" };
            var buckets = events.Select(x => BucketStart(x.OccurredAt, grouping))
                .Concat(followUps.Select(x => BucketStart(x.CreatedAt, grouping)))
                .Distinct()
                .OrderBy(x => x);

            foreach (var bucket in buckets)
            {
                var bucketEvents = events.Where(x => BucketStart(x.OccurredAt, grouping) == bucket).ToList();
                report.TrendRows.Add(Row(
                    ("Period", BucketLabel(bucket, grouping)),
                    ("Follow-ups", followUps.Count(x => BucketStart(x.CreatedAt, grouping) == bucket).ToString("N0")),
                    ("Status Changes", bucketEvents.Count(x => x.EventType == QuoteEventType.StatusChanged).ToString("N0")),
                    ("Completions", bucketEvents.Count(x => x.EventType == QuoteEventType.LeadRepCompleted).ToString("N0")),
                    ("Closures", bucketEvents.Count(x => x.EventType == QuoteEventType.LeadClosed).ToString("N0"))));
            }

            report.Notes.Add("Thursday compliance is an operational proxy based on logged actions before 16:00 Thursday; direct Zoho/LCS integration would make this authoritative.");
            return report;
        }

        private async Task<Dictionary<string, string>> GetUserNamesAsync()
        {
            return await _dbContext.Users.AsNoTracking()
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email ?? "Unknown User" : u.FullName);
        }

        private static CorporateReportDto CreateReport(string code, string title, string description, DateTime from, DateTime to)
        {
            return new CorporateReportDto
            {
                ReportCode = code,
                Title = title,
                Description = description,
                DateFrom = from,
                DateTo = to,
                PeriodLabel = $"{from:dd MMM yyyy} - {to:dd MMM yyyy}"
            };
        }

        private static CorporateReportCardDto Card(string label, int value, string tone) =>
            Card(label, value.ToString("N0"), tone);

        private static CorporateReportCardDto Card(string label, string value, string tone) =>
            new() { Label = label, Value = value, Tone = tone };

        private static CorporateReportRowDto Row(params (string Key, string Value)[] values)
        {
            return new CorporateReportRowDto
            {
                Values = values.ToDictionary(x => x.Key, x => x.Value)
            };
        }

        private static DateTime BucketStart(DateTime value, string grouping)
        {
            var date = value.Date;
            if (grouping == "Monthly")
                return new DateTime(date.Year, date.Month, 1);

            var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-offset);
        }

        private static string BucketLabel(DateTime value, string grouping)
        {
            return grouping == "Monthly"
                ? value.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                : $"W{ISOWeek.GetWeekOfYear(value)} · {value:dd MMM yyyy}";
        }

        private static string NormalizeReportCode(string? reportCode)
        {
            return reportCode switch
            {
                "FollowUps" => "FollowUps",
                "Pipeline" => "Pipeline",
                "Sla" => "Sla",
                "DataQuality" => "DataQuality",
                "ElvOperations" => "ElvOperations",
                _ => "UserActivity"
            };
        }

        private static bool IsUpdateAction(ActivityLog log)
        {
            return log.Action.Contains("update", StringComparison.OrdinalIgnoreCase) ||
                   log.Action.Contains("assign", StringComparison.OrdinalIgnoreCase) ||
                   log.Action.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
                   log.Action.Contains("close", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsThursdayLockAction(ActivityLog log) =>
            log.Timestamp.DayOfWeek == DayOfWeek.Thursday &&
            log.Timestamp.TimeOfDay <= new TimeSpan(16, 0, 0);

        private static string UserName(Dictionary<string, string> users, string id) =>
            users.TryGetValue(id, out var name) ? name : "Unknown User";

        private static string Percent(int numerator, int denominator) =>
            denominator == 0 ? "-" : $"{(decimal)numerator / denominator * 100m:N1}%";

        private static string Money(decimal value) => $"BHD {value:N3}";

        private static DateTime? OutcomeDate(DateTime? milestone, bool isOutcome, DateTime fallback) =>
            milestone ?? (isOutcome ? fallback : null);

        private static bool IsClosed(QuoteStatus status) =>
            status == QuoteStatus.Won ||
            status == QuoteStatus.Lost ||
            status == QuoteStatus.Cancelled ||
            status == QuoteStatus.LeadClosed ||
            status == QuoteStatus.Merged;
    }
}

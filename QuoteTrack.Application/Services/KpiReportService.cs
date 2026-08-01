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
    public class KpiReportService : IKpiReportService
    {
        private readonly IAppDbContext _dbContext;

        public KpiReportService(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<ApplicationUser>> GetKpiUsersAsync()
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.Email)
                .ToListAsync();
        }

        public async Task<KpiReportDto> GetReportAsync(string employeeId, string templateCode, string reportType, DateTime periodAnchor)
        {
            var normalizedTemplate = NormalizeTemplateCode(templateCode);
            var normalizedType = NormalizeReportType(reportType);
            var (periodStart, periodEnd, weekNumber, periodLabel) = ResolvePeriod(normalizedType, periodAnchor);

            var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == employeeId);
            if (user == null)
                throw new InvalidOperationException("Employee not found.");

            var existing = await _dbContext.KpiReports
                .AsNoTracking()
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r =>
                    r.EmployeeId == employeeId &&
                    r.TemplateCode == normalizedTemplate &&
                    r.ReportType == normalizedType &&
                    r.PeriodStart == periodStart);

            var report = new KpiReportDto
            {
                Id = existing?.Id,
                EmployeeId = employeeId,
                EmployeeName = DisplayName(user),
                TemplateCode = normalizedTemplate,
                ReportType = normalizedType,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                WeekNumber = weekNumber,
                PeriodLabel = periodLabel,
                ManagerReview = existing?.ManagerReview ?? "",
                MainAchievements = existing?.MainAchievements ?? "",
                AdditionalComments = existing?.AdditionalComments ?? "",
                LastSavedAt = existing?.UpdatedAt
            };

            var savedLines = existing?.Lines.ToDictionary(l => LineKey(l.Code, l.MetricName), l => l)
                ?? new Dictionary<string, KpiReportLine>();

            var stats = await BuildStatsAsync(employeeId, periodStart, periodEnd);
            report.Lines = BuildTemplate(normalizedTemplate, normalizedType, stats)
                .Select(line => MergeSavedLine(line, savedLines))
                .ToList();

            if (string.IsNullOrWhiteSpace(report.MainAchievements))
                report.MainAchievements = stats.DefaultAchievements;

            return report;
        }

        public async Task SaveReportAsync(KpiReportDto report, string? submittedByUserId)
        {
            var normalizedTemplate = NormalizeTemplateCode(report.TemplateCode);
            var normalizedType = NormalizeReportType(report.ReportType);
            var (periodStart, periodEnd, weekNumber, periodLabel) = ResolvePeriod(normalizedType, report.PeriodStart);

            var entity = await _dbContext.KpiReports
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r =>
                    r.EmployeeId == report.EmployeeId &&
                    r.TemplateCode == normalizedTemplate &&
                    r.ReportType == normalizedType &&
                    r.PeriodStart == periodStart);

            var now = DateTime.UtcNow;

            if (entity == null)
            {
                entity = new KpiReport
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = report.EmployeeId,
                    CreatedAt = now
                };
                _dbContext.KpiReports.Add(entity);
            }

            entity.EmployeeName = report.EmployeeName;
            entity.TemplateCode = normalizedTemplate;
            entity.ReportType = normalizedType;
            entity.PeriodStart = periodStart;
            entity.PeriodEnd = periodEnd;
            entity.WeekNumber = weekNumber;
            entity.PeriodLabel = periodLabel;
            entity.ManagerReview = report.ManagerReview ?? "";
            entity.MainAchievements = report.MainAchievements ?? "";
            entity.AdditionalComments = report.AdditionalComments ?? "";
            entity.SubmittedByUserId = submittedByUserId ?? "";
            entity.UpdatedAt = now;

            var incomingKeys = report.Lines.Select(l => LineKey(l.Code, l.MetricName)).ToHashSet();
            var removed = entity.Lines.Where(l => !incomingKeys.Contains(LineKey(l.Code, l.MetricName))).ToList();
            foreach (var line in removed)
                entity.Lines.Remove(line);

            foreach (var incoming in report.Lines)
            {
                var key = LineKey(incoming.Code, incoming.MetricName);
                var line = entity.Lines.FirstOrDefault(l => LineKey(l.Code, l.MetricName) == key);
                if (line == null)
                {
                    line = new KpiReportLine { Id = Guid.NewGuid(), KpiReportId = entity.Id };
                    entity.Lines.Add(line);
                }

                line.SortOrder = incoming.SortOrder;
                line.Code = incoming.Code ?? "";
                line.Category = incoming.Category ?? "";
                line.MetricName = incoming.MetricName ?? "";
                line.AutoValueText = incoming.AutoValueText ?? "";
                line.ManualValueText = incoming.ManualValueText ?? "";
                line.ValueText = !string.IsNullOrWhiteSpace(incoming.ManualValueText)
                    ? incoming.ManualValueText.Trim()
                    : incoming.AutoValueText ?? incoming.ValueText ?? "";
                line.Guidance = incoming.Guidance ?? "";
                line.EvidenceText = incoming.EvidenceText ?? "";
                line.IsManualInput = incoming.IsManualInput;
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task<KpiStats> BuildStatsAsync(string employeeId, DateTime periodStart, DateTime periodEnd)
        {
            var exclusiveEnd = periodEnd.AddTicks(1);
            var now = DateTime.UtcNow;

            var closedStatuses = new[]
            {
                QuoteStatus.Won,
                QuoteStatus.Lost,
                QuoteStatus.Cancelled,
                QuoteStatus.LeadClosed,
                QuoteStatus.Merged
            };

            var ownedRows = await _dbContext.QuoteListItems
                .AsNoTracking()
                .Where(q => q.OwnerId == employeeId)
                .Where(q => !q.IsDeleteRequested)
                .ToListAsync();

            var periodRows = ownedRows
                .Where(q => q.UpdatedAt >= periodStart && q.UpdatedAt < exclusiveEnd)
                .ToList();

            var wonRows = periodRows
                .Where(q => q.RecordType == QuoteRecordType.OutgoingQuote && q.Status == QuoteStatus.Won)
                .OrderByDescending(q => q.QuoteValue ?? 0m)
                .ToList();

            var activeRows = ownedRows
                .Where(q => !closedStatuses.Contains(q.Status))
                .ToList();

            var staleRows = activeRows
                .Where(q =>
                    (q.NextFollowUpDate.HasValue && q.NextFollowUpDate.Value < now) ||
                    q.UpdatedAt < now.AddDays(-14))
                .OrderByDescending(q => q.QuoteValue ?? 0m)
                .Take(8)
                .ToList();

            var quoteCycleRows = await _dbContext.Quotes
                .AsNoTracking()
                .Where(q => q.OwnerId == employeeId)
                .Where(q => !q.IsDeleteRequested)
                .Where(q => q.RecordType == QuoteRecordType.OutgoingQuote)
                .Where(q => q.CreatedAt >= periodStart && q.CreatedAt < exclusiveEnd)
                .Select(q => new
                {
                    q.CreatedAt,
                    q.AssignedAt,
                    q.QuotedAt,
                    q.UpdatedAt,
                    q.Status
                })
                .ToListAsync();

            var quotedRows = quoteCycleRows
                .Where(q => q.QuotedAt.HasValue || IsQuotedStatus(q.Status))
                .ToList();

            var within48 = quotedRows.Count(q =>
            {
                var start = q.AssignedAt ?? q.CreatedAt;
                var end = q.QuotedAt ?? q.UpdatedAt;
                return end >= start && (end - start).TotalHours <= 48;
            });

            var activityLogs = await _dbContext.ActivityLogs
                .AsNoTracking()
                .Where(a => a.UserId == employeeId)
                .Where(a => a.Timestamp >= periodStart && a.Timestamp < exclusiveEnd)
                .OrderByDescending(a => a.Timestamp)
                .Take(200)
                .ToListAsync();

            var thursdayLocks = activityLogs.Count(a =>
                a.Timestamp.DayOfWeek == DayOfWeek.Thursday &&
                a.Timestamp.TimeOfDay <= new TimeSpan(16, 0, 0));

            var topAccounts = ownedRows
                .Where(q => !string.IsNullOrWhiteSpace(q.ClientName))
                .GroupBy(q => q.ClientName.Trim())
                .Select(g => new
                {
                    Name = g.Key,
                    Value = g.Sum(x => x.QuoteValue ?? 0m),
                    LastTouch = g.Max(x => x.UpdatedAt)
                })
                .OrderByDescending(x => x.Value)
                .ThenByDescending(x => x.LastTouch)
                .Take(10)
                .Select(x => x.Name)
                .ToList();

            return new KpiStats
            {
                QuoteCount = quoteCycleRows.Count,
                QuotedCount = quotedRows.Count,
                QuotedWithin48Count = within48,
                ActivityCount = activityLogs.Count,
                ThursdayLockCount = thursdayLocks,
                ActiveDealCount = activeRows.Count,
                ActivePipelineValue = activeRows.Sum(q => q.QuoteValue ?? 0m),
                WonCount = wonRows.Count,
                WonValue = wonRows.Sum(q => q.QuoteValue ?? 0m),
                WonLabels = wonRows.Take(5).Select(Label).ToList(),
                StaleDealCount = staleRows.Count,
                StaleDealLabels = staleRows.Select(Label).ToList(),
                TopAccountLabels = topAccounts,
                LatestActivityLabels = activityLogs.Take(5).Select(a => $"{a.Action}: {a.Details}".Trim()).ToList()
            };
        }

        private static List<KpiReportLineDto> BuildTemplate(string templateCode, string reportType, KpiStats stats)
        {
            return (NormalizeTemplateCode(templateCode), reportType) switch
            {
                ("Sales 01", "Monthly") => BuildSales01Monthly(stats),
                ("Sales 01", _) => BuildSales01Weekly(stats),
                ("ELV 02", "Monthly") => BuildFarajMonthly(stats),
                ("ELV 02", _) => BuildFarajWeekly(stats),
                ("ELV 01", "Monthly") => BuildElv01Monthly(stats),
                _ => BuildElv01Weekly(stats)
            };
        }

        private static List<KpiReportLineDto> BuildElv01Weekly(KpiStats stats)
        {
            return new List<KpiReportLineDto>
            {
                Line(10, "SO-02 Quality", "Proposal Velocity / 48-Hour Rule", ProposalVelocity(stats), "80% of BOQs/quotes within 48 hours. Add reason if some need more time.", Evidence(stats), false),
                Line(20, "SO-04 Governance", "Team Discipline / Staff Management", $"Stable. {stats.ActivityCount} logged actions in period.", "Report ELV team oversight, reporting flow, and workflow adherence.", ActivityEvidence(stats), true),
                Line(30, "SO-05 Discipline", "Thursday Lock Completed?", $"{stats.ThursdayLockCount} Thursday-before-16:00 actions logged.", "Verify surveys and project milestones were logged by 16:00 on Thursday.", ActivityEvidence(stats), true),
                Line(40, "Weekly Funnel Review", "ELV Sales Funnel Review", StaleSummary(stats), "Summarize stalled deals identified and actions taken to unblock them.", StaleEvidence(stats), true),
                Line(50, "Support / Risks", "Escalations Needed", "", "List blockers, risks, or support needed from management.", "", true)
            };
        }

        private static List<KpiReportLineDto> BuildFarajWeekly(KpiStats stats)
        {
            return new List<KpiReportLineDto>
            {
                Line(10, "SO-02 Quality", "Technical Autonomy / 48-Hour Turnaround", ProposalVelocity(stats), "Independent design and quotation of proposals post-survey within 48 hours.", Evidence(stats), false),
                Line(20, "SO-04 Leadership", "Operational Support / Transition Support", $"{stats.ActivityCount} logged actions in period.", "Assist ELV Head and manage inter-departmental resource requests.", ActivityEvidence(stats), true),
                Line(30, "SO-05 Asset Mgmt", "Project Stock Turn / Inventory Usage", "", "Confirm project designs optimized use of available inventory.", "", true),
                Line(40, "Reputation", "Digital Growth / Google Reviews", "", "Record positive reviews secured from enterprise clients upon project completion.", "", true),
                Line(50, "Support / Risks", "Escalations Needed", "", "List blockers, risks, or support needed from ELV Head / management.", "", true)
            };
        }

        private static List<KpiReportLineDto> BuildElv01Monthly(KpiStats stats)
        {
            return new List<KpiReportLineDto>
            {
                Line(10, "SO-01 Revenue", "Departmental GP Progress vs 140K Annual Target", "", "Enter monthly GP progress and key technical presentations / closures for Government and Corporate tenders.", RevenueEvidence(stats), true),
                Line(20, "SO-03 Continuity", "Top 10 ELV Account Retention", AccountRetention(stats), "Record relationship calls and retention actions for top accounts.", AccountEvidence(stats), true),
                Line(30, "Monthly Summary", "Major Wins / Closures", WinsSummary(stats), "Summarize biggest wins, closures, or high-margin deals this month.", WinsEvidence(stats), true),
                Line(40, "Support / Risks", "Escalations Needed", "", "List blockers, risks, or support needed from management.", "", true)
            };
        }

        private static List<KpiReportLineDto> BuildFarajMonthly(KpiStats stats)
        {
            return new List<KpiReportLineDto>
            {
                Line(10, "SO-01 Revenue", "Individual GP Target (BHD)", "", "Track monthly progress toward target. GP figure is manual until finance integration is added.", RevenueEvidence(stats), true),
                Line(20, "SO-03 Profitability", "Net Margin %", "", "Maintain strict margin discipline; target minimum 35% net margin on self-managed projects.", RevenueEvidence(stats), true),
                Line(30, "SO-06 Collection", "Debt Recovery / Cashflow Health", "", "Confirm milestone-based collections and overdue follow-up status.", StaleEvidence(stats), true),
                Line(40, "Support / Risks", "Escalations Needed", "", "List blockers, risks, or support needed from ELV Head / management.", "", true)
            };
        }

        private static List<KpiReportLineDto> BuildSales01Weekly(KpiStats stats)
        {
            return new List<KpiReportLineDto>
            {
                Line(10, "SO-04 Leadership", "Resource Request Protocol Followed?", "", "Weight 5%. Target: Formal Protocol. All technical support requests must follow formal protocol; informal tasking is prohibited.", ActivityEvidence(stats), true),
                Line(20, "SO-05 Discipline", "Zoho / LCS Updated by Thursday?", $"{stats.ThursdayLockCount} Thursday-before-16:00 actions logged.", "Weight 10%. Target: 100% Data Accuracy. Confirm milestones and data updates are complete by COB Thursday.", ActivityEvidence(stats), true),
                Line(30, "Support / Risks", "Escalations Needed", "", "List blockers, risks, or support needed from CEO / management.", "", true)
            };
        }

        private static List<KpiReportLineDto> BuildSales01Monthly(KpiStats stats)
        {
            return new List<KpiReportLineDto>
            {
                Line(10, "SO-01 Revenue", "Departmental GP Closed-Won (BHD)", "", "Weight 45%. Target: BHD 9,000 monthly. Enter total monthly GP achieved from Closed-Won transactions; minimum 10% GP floor applies.", RevenueEvidence(stats), true),
                Line(20, "SO-02 Account Governance", "Top 10 Accounts Status", AccountRetention(stats), "Weight 35%. Target: 100% Relationship Stability. Classify each key account as Stable / At Risk / Lost.", AccountEvidence(stats), true),
                Line(30, "SO-02 Account Governance", "Retention / Pipeline Progress", AccountRetention(stats), "Record renewals, long-term pipeline, key account movement, and quarterly strategic review outcomes.", AccountEvidence(stats), true),
                Line(40, "SO-03 Business Development", "LED Revenue / Projects", "", "Weight 10%. Target: Incremental Revenue. Record LED-specific revenue, project count, or notable wins for the month.", WinsEvidence(stats), true),
                Line(50, "Support / Risks", "Escalations Needed", "", "List blockers, risks, or support needed from CEO / management.", "", true)
            };
        }

        private static KpiReportLineDto Line(int sortOrder, string code, string metric, string autoValue, string guidance, string evidence, bool isManual)
        {
            return new KpiReportLineDto
            {
                SortOrder = sortOrder,
                Code = code,
                Category = code,
                MetricName = metric,
                AutoValueText = autoValue,
                ValueText = autoValue,
                Guidance = guidance,
                EvidenceText = evidence,
                IsManualInput = isManual
            };
        }

        private static KpiReportLineDto MergeSavedLine(KpiReportLineDto line, Dictionary<string, KpiReportLine> savedLines)
        {
            if (!savedLines.TryGetValue(LineKey(line.Code, line.MetricName), out var saved))
                return line;

            line.ManualValueText = saved.ManualValueText;
            line.ValueText = !string.IsNullOrWhiteSpace(saved.ManualValueText)
                ? saved.ManualValueText
                : line.AutoValueText;
            return line;
        }

        private static (DateTime Start, DateTime End, int? WeekNumber, string Label) ResolvePeriod(string reportType, DateTime anchor)
        {
            var date = DateTime.SpecifyKind(anchor.Date, DateTimeKind.Utc);

            if (reportType == "Monthly")
            {
                var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var exclusiveEnd = start.AddMonths(1);
                return (start, exclusiveEnd.AddTicks(-1), null, start.ToString("MMMM yyyy", CultureInfo.InvariantCulture));
            }

            var delta = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = date.AddDays(-delta);
            var weekEndExclusive = weekStart.AddDays(7);
            var weekNo = ISOWeek.GetWeekOfYear(weekStart);
            return (weekStart, weekEndExclusive.AddTicks(-1), weekNo, $"Week {weekNo} ({weekStart:dd MMM} - {weekEndExclusive.AddDays(-1):dd MMM yyyy})");
        }

        private static string NormalizeReportType(string reportType)
        {
            return string.Equals(reportType, "Monthly", StringComparison.OrdinalIgnoreCase) ? "Monthly" : "Weekly";
        }

        private static string NormalizeTemplateCode(string? templateCode)
        {
            var value = (templateCode ?? "").Trim();

            if (value.Equals("Sales 01", StringComparison.OrdinalIgnoreCase))
                return "Sales 01";

            if (value.Equals("ELV 02", StringComparison.OrdinalIgnoreCase))
                return "ELV 02";

            return "ELV 01";
        }

        private static string DisplayName(ApplicationUser user)
        {
            return string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? "Unknown User" : user.FullName;
        }

        private static string LineKey(string? code, string? metricName)
        {
            return $"{(code ?? "").Trim().ToLowerInvariant()}::{(metricName ?? "").Trim().ToLowerInvariant()}";
        }

        private static string ProposalVelocity(KpiStats stats)
        {
            if (stats.QuotedCount == 0)
                return "No quoted proposals recorded in this period.";

            var pct = Math.Round((decimal)stats.QuotedWithin48Count / stats.QuotedCount * 100m, 1);
            return $"{pct}% ({stats.QuotedWithin48Count}/{stats.QuotedCount}) within 48 hours.";
        }

        private static string Evidence(KpiStats stats)
        {
            return $"{stats.QuoteCount} outgoing quotes created; {stats.QuotedCount} reached quoted/sent stage.";
        }

        private static string RevenueEvidence(KpiStats stats)
        {
            return $"CRM evidence: {stats.WonCount} won deals, BHD {stats.WonValue:N3} won value, BHD {stats.ActivePipelineValue:N3} active pipeline.";
        }

        private static string WinsSummary(KpiStats stats)
        {
            return stats.WonLabels.Count == 0 ? "No won closures recorded in CRM for this period." : string.Join("; ", stats.WonLabels);
        }

        private static string WinsEvidence(KpiStats stats)
        {
            return stats.WonLabels.Count == 0 ? "" : "Won records: " + string.Join("; ", stats.WonLabels);
        }

        private static string StaleSummary(KpiStats stats)
        {
            return stats.StaleDealCount == 0 ? "No overdue or stale active deals detected." : $"{stats.StaleDealCount} stale/overdue active deals require review.";
        }

        private static string StaleEvidence(KpiStats stats)
        {
            return stats.StaleDealLabels.Count == 0 ? "" : string.Join("; ", stats.StaleDealLabels);
        }

        private static string AccountRetention(KpiStats stats)
        {
            return stats.TopAccountLabels.Count == 0 ? "No account activity found yet." : string.Join(", ", stats.TopAccountLabels);
        }

        private static string AccountEvidence(KpiStats stats)
        {
            return stats.TopAccountLabels.Count == 0 ? "" : "Top active accounts by pipeline/history: " + string.Join(", ", stats.TopAccountLabels);
        }

        private static string ActivityEvidence(KpiStats stats)
        {
            if (stats.LatestActivityLabels.Count == 0)
                return "No activity log evidence found for this period.";

            return string.Join("; ", stats.LatestActivityLabels);
        }

        private static string Label(QuoteListItem q)
        {
            var client = string.IsNullOrWhiteSpace(q.ClientName) ? q.SenderEmail : q.ClientName;
            var reference = string.IsNullOrWhiteSpace(q.QuoteReference) ? q.Subject : q.QuoteReference;
            return $"{client} ({reference})";
        }

        private static bool IsQuotedStatus(QuoteStatus status)
        {
            return status == QuoteStatus.Sent ||
                   status == QuoteStatus.QuoteNew ||
                   status == QuoteStatus.QuoteReviewed ||
                   status == QuoteStatus.QuoteApproved ||
                   status == QuoteStatus.Won ||
                   status == QuoteStatus.Lost;
        }

        private class KpiStats
        {
            public int QuoteCount { get; set; }
            public int QuotedCount { get; set; }
            public int QuotedWithin48Count { get; set; }
            public int ActivityCount { get; set; }
            public int ThursdayLockCount { get; set; }
            public int ActiveDealCount { get; set; }
            public decimal ActivePipelineValue { get; set; }
            public int WonCount { get; set; }
            public decimal WonValue { get; set; }
            public List<string> WonLabels { get; set; } = new();
            public int StaleDealCount { get; set; }
            public List<string> StaleDealLabels { get; set; } = new();
            public List<string> TopAccountLabels { get; set; } = new();
            public List<string> LatestActivityLabels { get; set; } = new();

            public string DefaultAchievements =>
                WonLabels.Count > 0
                    ? string.Join(Environment.NewLine, WonLabels)
                    : $"Active pipeline: {ActiveDealCount} deals, BHD {ActivePipelineValue:N3}.";
        }
    }
}

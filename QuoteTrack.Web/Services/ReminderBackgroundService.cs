using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;
using QuoteTrack.Infrastructure.Logging;

namespace QuoteTrack.Web.Services
{
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly ILogger<ReminderBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly string _statePath;

        public ReminderBackgroundService(
            ILogger<ReminderBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;

            var appData = Path.Combine(AppContext.BaseDirectory, "App_Data");
            Directory.CreateDirectory(appData);
            _statePath = Path.Combine(appData, "daily_digest_state.json");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SMTP Reminder Engine is starting...");
            SystemLogger.LogEvent("INFO", "SMTP Reminder", "Reminder service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    SystemLogger.LogHeartbeat("SMTP");

                    await TrySendSalesReminderEmailsAsync(stoppingToken);
                    await TrySendAdminMorningDigestAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReminderBackgroundService error");
                    SystemLogger.LogEvent("ERROR", "SMTP Reminder", ex.ToString());
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        private async Task TrySendSalesReminderEmailsAsync(CancellationToken stoppingToken)
        {
            var enabled = _config.GetValue<bool?>("EnableSalesReminderEmails") ?? true;
            if (!enabled)
                return;

            var intervalHours = _config.GetValue<int?>("ReminderIntervalHours") ?? 12;
            if (intervalHours <= 0) intervalHours = 12;

            var lookaheadHours = _config.GetValue<int?>("ReminderLookaheadHours") ?? 24;
            if (lookaheadHours <= 0) lookaheadHours = 24;

            var overdueOnly = _config.GetValue<bool?>("ReminderSendOnlyOverdue") ?? true;
            var includeDueToday = _config.GetValue<bool?>("ReminderIncludeDueToday") ?? true;
            var copyAdmin = _config.GetValue<bool?>("ReminderAlsoSendToAdmin") ?? false;

            var state = LoadState();
            var nowUtc = DateTime.UtcNow;

            if (state.LastReminderRunUtc.HasValue &&
                nowUtc < state.LastReminderRunUtc.Value.AddHours(intervalHours))
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<WorkflowEmailService>();

            var tz = ResolveBahrainTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var todayLocalStart = nowLocal.Date;
            var todayLocalEnd = todayLocalStart.AddDays(1);

            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocalStart, tz);
            var todayEndUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocalEnd, tz);

            var thresholdUtc = nowUtc.AddHours(lookaheadHours);

            var baseQuery = db.Quotes
                .AsNoTracking()
                .Include(q => q.Owner)
                .Where(q => !q.IsDeleteRequested)
                .Where(q => q.NextFollowUpDate.HasValue)
                .Where(q =>
                    q.Status != QuoteStatus.Won &&
                    q.Status != QuoteStatus.Lost &&
                    q.Status != QuoteStatus.Cancelled &&
                    q.Status != QuoteStatus.LeadClosed &&
                    q.Status != QuoteStatus.Merged);

            if (overdueOnly)
            {
                baseQuery = baseQuery.Where(q => q.NextFollowUpDate!.Value < nowUtc);
            }
            else
            {
                baseQuery = baseQuery.Where(q => q.NextFollowUpDate!.Value <= thresholdUtc);

                if (!includeDueToday)
                {
                    baseQuery = baseQuery.Where(q =>
                        q.NextFollowUpDate!.Value < todayStartUtc ||
                        q.NextFollowUpDate!.Value >= todayEndUtc);
                }
            }

            var items = await baseQuery
                .OrderBy(q => q.NextFollowUpDate)
                .ToListAsync(stoppingToken);

            var groupedByOwner = items
                .Where(q => q.Owner != null && !string.IsNullOrWhiteSpace(q.Owner.Email))
                .GroupBy(q => q.Owner!.Email!, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var grp in groupedByOwner)
            {
                var first = grp.First();
                var ownerEmail = first.Owner!.Email!;
                var ownerName = string.IsNullOrWhiteSpace(first.Owner.FullName) ? ownerEmail : first.Owner.FullName!;

                var subject = $"NexTraQ Reminder - {grp.Count()} due / overdue item(s)";
                var html = BuildRepReminderHtml(tz, nowUtc, ownerName, grp.ToList());

                await emailService.SendEmailAsync(ownerEmail, ownerName, subject, html);

                SystemLogger.LogEvent("SUCCESS", "SMTP Reminder", $"Sales reminder sent to {ownerEmail}");
            }

            if (copyAdmin)
            {
                var adminEmail = _config["SystemAdminEmail"];
                if (!string.IsNullOrWhiteSpace(adminEmail) && items.Count > 0)
                {
                    var html = BuildAdminReminderCopyHtml(tz, nowUtc, items);
                    await emailService.SendEmailAsync(adminEmail!, "System Admin", "NexTraQ Reminder Copy - Due / Overdue Items", html);
                }
            }

            state.LastReminderRunUtc = nowUtc;
            SaveState(state);
        }

        private async Task TrySendAdminMorningDigestAsync(CancellationToken stoppingToken)
        {
            var enabled = _config.GetValue<bool?>("EnableDailyAdminDigest") ?? true;
            if (!enabled)
                return;

            var tz = ResolveBahrainTimeZone();
            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            var digestTimeText = _config["DailyDigestTimeLocal"] ?? "08:00";
            if (!TimeOnly.TryParse(digestTimeText, out var digestTime))
                digestTime = new TimeOnly(8, 0);

            if (nowLocal.TimeOfDay < digestTime.ToTimeSpan())
                return;

            var state = LoadState();
            if (state.LastDigestLocalDate == nowLocal.Date)
                return;

            var digestRecipient = _config["DailyDigestRecipientEmail"];
            if (string.IsNullOrWhiteSpace(digestRecipient))
                digestRecipient = _config["SystemAdminEmail"];

            if (string.IsNullOrWhiteSpace(digestRecipient))
            {
                SystemLogger.LogEvent("ERROR", "SMTP Reminder", "No daily digest recipient configured.");
                return;
            }

            var includeIngestion = _config.GetValue<bool?>("DailyDigestIncludeIngestion") ?? true;
            var includeLogins = _config.GetValue<bool?>("DailyDigestIncludeLogins") ?? true;
            var includeDueSummary = _config.GetValue<bool?>("DailyDigestIncludeDueSummary") ?? true;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<WorkflowEmailService>();

            var todayLocal = nowLocal.Date;
            var yesterdayLocal = todayLocal.AddDays(-1);

            var reportStartLocal = yesterdayLocal;
            var reportEndLocal = todayLocal;

            var reportStartUtc = TimeZoneInfo.ConvertTimeToUtc(reportStartLocal, tz);
            var reportEndUtc = TimeZoneInfo.ConvertTimeToUtc(reportEndLocal, tz);

            var ingestedLeads = new List<Quote>();
            var ingestedQuotes = new List<Quote>();
            var loginUsers = new List<ApplicationUser>();
            var activeLeadsDue = new List<Quote>();
            var activeQuotesDue = new List<Quote>();

            if (includeIngestion)
            {
                ingestedLeads = await db.Quotes
                    .AsNoTracking()
                    .Include(q => q.Owner)
                    .Where(q => q.RecordType == QuoteRecordType.Lead)
                    .Where(q => q.CreatedAt >= reportStartUtc && q.CreatedAt < reportEndUtc)
                    .OrderByDescending(q => q.CreatedAt)
                    .ToListAsync(stoppingToken);

                ingestedQuotes = await db.Quotes
                    .AsNoTracking()
                    .Include(q => q.Owner)
                    .Where(q => q.RecordType == QuoteRecordType.OutgoingQuote)
                    .Where(q => q.CreatedAt >= reportStartUtc && q.CreatedAt < reportEndUtc)
                    .OrderByDescending(q => q.CreatedAt)
                    .ToListAsync(stoppingToken);
            }

            if (includeLogins)
            {
                var loginUserIds = await db.ActivityLogs
                    .AsNoTracking()
                    .Where(a => a.Action == "LOGIN_SUCCESS")
                    .Where(a => a.Timestamp >= reportStartUtc && a.Timestamp < reportEndUtc)
                    .Select(a => a.UserId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                loginUsers = await db.Users
                    .AsNoTracking()
                    .Where(u => loginUserIds.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .ToListAsync(stoppingToken);
            }

            if (includeDueSummary)
            {
                activeLeadsDue = await db.Quotes
                    .AsNoTracking()
                    .Include(q => q.Owner)
                    .Where(q => q.RecordType == QuoteRecordType.Lead)
                    .Where(q => !q.IsDeleteRequested)
                    .Where(q => q.Status != QuoteStatus.Won &&
                                q.Status != QuoteStatus.Lost &&
                                q.Status != QuoteStatus.Cancelled &&
                                q.Status != QuoteStatus.LeadClosed &&
                                q.Status != QuoteStatus.Merged)
                    .Where(q => q.NextFollowUpDate.HasValue && q.NextFollowUpDate.Value <= nowUtc)
                    .OrderBy(q => q.NextFollowUpDate)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                activeQuotesDue = await db.Quotes
                    .AsNoTracking()
                    .Include(q => q.Owner)
                    .Where(q => q.RecordType == QuoteRecordType.OutgoingQuote)
                    .Where(q => !q.IsDeleteRequested)
                    .Where(q => q.Status != QuoteStatus.Won &&
                                q.Status != QuoteStatus.Lost &&
                                q.Status != QuoteStatus.Cancelled &&
                                q.Status != QuoteStatus.LeadClosed &&
                                q.Status != QuoteStatus.Merged)
                    .Where(q => q.NextFollowUpDate.HasValue && q.NextFollowUpDate.Value <= nowUtc)
                    .OrderBy(q => q.NextFollowUpDate)
                    .Take(20)
                    .ToListAsync(stoppingToken);
            }

            var subject = $"NexTraQ Daily Admin Digest - {todayLocal:dd MMM yyyy}";

            var html = BuildDailyDigestHtml(
                tz,
                nowUtc,
                reportStartLocal,
                reportEndLocal,
                includeIngestion,
                includeLogins,
                includeDueSummary,
                ingestedLeads,
                ingestedQuotes,
                loginUsers,
                activeLeadsDue,
                activeQuotesDue);

            await emailService.SendEmailAsync(digestRecipient!, "System Admin", subject, html);

            state.LastDigestLocalDate = todayLocal;
            SaveState(state);

            SystemLogger.LogEvent("SUCCESS", "SMTP Reminder", $"Daily admin digest sent to {digestRecipient} for {todayLocal:yyyy-MM-dd}");
        }

        private string BuildRepReminderHtml(TimeZoneInfo tz, DateTime nowUtc, string ownerName, List<Quote> items)
        {
            string LocalText(DateTime utc) =>
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz).ToString("dd MMM yyyy, hh:mm tt");

            string Safe(string? s) => WebUtility.HtmlEncode(s ?? "");

            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#222;'>");
            sb.Append($"<h2 style='margin:0 0 8px 0;'>NexTraQ Reminder for {Safe(ownerName)}</h2>");
            sb.Append($"<div style='color:#666;margin-bottom:18px;'>Generated at {LocalText(nowUtc)} (Bahrain time)</div>");

            var leads = items.Where(x => x.RecordType == QuoteRecordType.Lead).ToList();
            var quotes = items.Where(x => x.RecordType == QuoteRecordType.OutgoingQuote).ToList();

            sb.Append("<div style='display:flex;gap:12px;flex-wrap:wrap;margin-bottom:18px;'>");
            sb.Append(Card("Total Due / Overdue", items.Count.ToString()));
            sb.Append(Card("Leads", leads.Count.ToString()));
            sb.Append(Card("Quotes", quotes.Count.ToString()));
            sb.Append("</div>");

            if (leads.Count > 0)
            {
                sb.Append("<h3>Leads</h3>");
                sb.Append(BuildQuoteTable(
                    leads.Take(25).ToList(),
                    q => Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail),
                    q => Safe(q.Subject),
                    q => q.NextFollowUpDate.HasValue ? LocalText(q.NextFollowUpDate.Value) : "Not set",
                    q => Safe(q.Status.ToString()),
                    q => "-"));
            }

            if (quotes.Count > 0)
            {
                sb.Append("<h3 style='margin-top:18px;'>Quotes</h3>");
                sb.Append(BuildQuoteTable(
                    quotes.Take(25).ToList(),
                    q => Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail),
                    q => Safe(q.QuoteReference ?? q.Subject),
                    q => q.NextFollowUpDate.HasValue ? LocalText(q.NextFollowUpDate.Value) : "Not set",
                    q => Safe(q.Status.ToString()),
                    q => q.QuoteValue.HasValue ? $"{Safe(q.Currency)} {q.QuoteValue.Value:N3}" : "TBD"));
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private string BuildAdminReminderCopyHtml(TimeZoneInfo tz, DateTime nowUtc, List<Quote> items)
        {
            string LocalText(DateTime utc) =>
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz).ToString("dd MMM yyyy, hh:mm tt");

            string Safe(string? s) => WebUtility.HtmlEncode(s ?? "");

            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#222;'>");
            sb.Append("<h2 style='margin:0 0 8px 0;'>NexTraQ Admin Reminder Copy</h2>");
            sb.Append($"<div style='color:#666;margin-bottom:18px;'>Generated at {LocalText(nowUtc)} (Bahrain time)</div>");

            sb.Append(BuildQuoteTable(
                items.Take(50).ToList(),
                q => Safe(q.Owner?.FullName ?? "Unassigned"),
                q => Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail),
                q => Safe(q.QuoteReference ?? q.Subject),
                q => q.NextFollowUpDate.HasValue ? LocalText(q.NextFollowUpDate.Value) : "Not set",
                q => Safe(q.Status.ToString())));

            sb.Append("</div>");
            return sb.ToString();
        }

        private string BuildDailyDigestHtml(
            TimeZoneInfo tz,
            DateTime nowUtc,
            DateTime reportStartLocal,
            DateTime reportEndLocal,
            bool includeIngestion,
            bool includeLogins,
            bool includeDueSummary,
            List<Quote> ingestedLeads,
            List<Quote> ingestedQuotes,
            List<ApplicationUser> loginUsers,
            List<Quote> activeLeadsDue,
            List<Quote> activeQuotesDue)
        {
            string LocalText(DateTime utc) =>
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz).ToString("dd MMM yyyy, hh:mm tt");

            string Safe(string? s) => WebUtility.HtmlEncode(s ?? "");

            var sb = new StringBuilder();

            sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#222;'>");
            sb.Append("<h2 style='margin:0 0 8px 0;'>NexTraQ Daily Admin Digest</h2>");
            sb.Append($"<div style='color:#666;margin-bottom:18px;'>Generated at {LocalText(nowUtc)} (Bahrain time)</div>");

            sb.Append("<div style='display:flex;gap:12px;flex-wrap:wrap;margin-bottom:18px;'>");
            if (includeIngestion)
            {
                sb.Append(Card("Leads Ingested", ingestedLeads.Count.ToString()));
                sb.Append(Card("Quotes Ingested", ingestedQuotes.Count.ToString()));
            }
            if (includeLogins)
            {
                sb.Append(Card("Users Logged In", loginUsers.Count.ToString()));
            }
            if (includeDueSummary)
            {
                sb.Append(Card("Leads Due / Overdue", activeLeadsDue.Count.ToString()));
                sb.Append(Card("Quotes Due / Overdue", activeQuotesDue.Count.ToString()));
            }
            sb.Append("</div>");

            if (includeIngestion)
            {
                sb.Append($"<h3>Yesterday's Intake ({reportStartLocal:dd MMM} - {(reportEndLocal.AddSeconds(-1)):dd MMM yyyy})</h3>");

                sb.Append("<h4 style='margin-bottom:6px;'>Leads Ingested</h4>");
                sb.Append(BuildQuoteTable(
                    ingestedLeads.Take(15).ToList(),
                    q => $"{Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail)}",
                    q => Safe(q.Subject),
                    q => Safe(q.Owner?.FullName ?? "Unassigned"),
                    q => LocalText(q.CreatedAt),
                    q => "-"));

                sb.Append("<h4 style='margin:18px 0 6px 0;'>Quotes Ingested</h4>");
                sb.Append(BuildQuoteTable(
                    ingestedQuotes.Take(15).ToList(),
                    q => $"{Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail)}",
                    q => Safe(q.QuoteReference ?? q.Subject),
                    q => Safe(q.Owner?.FullName ?? "Unassigned"),
                    q => LocalText(q.CreatedAt),
                    q => q.QuoteValue.HasValue ? $"{Safe(q.Currency)} {q.QuoteValue.Value:N3}" : "TBD"));
            }

            if (includeLogins)
            {
                sb.Append("<h4 style='margin:18px 0 6px 0;'>Users Logged In</h4>");
                if (loginUsers.Count == 0)
                {
                    sb.Append("<div style='padding:10px;border:1px solid #eee;border-radius:8px;background:#fafafa;'>No login records found for the reporting window.</div>");
                }
                else
                {
                    sb.Append("<ul style='margin-top:6px;'>");
                    foreach (var user in loginUsers)
                    {
                        sb.Append($"<li>{Safe(user.FullName)} ({Safe(user.Email)})</li>");
                    }
                    sb.Append("</ul>");
                }
            }

            if (includeDueSummary)
            {
                sb.Append("<h3 style='margin-top:22px;'>Due / Overdue Snapshot</h3>");

                sb.Append("<h4 style='margin-bottom:6px;'>Leads Due / Overdue</h4>");
                sb.Append(BuildQuoteTable(
                    activeLeadsDue,
                    q => Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail),
                    q => Safe(q.Subject),
                    q => Safe(q.Owner?.FullName ?? "Unassigned"),
                    q => q.NextFollowUpDate.HasValue ? LocalText(q.NextFollowUpDate.Value) : "Not set",
                    q => Safe(q.Status.ToString())));

                sb.Append("<h4 style='margin:18px 0 6px 0;'>Quotes Due / Overdue</h4>");
                sb.Append(BuildQuoteTable(
                    activeQuotesDue,
                    q => Safe(q.Client?.CompanyName ?? q.ClientName ?? q.SenderEmail),
                    q => Safe(q.QuoteReference ?? q.Subject),
                    q => Safe(q.Owner?.FullName ?? "Unassigned"),
                    q => q.NextFollowUpDate.HasValue ? LocalText(q.NextFollowUpDate.Value) : "Not set",
                    q => q.QuoteValue.HasValue ? $"{Safe(q.Currency)} {q.QuoteValue.Value:N3}" : "TBD"));
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private string BuildQuoteTable(
            List<Quote> rows,
            Func<Quote, string> col1,
            Func<Quote, string> col2,
            Func<Quote, string> col3,
            Func<Quote, string> col4,
            Func<Quote, string> col5)
        {
            if (rows.Count == 0)
                return "<div style='padding:10px;border:1px solid #eee;border-radius:8px;background:#fafafa;'>None.</div>";

            var sb = new StringBuilder();
            sb.Append("<table style='width:100%;border-collapse:collapse;font-size:13px;'>");
            sb.Append("<thead><tr style='background:#f5f5f5;'>");
            sb.Append("<th style='text-align:left;padding:8px;border:1px solid #e5e5e5;'>Col 1</th>");
            sb.Append("<th style='text-align:left;padding:8px;border:1px solid #e5e5e5;'>Col 2</th>");
            sb.Append("<th style='text-align:left;padding:8px;border:1px solid #e5e5e5;'>Col 3</th>");
            sb.Append("<th style='text-align:left;padding:8px;border:1px solid #e5e5e5;'>Col 4</th>");
            sb.Append("<th style='text-align:left;padding:8px;border:1px solid #e5e5e5;'>Col 5</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                sb.Append($"<td style='padding:8px;border:1px solid #e5e5e5;'>{col1(row)}</td>");
                sb.Append($"<td style='padding:8px;border:1px solid #e5e5e5;'>{col2(row)}</td>");
                sb.Append($"<td style='padding:8px;border:1px solid #e5e5e5;'>{col3(row)}</td>");
                sb.Append($"<td style='padding:8px;border:1px solid #e5e5e5;'>{col4(row)}</td>");
                sb.Append($"<td style='padding:8px;border:1px solid #e5e5e5;'>{col5(row)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string Card(string label, string value)
        {
            return $"<div style='min-width:160px;border:1px solid #e5e5e5;border-radius:12px;padding:12px 14px;background:#fafafa;'>" +
                   $"<div style='font-size:12px;color:#666;text-transform:uppercase;font-weight:700;margin-bottom:4px;'>{WebUtility.HtmlEncode(label)}</div>" +
                   $"<div style='font-size:24px;font-weight:800;'>{WebUtility.HtmlEncode(value)}</div>" +
                   $"</div>";
        }

        private TimeZoneInfo ResolveBahrainTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bahrain"); } catch { }
            try { return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); } catch { }
            return TimeZoneInfo.Utc;
        }

        private ReminderState LoadState()
        {
            try
            {
                if (!File.Exists(_statePath))
                    return new ReminderState();

                return JsonSerializer.Deserialize<ReminderState>(File.ReadAllText(_statePath)) ?? new ReminderState();
            }
            catch
            {
                return new ReminderState();
            }
        }

        private void SaveState(ReminderState state)
        {
            try
            {
                File.WriteAllText(_statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
            }
        }

        private sealed class ReminderState
        {
            public DateTime? LastDigestLocalDate { get; set; }
            public DateTime? LastReminderRunUtc { get; set; }
        }
    }
}

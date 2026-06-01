using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Infrastructure.Data;
using QuoteTrack.Infrastructure.Email;
using QuoteTrack.Domain.Entities;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuoteTrack.Web.Services
{
    public class RfqEmailIngestionWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RfqEmailIngestionWorker> _logger;
        private readonly IConfiguration _config;
        private readonly string _internalDomain;
        private readonly string _alternateInternalDomain;

        // HARD ROUTE: This worker must ONLY handle Leads/RFQs.
        private const string LeadsMailbox = "leads@nexcelservice.net";
        private const string QuotesMailbox = "quotes@nexcelservice.net";

        public RfqEmailIngestionWorker(IServiceProvider serviceProvider, ILogger<RfqEmailIngestionWorker> logger, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;

            _internalDomain = "@nexcel.me";
            _alternateInternalDomain = "@nexcelservice.net";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NexTraQ: RFQ (Leads) Ingestion Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessIncomingEmailsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NexTraQ: Failed to process incoming emails.");
                }

                await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
            }
        }

        private async Task ProcessIncomingEmailsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailParser = scope.ServiceProvider.GetRequiredService<EmailParsingService>();

            var imapAccounts = _config.GetSection("ImapAccounts").GetChildren();

            foreach (var account in imapAccounts)
            {
                bool isActive = bool.TryParse(account["IsActive"], out var active) && active;
                if (!isActive) continue;

                string server = account["Host"] ?? "";
                int port = int.TryParse(account["Port"], out var p) ? p : 993;
                bool useSsl = bool.TryParse(account["UseSsl"], out var ssl) ? ssl : true;
                string mailboxEmail = (account["Username"] ?? "").Trim();
                string password = account["Password"] ?? "";
                string targetFolder = account["TargetFolder"] ?? "INBOX";

                if (string.IsNullOrWhiteSpace(server) ||
                    string.IsNullOrWhiteSpace(mailboxEmail) ||
                    string.IsNullOrWhiteSpace(password))
                    continue;

                // ✅ CRITICAL FIX:
                // This worker must NEVER process the Quotes mailbox.
                if (mailboxEmail.Equals(QuotesMailbox, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"NexTraQ: Skipping QUOTES mailbox in RFQ worker -> {mailboxEmail}");
                    continue;
                }

                // This worker is strictly for LEADS mailbox only.
                if (!mailboxEmail.Equals(LeadsMailbox, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"NexTraQ: Skipping non-leads mailbox in RFQ worker -> {mailboxEmail}");
                    continue;
                }

                _logger.LogInformation($"NexTraQ: Connecting (LEADS only) -> {mailboxEmail}");
                await ProcessSingleLeadsMailboxAsync(server, port, useSsl, mailboxEmail, password, targetFolder, dbContext, emailParser, stoppingToken);
            }
        }

        private async Task ProcessSingleLeadsMailboxAsync(
            string server,
            int port,
            bool useSsl,
            string mailboxEmail,
            string password,
            string targetFolder,
            AppDbContext dbContext,
            EmailParsingService emailParser,
            CancellationToken stoppingToken)
        {
            using var client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            await client.ConnectAsync(server, port, useSsl, stoppingToken);
            await client.AuthenticateAsync(mailboxEmail, password, stoppingToken);

            var folder = client.GetFolder(targetFolder);
            await folder.OpenAsync(FolderAccess.ReadWrite, stoppingToken);

            var unreadUids = await folder.SearchAsync(SearchQuery.NotSeen, stoppingToken);

            foreach (var uid in unreadUids)
            {
                MimeMessage message;
                try
                {
                    message = await folder.GetMessageAsync(uid, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"NexTraQ: Failed reading message UID {uid} - {ex.Message}");
                    continue;
                }

                string senderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown@system.local";
                string subject = message.Subject ?? "No Subject";
                string plainTextBody = message.TextBody ?? message.HtmlBody ?? "";
                DateTime receivedAt = message.Date.UtcDateTime;

                bool isInternalRep =
                    senderEmail.EndsWith(_internalDomain, StringComparison.OrdinalIgnoreCase) ||
                    senderEmail.EndsWith(_alternateInternalDomain, StringComparison.OrdinalIgnoreCase);

                // Extra protection: If message was delivered-to QUOTES (alias/catch-all), skip here.
                if (IsDeliveredToQuotes(message))
                {
                    _logger.LogInformation($"NexTraQ: Skipping RFQ ingestion because message was delivered-to QUOTES. UID={uid}");
                    await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, stoppingToken);
                    continue;
                }

                // ✅ Postgres-safe dedup guard (no Abs/DateDiff)
                // If same sender + same subject exists within +/-10 minutes, skip.
                try
                {
                    var senderLower = senderEmail.ToLowerInvariant();
                    var minTime = receivedAt.AddMinutes(-10);
                    var maxTime = receivedAt.AddMinutes(10);

                    var dup = await dbContext.Rfqs.AsNoTracking().AnyAsync(r =>
                        r.ClientEmail != null &&
                        r.ClientEmail.ToLower() == senderLower &&
                        r.Subject == subject &&
                        r.ReceivedAt >= minTime &&
                        r.ReceivedAt <= maxTime, stoppingToken);

                    if (dup)
                    {
                        _logger.LogInformation($"NexTraQ: Duplicate RFQ ignored. Sender={senderEmail}, Subject={subject}");
                        await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, stoppingToken);
                        continue;
                    }
                }
                catch
                {
                    // never block ingestion
                }

                // Create RFQ from the forwarded body (existing logic)
                var rfq = emailParser.ExtractClientDetailsFromForward(senderEmail, subject, plainTextBody);
                rfq.ReceivedAt = receivedAt;

                // Assign RFQ to internal rep (if it came from internal sender)
                if (isInternalRep)
                {
                    var internalUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == senderEmail, stoppingToken);
                    if (internalUser != null)
                        rfq.AssignedUserId = internalUser.Id;
                }

                dbContext.Rfqs.Add(rfq);
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation($"NexTraQ: Routed new LEAD/RFQ from {senderEmail} | Subject={subject}");

                await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, stoppingToken);
            }

            await client.DisconnectAsync(true, stoppingToken);
        }

        private static bool IsDeliveredToQuotes(MimeMessage message)
        {
            if (message?.Headers == null) return false;

            var headerKeys = new[]
            {
                "Delivered-To",
                "X-Delivered-To",
                "X-Original-To",
                "X-Envelope-To",
                "Envelope-To"
            };

            foreach (var k in headerKeys)
            {
                foreach (var h in message.Headers.Where(x => x.Field.Equals(k, StringComparison.OrdinalIgnoreCase)))
                {
                    var v = h.Value ?? "";
                    if (v.IndexOf("quotes@nexcelservice.net", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (v.IndexOf("quotes@nexcel.me", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }

            var all = (message.To?.Mailboxes ?? Enumerable.Empty<MailboxAddress>())
                .Concat(message.Cc?.Mailboxes ?? Enumerable.Empty<MailboxAddress>())
                .Concat(message.Bcc?.Mailboxes ?? Enumerable.Empty<MailboxAddress>());

            foreach (var mb in all)
            {
                var addr = mb?.Address ?? "";
                if (addr.Equals("quotes@nexcelservice.net", StringComparison.OrdinalIgnoreCase)) return true;
                if (addr.Equals("quotes@nexcel.me", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
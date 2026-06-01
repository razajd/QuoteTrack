using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;
using QuoteTrack.Infrastructure.Logging;

namespace QuoteTrack.Web.BackgroundServices
{
    public class ImapLeadIngestionService : BackgroundService
    {
        private enum IngestionRoute { Lead = 0, Quote = 1 }

        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ImapLeadIngestionService> _logger;
        private readonly IWebHostEnvironment _env;

        private readonly string _attachmentsPhysicalRoot;
        private readonly string _attachmentsRelativeRoot;

        private static readonly string[] QuoteTargets =
        {
            "quotes@nexcelservice.net",
            "quotes@nexcel.me"
        };

        private static readonly string[] LeadTargets =
        {
            "leads@nexcelservice.net",
            "leads@nexcel.me",
            "lead@nexcel.me",
            "sales@nexcel.me"
        };

        // throttling to avoid mail spam from repeating cycle errors
        private static DateTime _lastCycleAlertUtc = DateTime.MinValue;
        private static string _lastCycleSignature = string.Empty;

        public ImapLeadIngestionService(
            IConfiguration config,
            IServiceScopeFactory scopeFactory,
            ILogger<ImapLeadIngestionService> logger,
            IWebHostEnvironment env)
        {
            _config = config;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _env = env;

            _attachmentsRelativeRoot = Path.Combine("Storage", "Attachments");
            _attachmentsPhysicalRoot = Path.Combine(_env.WebRootPath, _attachmentsRelativeRoot);

            if (!Directory.Exists(_attachmentsPhysicalRoot))
                Directory.CreateDirectory(_attachmentsPhysicalRoot);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IMAP Ingestion Service started.");
            SystemLogger.LogEvent("SUCCESS", "IMAP Listener", "Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    SystemLogger.LogHeartbeat("IMAP");
                    await CheckAllInboxesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    var details = BuildExceptionDetails(ex);
                    _logger.LogError(ex, "IMAP ingestion cycle error. {Details}", details);
                    ThrottledSystemError("IMAP Listener", $"Cycle error: {details}");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task CheckAllInboxesAsync(CancellationToken stoppingToken)
        {
            var imapAccounts = _config.GetSection("ImapAccounts").GetChildren();

            foreach (var account in imapAccounts)
            {
                var isActive = bool.TryParse(account["IsActive"], out var active) && active;
                if (!isActive) continue;

                var host = account["Host"] ?? string.Empty;
                var port = int.TryParse(account["Port"], out var parsedPort) ? parsedPort : 993;
                var useSsl = !bool.TryParse(account["UseSsl"], out var parsedUseSsl) || parsedUseSsl;

                var username = account["Username"] ?? string.Empty;
                var password = account["Password"] ?? string.Empty;

                var role = account["AccountRole"] ?? "Lead";
                var folderName = account["TargetFolder"] ?? "INBOX";
                var postProcessAction = account["PostProcessAction"] ?? "MarkRead"; // MarkRead | Delete | None

                if (string.IsNullOrWhiteSpace(host) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password))
                    continue;

                await ProcessAccountAsync(host, port, useSsl, username, password, role, folderName, postProcessAction, stoppingToken);
            }
        }

        private async Task ProcessAccountAsync(
            string host,
            int port,
            bool useSsl,
            string username,
            string password,
            string role,
            string folderName,
            string postProcessAction,
            CancellationToken stoppingToken)
        {
            using var client = new ImapClient();
            var secureOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

            await client.ConnectAsync(host, port, secureOptions, stoppingToken);
            await client.AuthenticateAsync(username, password, stoppingToken);

            var inbox = await client.GetFolderAsync(folderName, stoppingToken);
            await inbox.OpenAsync(FolderAccess.ReadWrite, stoppingToken);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, stoppingToken);
            if (uids.Count == 0)
            {
                await client.DisconnectAsync(true, stoppingToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var pdfExtractor = scope.ServiceProvider.GetRequiredService<IPdfQuotationExtractor>();
            var excelExtractor = scope.ServiceProvider.GetRequiredService<IExcelQuotationExtractor>();

            var mailboxUserLower = username.Trim().ToLowerInvariant();

            foreach (var uid in uids)
            {
                try
                {
                    MimeMessage message;
                    try
                    {
                        message = await inbox.GetMessageAsync(uid, stoppingToken);
                    }
                    catch (Exception exRead)
                    {
                        SystemLogger.LogEvent("ERROR", "IMAP Listener", $"Failed to read message UID={uid}: {BuildExceptionDetails(exRead)}");
                        await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                        continue;
                    }

                    var deliveredRecipients = GetDeliveredRecipients(message);
                    var deliveredToQuotes = deliveredRecipients.Overlaps(new HashSet<string>(QuoteTargets.Select(t => t.ToLowerInvariant())));
                    var deliveredToLeads = deliveredRecipients.Overlaps(new HashSet<string>(LeadTargets.Select(t => t.ToLowerInvariant())));

                    if (mailboxUserLower.Contains("leads@") && deliveredToQuotes)
                    {
                        SystemLogger.LogEvent("INFO", "IMAP Routing", $"Skipped ingestion in LEADS mailbox (message delivered to QUOTES). UID={uid}");
                        await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                        continue;
                    }

                    if (mailboxUserLower.Contains("quotes@") && deliveredToLeads)
                    {
                        SystemLogger.LogEvent("INFO", "IMAP Routing", $"Skipped ingestion in QUOTES mailbox (message delivered to LEADS). UID={uid}");
                        await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                        continue;
                    }

                    var originalSenderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? "";
                    var originalSenderName = message.From.Mailboxes.FirstOrDefault()?.Name ?? originalSenderEmail;
                    var ownerCandidateEmail = GetBestOwnerCandidateEmail(message) ?? originalSenderEmail;

                    var subject = message.Subject ?? "No Subject";
                    var rawMessageId = message.MessageId ?? string.Empty;

                    var route = ResolveRoute(message, role, username, folderName, deliveredRecipients);
                    var isQuoteInbox = route == IngestionRoute.Quote;

                    var recordType = isQuoteInbox ? QuoteRecordType.OutgoingQuote : QuoteRecordType.Lead;
var status = isQuoteInbox ? QuoteStatus.Sent : QuoteStatus.LeadNew;;

                    var bodyTextForParsing = ExtractBodyForParsing(message);
                    var bodyTextForUI = ExtractBodyForUI(message);

                    var clientEmail = originalSenderEmail;
                    var clientDisplayName = originalSenderName;

                    if (!isQuoteInbox && IsInternalNexcelEmail(originalSenderEmail))
                    {
                        if (TryExtractForwardedExternalContact(bodyTextForParsing, out var extEmail, out var extName))
                        {
                            clientEmail = extEmail;
                            clientDisplayName = string.IsNullOrWhiteSpace(extName) ? extEmail : extName;
                            SystemLogger.LogEvent("INFO", "Lead Normalization", $"Forwarded lead. Internal sender={originalSenderEmail}. Extracted external client={extEmail}");
                        }
                        else
                        {
                            SystemLogger.LogEvent("WARNING", "Lead Normalization", $"Forwarded lead from internal sender={originalSenderEmail}. Could not extract external client.");
                        }
                    }

                    var dedupKey = BuildDedupKey(rawMessageId, message, clientEmail, subject);
                    var receivedUtc = EnsureUtc(message.Date.UtcDateTime);

                    try
                    {
                        var alreadyExists = await db.Quotes.AsNoTracking().AnyAsync(q =>
                            (!string.IsNullOrWhiteSpace(q.EmailMessageId) && q.EmailMessageId == dedupKey) ||
                            (!string.IsNullOrWhiteSpace(rawMessageId) && q.EmailMessageId == rawMessageId) ||
                            (q.EmailReceivedDateTime == receivedUtc &&
                             q.SenderEmail == clientEmail &&
                             q.Subject == subject), stoppingToken);

                        if (alreadyExists)
                        {
                            SystemLogger.LogEvent("INFO", "IMAP Listener",
                                $"Duplicate ignored. UID={uid}, Route={(isQuoteInbox ? "Quote" : "Lead")}, MsgId='{rawMessageId}', DedupKey='{dedupKey}'");

                            await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                            continue;
                        }
                    }
                    catch { }

                    var nowUtc = DateTime.UtcNow;

                    // Save original .eml
                    var emlUnique = $"Original_Email_{uid}_{Guid.NewGuid():N}.eml";
                    var emlPhysicalPath = Path.Combine(_attachmentsPhysicalRoot, emlUnique);
                    await message.WriteToAsync(emlPhysicalPath, stoppingToken);

                    var emlRelativePath = Path.Combine(_attachmentsRelativeRoot, emlUnique).Replace("\\", "/");

                    var attachments = new List<Attachment>
                    {
                        new Attachment
                        {
                            Id = Guid.NewGuid(),
                            FileName = "Original_Email_Thread.eml",
                            StoragePath = emlRelativePath,
                            ContentType = "message/rfc822",
                            FileSize = new FileInfo(emlPhysicalPath).Length,
                            FileHash = ComputeSha256File(emlPhysicalPath),
                            CreatedAt = nowUtc
                        }
                    };

                    var savedPhysicalFiles = new List<string>();

                    foreach (var att in message.Attachments)
                    {
                        string originalFileName = "Unnamed_File";

                        if (att.ContentDisposition != null && !string.IsNullOrEmpty(att.ContentDisposition.FileName))
                            originalFileName = att.ContentDisposition.FileName;
                        else if (att.ContentType != null && !string.IsNullOrEmpty(att.ContentType.Name))
                            originalFileName = att.ContentType.Name;

                        var safeName = string.Join("_", originalFileName.Split(Path.GetInvalidFileNameChars()));
                        var uniqueFileName = $"{Guid.NewGuid():N}_{safeName}";
                        var physicalPath = Path.Combine(_attachmentsPhysicalRoot, uniqueFileName);

                        using (var stream = File.Create(physicalPath))
                        {
                            if (att is MessagePart mp && mp.Message != null)
                                await mp.Message.WriteToAsync(stream, stoppingToken);
                            else if (att is MimePart mime && mime.Content != null)
                                await mime.Content.DecodeToAsync(stream, stoppingToken);
                        }

                        savedPhysicalFiles.Add(physicalPath);

                        var relativePath = Path.Combine(_attachmentsRelativeRoot, uniqueFileName).Replace("\\", "/");
                        var mimeType = att.ContentType?.MimeType ?? "application/octet-stream";

                        attachments.Add(new Attachment
                        {
                            Id = Guid.NewGuid(),
                            FileName = originalFileName,
                            StoragePath = relativePath,
                            ContentType = mimeType,
                            FileSize = new FileInfo(physicalPath).Length,
                            FileHash = ComputeSha256File(physicalPath),
                            CreatedAt = nowUtc
                        });
                    }

                    var storedMessageId = !string.IsNullOrWhiteSpace(rawMessageId) ? rawMessageId : dedupKey;

                    var newRecord = new Quote
                    {
                        RecordType = recordType,
                        Status = status,

                        SenderEmail = clientEmail,
                        SenderName = clientDisplayName,
                        Subject = subject,
                        EmailMessageId = storedMessageId,

                        EmailReceivedDateTime = receivedUtc,
                        EmailSentDateTime = receivedUtc,

                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc,

                        NextFollowUpDate = isQuoteInbox ? receivedUtc.AddDays(2) : (DateTime?)null,

                        LeadSource = isQuoteInbox ? "Outgoing Quotation" : "Website Inquiry",
                        SolutionSummary = bodyTextForUI,
                        Attachments = attachments
                    };

                    try
                    {
                        var ownerUser = await FindUserByEmailOrUsernameAsync(db, ownerCandidateEmail, stoppingToken);
                        if (ownerUser != null)
                            newRecord.OwnerId = ownerUser.Id;
                    }
                    catch { }

                    newRecord.ClientName = (!string.IsNullOrWhiteSpace(clientDisplayName) && !clientDisplayName.Contains("@"))
                        ? clientDisplayName
                        : clientEmail;

                    if (isQuoteInbox)
                    {
                        var pdfPath = savedPhysicalFiles.FirstOrDefault(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
                        var xlsPath = savedPhysicalFiles.FirstOrDefault(p =>
                            p.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(pdfPath))
                                await pdfExtractor.ExtractAsync(pdfPath, newRecord);
                            else if (!string.IsNullOrWhiteSpace(xlsPath))
                                await excelExtractor.ExtractAsync(xlsPath, newRecord);
                        }
                        catch (Exception exExtract)
                        {
                            SystemLogger.LogEvent("WARNING", "Quote Extraction", $"Extraction failed UID={uid}: {BuildExceptionDetails(exExtract)}");
                        }

                        // ✅ FIXED: Auto-link by exact normalized match (DB prefilter, in-memory final match)
                        await TryAttachExistingClientAsync(db, newRecord, stoppingToken);

                        SystemLogger.LogEvent("SUCCESS", "Quote Ingestion",
                            $"Quote ingested: UID={uid}, Subj='{subject}', Client='{newRecord.ClientName}', ClientId='{newRecord.ClientId}', Ref='{newRecord.QuoteReference}', Value='{newRecord.QuoteValue} {newRecord.Currency}'");
                    }
                    else
                    {
                        SystemLogger.LogEvent("SUCCESS", "Lead Ingestion",
                            $"Lead ingested: UID={uid}, Subj='{subject}', Client='{newRecord.ClientName}', Email='{clientEmail}'");
                    }

                    db.Quotes.Add(newRecord);

                    try
                    {
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception exSave)
                    {
                        var details = BuildExceptionDetails(exSave);
                        SystemLogger.LogEvent("ERROR", "IMAP Save",
                            $"Save failed: UID={uid}, Route={(isQuoteInbox ? "Quote" : "Lead")}, MsgId='{rawMessageId}', Sender='{clientEmail}', Subj='{subject}', Err={details}");

                        _logger.LogError(exSave,
                            "IMAP Save failed. UID={Uid}, Route={Route}, MsgId={MsgId}, Sender={Sender}, Subject={Subject}. Details={Details}",
                            uid, (isQuoteInbox ? "Quote" : "Lead"), rawMessageId, clientEmail, subject, details);

                        await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                        continue;
                    }

                    await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                }
                catch (Exception ex)
                {
                    SystemLogger.LogEvent("ERROR", "IMAP Listener", $"Message cycle failure UID={uid}: {BuildExceptionDetails(ex)}");
                    _logger.LogError(ex, "Message cycle failure UID={Uid}.", uid);
                    await ApplyPostProcessAsync(inbox, uid, postProcessAction, stoppingToken);
                }
            }

            await client.DisconnectAsync(true, stoppingToken);
        }

        /// <summary>
        /// ✅ Auto-link Quote.ClientId if an existing client CompanyName matches "exact" normalized name.
        /// EF translation safe: DB prefilter uses ILIKE, final exact match occurs in-memory.
        /// </summary>
        private static async Task TryAttachExistingClientAsync(IAppDbContext db, Quote quote, CancellationToken ct)
        {
            if (quote == null) return;
            if (quote.ClientId.HasValue) return;

            var name = (quote.ClientName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var normalized = NormalizeCompanyNameExact(name);

            // DB pre-filter (case-insensitive exact match) using Postgres ILIKE
            // This is translatable by EF Core Npgsql.
            var candidates = await db.Clients
                .AsNoTracking()
                .Where(c => c.CompanyName != null && EF.Functions.ILike(c.CompanyName, name))
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return;

            // Final exact match in-memory with your normalization rules
            var exact = candidates.FirstOrDefault(c => NormalizeCompanyNameExact(c.CompanyName) == normalized);
            if (exact != null)
            {
                quote.ClientId = exact.Id;
                quote.ClientName = exact.CompanyName;

                SystemLogger.LogEvent("INFO", "Client AutoLink",
                    $"Auto-linked client by exact name. QuoteId={quote.Id}, Client='{exact.CompanyName}', ClientId={exact.Id}");
            }
        }

        private static string NormalizeCompanyNameExact(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var x = s.Trim();
            x = Regex.Replace(x, @"\s+", " ");
            x = x.ToUpperInvariant();
            return x;
        }

        private static async Task ApplyPostProcessAsync(IMailFolder inbox, UniqueId uid, string action, CancellationToken ct)
        {
            action = (action ?? "MarkRead").Trim();

            if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
            {
                await inbox.AddFlagsAsync(uid, MessageFlags.Deleted, true, ct);
                await inbox.ExpungeAsync(ct);
                return;
            }

            if (action.Equals("None", StringComparison.OrdinalIgnoreCase))
                return;

            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
        }

        private static HashSet<string> GetDeliveredRecipients(MimeMessage message)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (message?.Headers == null) return result;

            var keys = new[] { "Delivered-To", "X-Delivered-To", "X-Original-To", "X-Envelope-To", "Envelope-To" };

            foreach (var k in keys)
            {
                var values = message.Headers
                    .Where(h => h.Field.Equals(k, StringComparison.OrdinalIgnoreCase))
                    .Select(h => h.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v));

                foreach (var v in values)
                {
                    foreach (Match m in Regex.Matches(v, @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase))
                        result.Add(m.Value.Trim().ToLowerInvariant());
                }
            }

            foreach (var mb in (message.To?.Mailboxes ?? Enumerable.Empty<MailboxAddress>())
                .Concat(message.Cc?.Mailboxes ?? Enumerable.Empty<MailboxAddress>())
                .Concat(message.Bcc?.Mailboxes ?? Enumerable.Empty<MailboxAddress>()))
            {
                if (!string.IsNullOrWhiteSpace(mb?.Address))
                    result.Add(mb.Address.Trim().ToLowerInvariant());
            }

            return result;
        }

        private static void ThrottledSystemError(string source, string message)
        {
            var now = DateTime.UtcNow;
            var sig = (message ?? "").Trim();
            if (sig.Length > 300) sig = sig.Substring(0, 300);

            if (sig != _lastCycleSignature || (now - _lastCycleAlertUtc) > TimeSpan.FromMinutes(30))
            {
                _lastCycleSignature = sig;
                _lastCycleAlertUtc = now;
                SystemLogger.LogEvent("ERROR", source, message ?? "");
            }
            else
            {
                SystemLogger.LogEvent("WARN", source, "IMAP cycle error repeated (suppressed).");
            }
        }

        private static string BuildExceptionDetails(Exception ex)
        {
            var parts = new List<string>();
            int depth = 0;
            Exception? cur = ex;

            while (cur != null && depth < 10)
            {
                var msg = (cur.Message ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                parts.Add($"[{depth}] {cur.GetType().Name}: {msg}");
                cur = cur.InnerException;
                depth++;
            }

            return string.Join(" | ", parts);
        }

        private static DateTime EnsureUtc(DateTime dt)
        {
            if (dt == DateTime.MinValue) return DateTime.UtcNow;
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        private bool IsInternalNexcelEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var e = email.Trim().ToLowerInvariant();
            var at = e.LastIndexOf('@');
            if (at < 0) return false;
            var domain = e.Substring(at + 1);
            return domain.Contains("nexcel");
        }

        private bool TryExtractForwardedExternalContact(string body, out string email, out string name)
        {
            email = "";
            name = "";

            if (string.IsNullOrWhiteSpace(body))
                return false;

            var fromMatch = Regex.Match(body, @"(?im)^\s*From\s*:\s*(.+)$");
            if (fromMatch.Success)
            {
                var line = fromMatch.Groups[1].Value.Trim();
                var em = Regex.Match(line, @"([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})", RegexOptions.IgnoreCase);
                if (em.Success)
                {
                    var candidate = em.Groups[1].Value.Trim();
                    if (!IsInternalNexcelEmail(candidate))
                    {
                        email = candidate;

                        var nm = Regex.Replace(line, @"<\s*[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\s*>", "", RegexOptions.IgnoreCase).Trim();
                        nm = Regex.Replace(nm, @"[\""<>]", "").Trim();
                        name = nm;

                        return true;
                    }
                }
            }

            var matches = Regex.Matches(body, @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var candidate = m.Value.Trim();
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (IsInternalNexcelEmail(candidate)) continue;

                email = candidate;
                return true;
            }

            return false;
        }

        private string ExtractBodyForUI(MimeMessage message)
        {
            var text = message?.TextBody ?? "";
            var html = message?.HtmlBody ?? "";

            var htmlStripped = "";
            if (!string.IsNullOrWhiteSpace(html))
            {
                htmlStripped = Regex.Replace(html, "<.*?>", " ");
                htmlStripped = Regex.Replace(htmlStripped, @"\s+", " ").Trim();
            }

            var combined = (text + "\n\n" + htmlStripped).Trim();
            combined = Regex.Replace(combined, @"\[(cid:[^\]]+)\]", "", RegexOptions.IgnoreCase);
            combined = Regex.Replace(combined, @"\s{3,}", " ");

            if (string.IsNullOrWhiteSpace(combined))
                combined = "No readable email body found. Please open Original_Email_Thread.eml attachment.";

            if (combined.Length > 10000)
                combined = combined.Substring(0, 10000) + "\n\n...[TRUNCATED]";

            return combined;
        }

        private string ExtractBodyForParsing(MimeMessage message) => ExtractBodyForUI(message);

        private static string? GetBestOwnerCandidateEmail(MimeMessage message)
        {
            var sender = message?.Sender?.Address;
            if (!string.IsNullOrWhiteSpace(sender)) return NormalizeEmail(sender);

            var from = message?.From?.Mailboxes?.FirstOrDefault()?.Address;
            if (!string.IsNullOrWhiteSpace(from)) return NormalizeEmail(from);

            var reply = message?.ReplyTo?.Mailboxes?.FirstOrDefault()?.Address;
            if (!string.IsNullOrWhiteSpace(reply)) return NormalizeEmail(reply);

            return null;
        }

        private static string NormalizeEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var v = value.Trim().Trim('<', '>').Trim();
            return v.ToLowerInvariant();
        }

        private static async Task<ApplicationUser?> FindUserByEmailOrUsernameAsync(IAppDbContext db, string? emailOrUser, CancellationToken ct)
        {
            var key = NormalizeEmail(emailOrUser);
            if (string.IsNullOrWhiteSpace(key)) return null;

            return await db.Users.AsNoTracking().FirstOrDefaultAsync(u =>
                (u.Email != null && u.Email.ToLower() == key) ||
                (u.UserName != null && u.UserName.ToLower() == key) ||
                (u.NormalizedEmail != null && u.NormalizedEmail.ToLower() == key) ||
                (u.NormalizedUserName != null && u.NormalizedUserName.ToLower() == key), ct);
        }

        private IngestionRoute ResolveRoute(MimeMessage message, string role, string username, string folderName, HashSet<string> deliveredRecipients)
        {
            var userLower = (username ?? "").Trim().ToLowerInvariant();
            var folderLower = (folderName ?? "").Trim().ToLowerInvariant();

            if (userLower.Contains("quotes@") || folderLower.Contains("quotes"))
                return IngestionRoute.Quote;

            if (userLower.Contains("leads@") || userLower.Contains("lead@") || folderLower.Contains("leads"))
                return IngestionRoute.Lead;

            if (deliveredRecipients != null)
            {
                if (deliveredRecipients.Overlaps(QuoteTargets.Select(t => t.ToLowerInvariant())))
                    return IngestionRoute.Quote;

                if (deliveredRecipients.Overlaps(LeadTargets.Select(t => t.ToLowerInvariant())))
                    return IngestionRoute.Lead;
            }

            if (!string.IsNullOrWhiteSpace(role) && role.Equals("Quote", StringComparison.OrdinalIgnoreCase))
                return IngestionRoute.Quote;

            return IngestionRoute.Lead;
        }

        private static string BuildDedupKey(string rawMessageId, MimeMessage message, string senderEmail, string subject)
        {
            if (!string.IsNullOrWhiteSpace(rawMessageId))
                return rawMessageId.Trim();

            var date = message?.Date.UtcDateTime.ToString("O") ?? "";
            var from = NormalizeEmail(senderEmail);
            var subj = subject ?? "";

            long totalSize = 0;
            int attCount = 0;
            try
            {
                foreach (var att in message?.Attachments ?? Enumerable.Empty<MimeEntity>())
                {
                    attCount++;
                    if (att is MimePart mp && mp.ContentDisposition != null && mp.ContentDisposition.Size.HasValue)
                        totalSize += mp.ContentDisposition.Size.Value;
                }
            }
            catch { }

            var input = $"{date}|{from}|{subj}|att={attCount}|sz={totalSize}";
            return $"synthetic:{Sha256Hex(input)}";
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string ComputeSha256File(string physicalPath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(physicalPath);
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
// QuoteTrack.Infrastructure/Email/EmailIngestionService.cs
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;
using QuoteTrack.Infrastructure.Data;
using System.Collections.Generic;

namespace QuoteTrack.Infrastructure.Email
{
    public class EmailIngestionService : IEmailIngestionService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<EmailIngestionService> _logger;
        private readonly IPdfQuotationExtractor _pdfExtractor;
        private readonly IExcelQuotationExtractor _excelExtractor;
        private readonly string _settingsPath = "appsettings.custom.json";

        public EmailIngestionService(
            AppDbContext dbContext,
            ILogger<EmailIngestionService> logger,
            IPdfQuotationExtractor pdfExtractor,
            IExcelQuotationExtractor excelExtractor)
        {
            _dbContext = dbContext;
            _logger = logger;
            _pdfExtractor = pdfExtractor;
            _excelExtractor = excelExtractor;
        }

        private class CustomConfig
        {
            public string DefaultLeadOwnerId { get; set; } = "";
            public List<ImapAccountDto> ImapAccounts { get; set; } = new();
        }

        private class ImapAccountDto
        {
            public string Host { get; set; } = "";
            public int Port { get; set; }
            public bool UseSsl { get; set; }
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public bool IsActive { get; set; }
            public string TargetFolder { get; set; } = "INBOX";
            public string PostProcessAction { get; set; } = "MarkRead";
            public string AccountRole { get; set; } = "Lead";
        }

        public async Task ProcessNewEmailsAsync(CancellationToken cancellationToken)
        {
            QuoteTrack.Infrastructure.Logging.SystemLogger.LogHeartbeat("IMAP");

            if (!File.Exists(_settingsPath))
                return;

            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
            var config = JsonSerializer.Deserialize<CustomConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config == null || !config.ImapAccounts.Any(a => a.IsActive))
                return;

            foreach (var account in config.ImapAccounts.Where(a =>
                         a.IsActive &&
                         a.AccountRole.Equals("Quote", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(account.Host) || string.IsNullOrWhiteSpace(account.Username))
                    continue;

                await ProcessSingleMailboxAsync(account, config.DefaultLeadOwnerId, cancellationToken);
            }
        }

        private async Task ProcessSingleMailboxAsync(
            ImapAccountDto account,
            string defaultOwnerId,
            CancellationToken cancellationToken)
        {
            var storageBasePath = @"C:\QuoteTrack\Data\Attachments";

            using var client = new ImapClient();
            try
            {
                await client.ConnectAsync(account.Host, account.Port, account.UseSsl, cancellationToken);
                await client.AuthenticateAsync(account.Username, account.Password, cancellationToken);

                var folderName = string.IsNullOrWhiteSpace(account.TargetFolder) ? "INBOX" : account.TargetFolder;
                var targetFolder = await client.GetFolderAsync(folderName, cancellationToken);
                await targetFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

                var uids = await targetFolder.SearchAsync(SearchQuery.NotSeen, cancellationToken);

                foreach (var uid in uids)
                {
                    var message = await targetFolder.GetMessageAsync(uid, cancellationToken);

                    var messageKey = BuildMessageKey(message, uid);
                    var rawMessageId = message.MessageId ?? string.Empty;

                    var senderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? "Unknown Email";
                    var subject = message.Subject ?? "No Subject";

                    if (subject.Contains("Automatic reply:", StringComparison.OrdinalIgnoreCase) ||
                        subject.Contains("Out of Office", StringComparison.OrdinalIgnoreCase))
                    {
                        var existingQuote = await _dbContext.Quotes
                            .Where(q => q.SenderEmail.ToLower() == senderEmail.ToLower() &&
                                        q.Status != QuoteStatus.Won &&
                                        q.Status != QuoteStatus.Lost &&
                                        q.Status != QuoteStatus.Cancelled)
                            .OrderByDescending(q => q.CreatedAt)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (existingQuote != null)
                        {
                            existingQuote.NextFollowUpDate = DateTime.UtcNow.AddDays(7);
                            var safeDueDate = existingQuote.NextFollowUpDate ?? DateTime.UtcNow.AddDays(7);

                            var fu = new FollowUp
                            {
                                Id = Guid.NewGuid(),
                                QuoteId = existingQuote.Id,
                                DueDate = safeDueDate,
                                Notes = "SYSTEM AUTO-SNOOZE: Received Out of Office reply.",
                                CreatedAt = DateTime.UtcNow
                            };

                            _dbContext.FollowUps.Add(fu);
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            QuoteTrack.Infrastructure.Logging.SystemLogger.LogEvent(
                                "INFO",
                                "IMAP Auto-Snooze",
                                $"Snoozed quote {existingQuote.QuoteReference} due to OOO reply from {senderEmail}.");

                            await ApplyPostProcessAction(targetFolder, uid, account.PostProcessAction, cancellationToken);
                            continue;
                        }
                    }

                    if (await _dbContext.Quotes.AnyAsync(
                            q => q.EmailMessageId == messageKey ||
                                 (!string.IsNullOrWhiteSpace(rawMessageId) && q.EmailMessageId == rawMessageId),
                            cancellationToken))
                    {
                        await ApplyPostProcessAction(targetFolder, uid, account.PostProcessAction, cancellationToken);
                        continue;
                    }

                    var attachments = message.Attachments
                        .OfType<MimePart>()
                        .Where(a => a.FileName != null &&
                                    (a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                                     a.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                                     a.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (!attachments.Any())
                    {
                        await ApplyPostProcessAction(targetFolder, uid, account.PostProcessAction, cancellationToken);
                        continue;
                    }

                    var assignmentCandidatesLower = new List<string>();

                    void AddCandidate(string? value)
                    {
                        if (string.IsNullOrWhiteSpace(value))
                            return;

                        var v = value.Trim();
                        if (v.StartsWith("<") && v.EndsWith(">"))
                            v = v.Trim('<', '>');

                        v = v.Trim().ToLowerInvariant();

                        if (string.IsNullOrWhiteSpace(v))
                            return;

                        if (!assignmentCandidatesLower.Contains(v))
                            assignmentCandidatesLower.Add(v);
                    }

                    AddCandidate(message.From.Mailboxes.FirstOrDefault()?.Address);
                    AddCandidate(message.Sender?.Address);
                    AddCandidate(message.ReplyTo.Mailboxes.FirstOrDefault()?.Address);

                    ApplicationUser? matchingUser = null;

                    foreach (var candidate in assignmentCandidatesLower)
                    {
                        matchingUser = await _dbContext.Users.FirstOrDefaultAsync(
                            u => (u.Email != null && u.Email.ToLower() == candidate) ||
                                 (u.UserName != null && u.UserName.ToLower() == candidate),
                            cancellationToken);

                        if (matchingUser != null)
                            break;

                        var candidateUpper = candidate.ToUpperInvariant();

                        matchingUser = await _dbContext.Users.FirstOrDefaultAsync(
                            u => (u.NormalizedEmail != null && u.NormalizedEmail == candidateUpper) ||
                                 (u.NormalizedUserName != null && u.NormalizedUserName == candidateUpper),
                            cancellationToken);

                        if (matchingUser != null)
                            break;
                    }

                    var finalOwnerId = matchingUser?.Id ?? (string.IsNullOrWhiteSpace(defaultOwnerId) ? null : defaultOwnerId);

                    var quoteSentUtc = message.Date != DateTimeOffset.MinValue
                        ? message.Date.UtcDateTime
                        : DateTime.UtcNow;

                    var quote = new Quote
                    {
                        Id = Guid.NewGuid(),
                        EmailMessageId = messageKey,
                        Subject = subject,
                        EmailSentDateTime = quoteSentUtc,
                        EmailReceivedDateTime = DateTime.UtcNow,
                        SenderName = message.From.Mailboxes.FirstOrDefault()?.Name ?? "Unknown Sender",
                        SenderEmail = senderEmail,
                        RecordType = QuoteRecordType.OutgoingQuote,
                        Status = QuoteStatus.Sent,
                        NextFollowUpDate = quoteSentUtc.AddDays(2),
                        OwnerId = finalOwnerId,
                        LeadSource = "Outgoing Quotation"
                    };

                    _dbContext.Quotes.Add(quote);

                    var yearMonthPath = Path.Combine(
                        storageBasePath,
                        DateTime.UtcNow.ToString("yyyy"),
                        DateTime.UtcNow.ToString("MM"));

                    Directory.CreateDirectory(yearMonthPath);

                    try
                    {
                        var emlFileName = $"{Guid.NewGuid()}_original.eml";
                        var emlFullPath = Path.Combine(yearMonthPath, emlFileName);

                        using (var emlStream = File.Create(emlFullPath))
                        {
                            await message.WriteToAsync(emlStream, cancellationToken);
                        }

                        var emlAttachment = new Attachment
                        {
                            Id = Guid.NewGuid(),
                            QuoteId = quote.Id,
                            FileName = "original.eml",
                            ContentType = "message/rfc822",
                            FileSize = new FileInfo(emlFullPath).Length,
                            StoragePath = emlFullPath,
                            FileHash = CalculateFileHash(emlFullPath)
                        };

                        _dbContext.Attachments.Add(emlAttachment);
                    }
                    catch
                    {
                        // fail-safe: do not block ingestion if .eml cannot be saved
                    }

                    foreach (var attachment in attachments)
                    {
                        var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(attachment.FileName ?? "unknown.dat")}";
                        var fullPath = Path.Combine(yearMonthPath, safeFileName);

                        using (var stream = File.Create(fullPath))
                        {
                            if (attachment.Content is MimeContent content)
                                content.DecodeTo(stream);
                        }

                        var dbAttachment = new Attachment
                        {
                            Id = Guid.NewGuid(),
                            QuoteId = quote.Id,
                            FileName = attachment.FileName ?? "unknown.dat",
                            ContentType = attachment.ContentType?.MimeType ?? "application/octet-stream",
                            FileSize = new FileInfo(fullPath).Length,
                            StoragePath = fullPath,
                            FileHash = CalculateFileHash(fullPath)
                        };

                        _dbContext.Attachments.Add(dbAttachment);

                        if (fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            await _pdfExtractor.ExtractAsync(fullPath, quote);
                        else if (fullPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                                 fullPath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                            await _excelExtractor.ExtractAsync(fullPath, quote);
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await ApplyPostProcessAction(targetFolder, uid, account.PostProcessAction, cancellationToken);

                    QuoteTrack.Infrastructure.Logging.SystemLogger.LogEvent(
                        "SUCCESS",
                        "IMAP Ingestion",
                        $"Processed quote from {quote.SenderEmail}.");
                }

                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                QuoteTrack.Infrastructure.Logging.SystemLogger.LogEvent(
                    "ERROR",
                    $"IMAP Worker ({account.Username})",
                    ex.Message);
            }
        }

        private async Task ApplyPostProcessAction(
            IMailFolder folder,
            UniqueId uid,
            string action,
            CancellationToken token)
        {
            if (action == "Delete")
            {
                await folder.AddFlagsAsync(uid, MessageFlags.Deleted, true, token);
                await folder.ExpungeAsync(token);
            }
            else
            {
                await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, token);
            }
        }

        private string CalculateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string BuildMessageKey(MimeMessage message, UniqueId uid)
        {
            var raw = message?.MessageId;
            if (!string.IsNullOrWhiteSpace(raw))
                return raw.Trim();

            var date = message?.Date.UtcDateTime.ToString("O") ?? string.Empty;
            var from = (message?.From?.Mailboxes?.FirstOrDefault()?.Address ?? string.Empty).Trim().ToLowerInvariant();
            var subj = message?.Subject ?? string.Empty;

            int attCount = 0;
            long totalSize = 0;

            try
            {
                foreach (var att in message?.Attachments ?? Enumerable.Empty<MimeEntity>())
                {
                    attCount++;

                    if (att is MimePart mp &&
                        mp.ContentDisposition != null &&
                        mp.ContentDisposition.Size.HasValue)
                    {
                        totalSize += mp.ContentDisposition.Size.Value;
                    }
                }
            }
            catch
            {
                // ignore
            }

            var input = $"{date}|{from}|{subj}|att={attCount}|sz={totalSize}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            return $"synthetic:{hex}";
        }
    }
}
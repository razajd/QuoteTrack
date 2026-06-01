// QuoteTrack.Worker/Workers/ReminderWorker.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Infrastructure.Data;
using QuoteTrack.Domain.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using System.Text;
using System.Collections.Generic;

namespace QuoteTrack.Worker.Workers
{
    public class ReminderWorker : BackgroundService
    {
        private readonly ILogger<ReminderWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _settingsPath = "appsettings.custom.json";

        public ReminderWorker(ILogger<ReminderWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // Lightweight DTOs to read the UI settings
        private class CustomConfig
        {
            public int ReminderIntervalHours { get; set; } = 12;
            public List<SmtpAccountDto> SmtpAccounts { get; set; } = new();
        }
        private class SmtpAccountDto
        {
            public string Host { get; set; } = ""; public int Port { get; set; }
            public bool UseSsl { get; set; }
            public string Username { get; set; } = ""; public string Password { get; set; } = "";
            public string SenderName { get; set; } = ""; public string SenderEmail { get; set; } = "";
            public bool IsActive { get; set; }
        }

        private CustomConfig GetConfig()
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<CustomConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CustomConfig();
            }
            return new CustomConfig();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("--- REMINDER WORKER INITIALIZED ---");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var config = GetConfig();

                try
                {
                    _logger.LogInformation(">>> ReminderWorker: Scanning database for pending Follow-Ups...");
                    await ProcessRemindersAsync(config, stoppingToken);
                }
                catch (Exception ex)
                {
                    QuoteTrack.Infrastructure.Logging.SystemLogger.LogEvent("ERROR", "Reminder Worker", ex.Message);
                    _logger.LogError(ex, "ERROR during reminder processing loop.");
                }

                // Wait for the exact interval specified in the Control Panel!
                var interval = config.ReminderIntervalHours > 0 ? config.ReminderIntervalHours : 12;
                await Task.Delay(TimeSpan.FromHours(interval), stoppingToken);
            }
        }

        private async Task ProcessRemindersAsync(CustomConfig config, CancellationToken stoppingToken)
        {
            QuoteTrack.Infrastructure.Logging.SystemLogger.LogHeartbeat("SMTP");
            var activeSmtp = config.SmtpAccounts.FirstOrDefault(a => a.IsActive);
            if (activeSmtp == null || string.IsNullOrWhiteSpace(activeSmtp.Host))
            {
                _logger.LogWarning("No active SMTP account found in Settings. Skipping email reminders.");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var today = DateTime.UtcNow.Date;

            var dueQuotes = await db.Quotes
                .Include(q => q.Owner)
                .Where(q => q.NextFollowUpDate.HasValue && q.NextFollowUpDate.Value.Date <= today && q.Status != QuoteStatus.Won && q.Status != QuoteStatus.Lost && q.Status != QuoteStatus.Cancelled)
                .ToListAsync(stoppingToken);

            if (!dueQuotes.Any()) return;

            var assignedQuotes = dueQuotes.Where(q => q.Owner != null).GroupBy(q => q.Owner!);

            foreach (var group in assignedQuotes)
            {
                var owner = group.Key;
                await SendSummaryEmailAsync(activeSmtp, owner.Email ?? "", owner.FullName, group.ToList(), stoppingToken);
            }

            var unassignedQuotes = dueQuotes.Where(q => q.Owner == null).ToList();
            if (unassignedQuotes.Any())
            {
                await SendSummaryEmailAsync(activeSmtp, "admin@nexcel.me", "System Administrator", unassignedQuotes, stoppingToken);
            }
        }

        private async Task SendSummaryEmailAsync(SmtpAccountDto smtp, string toEmail, string toName, List<QuoteTrack.Domain.Entities.Quote> quotes, CancellationToken stoppingToken)
        {
            if (string.IsNullOrEmpty(toEmail)) return;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp.SenderName, string.IsNullOrWhiteSpace(smtp.SenderEmail) ? smtp.Username : smtp.SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = $"Action Required: {quotes.Count} Quotes Need Follow-Up Today";

            var sb = new StringBuilder();
            sb.AppendLine($"<h2 style='color:#fca311; font-family:sans-serif;'>Nexcel QuoteTrack Alert</h2>");
            sb.AppendLine($"<p style='font-family:sans-serif; color:#333;'>Hello <strong>{toName}</strong>,</p>");
            sb.AppendLine($"<p style='font-family:sans-serif; color:#333;'>You have <strong>{quotes.Count}</strong> quotes that require immediate follow-up.</p>");
            sb.AppendLine("<table style='width: 100%; border-collapse: collapse; font-family:sans-serif;'>");
            sb.AppendLine("<tr style='background-color: #3a3a3a; color: white; text-align: left;'>");
            sb.AppendLine("<th style='padding: 10px; border: 1px solid #ddd;'>Client</th><th style='padding: 10px; border: 1px solid #ddd;'>Reference</th><th style='padding: 10px; border: 1px solid #ddd;'>Status</th></tr>");

            foreach (var q in quotes)
            {
                var clientName = string.IsNullOrWhiteSpace(q.ClientName) ? "Unknown" : q.ClientName;
                var reference = string.IsNullOrWhiteSpace(q.QuoteReference) ? "N/A" : q.QuoteReference;
                sb.AppendLine($"<tr><td style='padding: 10px; border: 1px solid #ddd; font-weight:bold;'>{clientName}</td><td style='padding: 10px; border: 1px solid #ddd;'>{reference}</td><td style='padding: 10px; border: 1px solid #ddd;'>{q.Status}</td></tr>");
            }

            sb.AppendLine("</table>");
            message.Body = new TextPart(TextFormat.Html) { Text = sb.ToString() };

            using var smtpClient = new SmtpClient();
            try
            {
                var secureSocketOptions = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await smtpClient.ConnectAsync(smtp.Host, smtp.Port, secureSocketOptions, stoppingToken);
                await smtpClient.AuthenticateAsync(smtp.Username, smtp.Password, stoppingToken);
                await smtpClient.SendAsync(message, stoppingToken);
                QuoteTrack.Infrastructure.Logging.SystemLogger.LogEvent("SUCCESS", "SMTP Send", $"Sent reminder email to {toEmail}.");
                await smtpClient.DisconnectAsync(true, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send reminder email to {toEmail}.");
            }
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Infrastructure.Logging;

namespace QuoteTrack.Web.Services
{
    public class WorkflowEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<WorkflowEmailService> _logger;

        public WorkflowEmailService(IConfiguration config, ILogger<WorkflowEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            try
            {
                var smtp = ResolveSmtpConfig();

                if (smtp == null || string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
                {
                    _logger.LogWarning("SMTP credentials missing. Cannot send email.");
                    SystemLogger.LogEvent("ERROR", "SMTP Engine", "SMTP credentials missing. Email not sent.");
                    return;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromEmail));

                var recipients = (toEmail ?? string.Empty)
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var recipient in recipients)
                {
                    message.To.Add(new MailboxAddress(toName ?? recipient, recipient));
                }

                if (!message.To.Any())
                {
                    _logger.LogWarning("No recipient email provided.");
                    return;
                }

                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

                using var client = new SmtpClient();
                var secureOptions = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

                await client.ConnectAsync(smtp.Host, smtp.Port, secureOptions);
                await client.AuthenticateAsync(smtp.Username, smtp.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                SystemLogger.LogEvent("SUCCESS", "SMTP Engine", $"Email sent to {string.Join(", ", recipients)} | Subject={subject}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email");
                SystemLogger.LogEvent("ERROR", "SMTP Engine", $"Failed to send email. {ex.Message}");
            }
        }

        public async Task SendProjectHandoffEmailAsync(Quote quote, string salesRepName)
        {
            SystemLogger.SendHandoffNotification(quote, salesRepName);
            await Task.CompletedTask;
        }

        private SmtpResolvedConfig? ResolveSmtpConfig()
        {
            try
            {
                // 1) First support direct SmtpSettings if ever used
                var directHost = _config["SmtpSettings:Host"];
                var directUser = _config["SmtpSettings:Username"];
                var directPass = _config["SmtpSettings:Password"];

                if (!string.IsNullOrWhiteSpace(directHost) &&
                    !string.IsNullOrWhiteSpace(directUser) &&
                    !string.IsNullOrWhiteSpace(directPass))
                {
                    var directPort = int.TryParse(_config["SmtpSettings:Port"], out var dp) ? dp : 465;
                    return new SmtpResolvedConfig
                    {
                        Host = directHost,
                        Port = directPort,
                        UseSsl = directPort == 465,
                        Username = directUser,
                        Password = directPass,
                        FromName = _config["SmtpSettings:FromName"] ?? "QuoteTrack System",
                        FromEmail = _config["SmtpSettings:FromEmail"] ?? directUser
                    };
                }

                // 2) Fallback to your actual appsettings.custom.json format: SmtpAccounts[]
                var smtpAccounts = _config.GetSection("SmtpAccounts").GetChildren().ToList();
                var active = smtpAccounts.FirstOrDefault(a =>
                    bool.TryParse(a["IsActive"], out var isActive) && isActive);

                if (active == null)
                    return null;

                var host = active["Host"];
                var username = active["Username"];
                var password = active["Password"];

                if (string.IsNullOrWhiteSpace(host) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password))
                    return null;

                var port = int.TryParse(active["Port"], out var p) ? p : 465;
                var useSsl = !bool.TryParse(active["UseSsl"], out var us) || us;

                return new SmtpResolvedConfig
                {
                    Host = host,
                    Port = port,
                    UseSsl = useSsl,
                    Username = username,
                    Password = password,
                    FromName = active["SenderName"] ?? "QuoteTrack System",
                    FromEmail = string.IsNullOrWhiteSpace(active["SenderEmail"]) ? username : active["SenderEmail"]!
                };
            }
            catch
            {
                return null;
            }
        }

        private sealed class SmtpResolvedConfig
        {
            public string Host { get; set; } = "";
            public int Port { get; set; }
            public bool UseSsl { get; set; }
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public string FromName { get; set; } = "";
            public string FromEmail { get; set; } = "";
        }
    }
}

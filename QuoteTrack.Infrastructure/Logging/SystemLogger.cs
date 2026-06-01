using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MailKit.Net.Smtp;
using MimeKit;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Infrastructure.Logging
{
    public class HealthState
    {
        public DateTime LastImapCheck { get; set; }
        public DateTime LastSmtpRun { get; set; }
        public List<LogEntry> RecentLogs { get; set; } = new();
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = "INFO";
        public string Source { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static class SystemLogger
    {
        // Prefer app-local writable folder:
        // <app base>\App_Data\system_health.json
        // This works reliably on IIS when App_Data has write permission.
        private static readonly string DataDir = ResolveDataDir();
        private static readonly string LogPath = Path.Combine(DataDir, "system_health.json");

        // appsettings.custom.json must be found in the running directory (IIS content root)
        private static readonly string ConfigPath = "appsettings.custom.json";

        private static readonly object _lock = new object();

        private static string ResolveDataDir()
        {
            try
            {
                // IIS: AppContext.BaseDirectory points to bin folder; go up to site root if possible
                var baseDir = AppContext.BaseDirectory;

                // Try: bin\ => go up one to site root
                var parent = Directory.GetParent(baseDir);
                if (parent != null)
                {
                    var candidate = Path.Combine(parent.FullName, "App_Data");
                    Directory.CreateDirectory(candidate);
                    return candidate;
                }

                // Fallback to base dir
                var fallback = Path.Combine(baseDir, "App_Data");
                Directory.CreateDirectory(fallback);
                return fallback;
            }
            catch
            {
                // Last resort: ProgramData
                var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var candidate = Path.Combine(progData, "QuoteTrack", "App_Data");
                Directory.CreateDirectory(candidate);
                return candidate;
            }
        }

        public static HealthState GetState()
        {
            lock (_lock)
            {
                if (!File.Exists(LogPath))
                    return new HealthState();

                try
                {
                    return JsonSerializer.Deserialize<HealthState>(File.ReadAllText(LogPath)) ?? new HealthState();
                }
                catch
                {
                    return new HealthState();
                }
            }
        }

        public static void LogHeartbeat(string workerType)
        {
            var state = GetState();
            if (workerType == "IMAP") state.LastImapCheck = DateTime.UtcNow;
            if (workerType == "SMTP") state.LastSmtpRun = DateTime.UtcNow;
            SaveState(state);
        }

        public static void LogEvent(string type, string source, string message)
        {
            var state = GetState();

            state.RecentLogs.Insert(0, new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Source = source,
                Message = message
            });

            if (state.RecentLogs.Count > 200)
                state.RecentLogs = state.RecentLogs.Take(200).ToList();

            SaveState(state);

            if (string.Equals(type, "ERROR", StringComparison.OrdinalIgnoreCase))
                SendAdminErrorAlert(source, message);
        }

        private static void SaveState(HealthState state)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(LogPath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private static void AddRecipients(MimeMessage msg, string namePrefix, string emailString)
        {
            if (string.IsNullOrWhiteSpace(emailString)) return;
            var emails = emailString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var email in emails)
            {
                var trimmed = email.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    msg.To.Add(new MailboxAddress(namePrefix, trimmed));
            }
        }

        public static void SendHandoffNotification(Quote quote, string salesRepName)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (!File.Exists(ConfigPath)) return;

                    using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                    var smtpAccounts = doc.RootElement.GetProperty("SmtpAccounts").EnumerateArray();
                    var activeSmtp = smtpAccounts.FirstOrDefault(a => a.GetProperty("IsActive").GetBoolean());
                    if (activeSmtp.ValueKind == JsonValueKind.Undefined) return;

                    var host = activeSmtp.GetProperty("Host").GetString();
                    var port = activeSmtp.GetProperty("Port").GetInt32();
                    var useSsl = activeSmtp.GetProperty("UseSsl").GetBoolean();
                    var user = activeSmtp.GetProperty("Username").GetString() ?? "no-reply@nexcel.me";
                    var pass = activeSmtp.GetProperty("Password").GetString() ?? "";

                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("QuoteTrack Automations", user));

                    var root = doc.RootElement;
                    if (root.TryGetProperty("PresalesEmail", out var pEmail)) AddRecipients(message, "Presales", pEmail.GetString() ?? "");
                    if (root.TryGetProperty("ImplementationEmail", out var iEmail)) AddRecipients(message, "Implementation", iEmail.GetString() ?? "");
                    if (root.TryGetProperty("CoordinatorEmail", out var cEmail)) AddRecipients(message, "Coordinator", cEmail.GetString() ?? "");
                    if (root.TryGetProperty("MaterialsEmail", out var mEmail)) AddRecipients(message, "Materials", mEmail.GetString() ?? "");

                    if (!message.To.Any())
                        return;

                    message.Subject = $"PROJECT WON: Handoff for {quote.ClientName}";
                    message.Body = new TextPart("html")
                    {
                        Text = $"<h2 style='color:green;'>Project Won!</h2>" +
                               $"<p><strong>Sales Rep:</strong> {salesRepName}</p>" +
                               $"<p><strong>Client:</strong> {quote.ClientName}</p>" +
                               $"<p><strong>Reference:</strong> {quote.QuoteReference}</p>" +
                               $"<p><strong>Value:</strong> {quote.QuoteValue} {quote.Currency}</p>" +
                               $"<p>Please initiate the implementation schedule and material arrangements.</p>"
                    };

                    using var smtp = new SmtpClient();
                    var secureOptions = useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls;

                    await smtp.ConnectAsync(host, port, secureOptions);
                    await smtp.AuthenticateAsync(user, pass);
                    await smtp.SendAsync(message);
                    await smtp.DisconnectAsync(true);

                    LogHeartbeat("SMTP");
                }
                catch
                {
                    // swallow
                }
            });
        }

        private static void SendAdminErrorAlert(string source, string errorMessage)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (!File.Exists(ConfigPath)) return;

                    using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                    var adminEmail = "admin@nexcel.me";
                    if (doc.RootElement.TryGetProperty("SystemAdminEmail", out var adminEmailProp))
                        adminEmail = adminEmailProp.GetString() ?? adminEmail;

                    var smtpAccounts = doc.RootElement.GetProperty("SmtpAccounts").EnumerateArray();
                    var activeSmtp = smtpAccounts.FirstOrDefault(a => a.GetProperty("IsActive").GetBoolean());
                    if (activeSmtp.ValueKind == JsonValueKind.Undefined) return;

                    var host = activeSmtp.GetProperty("Host").GetString();
                    var port = activeSmtp.GetProperty("Port").GetInt32();
                    var useSsl = activeSmtp.GetProperty("UseSsl").GetBoolean();
                    var user = activeSmtp.GetProperty("Username").GetString() ?? "no-reply@nexcel.me";
                    var pass = activeSmtp.GetProperty("Password").GetString() ?? "";

                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("QuoteTrack Alerts", user));
                    AddRecipients(message, "System Admin", adminEmail);

                    if (!message.To.Any())
                        return;

                    message.Subject = $"CRITICAL ERROR: QuoteTrack {source}";
                    message.Body = new TextPart("html")
                    {
                        Text = $"<h3 style='color:red;'>System Error Alert</h3>" +
                               $"<p><strong>Source:</strong> {source}</p>" +
                               $"<p><strong>Error:</strong> {errorMessage}</p>"
                    };

                    using var smtp = new SmtpClient();
                    var secureOptions = useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls;

                    await smtp.ConnectAsync(host, port, secureOptions);
                    await smtp.AuthenticateAsync(user, pass);
                    await smtp.SendAsync(message);
                    await smtp.DisconnectAsync(true);

                    LogHeartbeat("SMTP");
                }
                catch
                {
                    // swallow
                }
            });
        }
    }
}
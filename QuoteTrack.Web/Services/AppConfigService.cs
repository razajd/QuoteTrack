// QuoteTrack.Web/Services/AppConfigService.cs
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QuoteTrack.Web.Services
{
    public class ImapAccount
    {
        public string Host { get; set; } = "imap.titan.email";
        public int Port { get; set; } = 993;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string TargetFolder { get; set; } = "INBOX";
        public string PostProcessAction { get; set; } = "MarkRead";
        public bool IsActive { get; set; } = true;
        public string AccountRole { get; set; } = "Lead";
    }

    public class SmtpAccount
    {
        public string Host { get; set; } = "smtp.titan.email";
        public int Port { get; set; } = 465;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string SenderName { get; set; } = "QuoteTrack CRM";
        public string SenderEmail { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    public class AppConfig
    {
        public bool IsSetupComplete { get; set; } = false;
        public string DbConnectionString { get; set; } = "";

        public string AppTitle { get; set; } = "NEXCEL QuoteTrack";
        public string LogoUrl { get; set; } = "";
        public string PrimaryColor { get; set; } = "#fca311";
        public string NavBackgroundColor { get; set; } = "#3a3a3a";
        public string BackgroundColor { get; set; } = "#f4f6f9";

        // Theme / typography
        public string SurfaceColor { get; set; } = "#ffffff";
        public string SurfaceAltColor { get; set; } = "#f8fafc";
        public string TextColor { get; set; } = "#1f2937";
        public string MutedTextColor { get; set; } = "#6b7280";
        public string BorderColor { get; set; } = "#e5e7eb";
        public string FontFamily { get; set; } = "Segoe UI, Tahoma, Geneva, Verdana, sans-serif";
        public string HeadingFontFamily { get; set; } = "Segoe UI, Tahoma, Geneva, Verdana, sans-serif";
        public int CardRadiusPx { get; set; } = 18;

        public string SystemAdminEmail { get; set; } = "admin@nexcel.me";
        public string SupportRequestEmail { get; set; } = "support@nexcel.me";

        // Existing
        public int ReminderIntervalHours { get; set; } = 12;
        public string DefaultLeadOwnerId { get; set; } = "";

        // New - reminder controls
        public bool EnableSalesReminderEmails { get; set; } = true;
        public bool ReminderSendOnlyOverdue { get; set; } = true;
        public bool ReminderIncludeDueToday { get; set; } = true;
        public bool ReminderAlsoSendToAdmin { get; set; } = false;
        public int ReminderLookaheadHours { get; set; } = 24;

        // New - daily digest controls
        public bool EnableDailyAdminDigest { get; set; } = true;
        public string DailyDigestRecipientEmail { get; set; } = "";
        public string DailyDigestTimeLocal { get; set; } = "08:00";
        public bool DailyDigestIncludeIngestion { get; set; } = true;
        public bool DailyDigestIncludeLogins { get; set; } = true;
        public bool DailyDigestIncludeDueSummary { get; set; } = true;

        public string PresalesEmail { get; set; } = "ahmed@nexcel.me";
        public string ImplementationEmail { get; set; } = "sohail@nexcel.me";
        public string CoordinatorEmail { get; set; } = "maher@nexcel.me";
        public string MaterialsEmail { get; set; } = "shamsa@nexcel.me";

        public List<ImapAccount> ImapAccounts { get; set; } = new();
        public List<SmtpAccount> SmtpAccounts { get; set; } = new();
    }

    public class AppConfigService
    {
        private readonly string _filePath = "appsettings.custom.json";
        public AppConfig Current { get; private set; } = new();

        public AppConfigService()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_filePath)) ?? new AppConfig();
            }
            else
            {
                Current.ImapAccounts.Add(new ImapAccount());
                Current.SmtpAccounts.Add(new SmtpAccount());
                Save();
            }

            if (Current.ImapAccounts.Count == 0)
                Current.ImapAccounts.Add(new ImapAccount());

            if (Current.SmtpAccounts.Count == 0)
                Current.SmtpAccounts.Add(new SmtpAccount());

            NormalizeThemeDefaults();
            NormalizeAutomationDefaults();
        }

        public void Save()
        {
            NormalizeThemeDefaults();
            NormalizeAutomationDefaults();
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void NormalizeThemeDefaults()
        {
            Current.PrimaryColor = string.IsNullOrWhiteSpace(Current.PrimaryColor) ? "#fca311" : Current.PrimaryColor;
            Current.NavBackgroundColor = string.IsNullOrWhiteSpace(Current.NavBackgroundColor) ? "#3a3a3a" : Current.NavBackgroundColor;
            Current.BackgroundColor = string.IsNullOrWhiteSpace(Current.BackgroundColor) ? "#f4f6f9" : Current.BackgroundColor;
            Current.SurfaceColor = string.IsNullOrWhiteSpace(Current.SurfaceColor) ? "#ffffff" : Current.SurfaceColor;
            Current.SurfaceAltColor = string.IsNullOrWhiteSpace(Current.SurfaceAltColor) ? "#f8fafc" : Current.SurfaceAltColor;
            Current.TextColor = string.IsNullOrWhiteSpace(Current.TextColor) ? "#1f2937" : Current.TextColor;
            Current.MutedTextColor = string.IsNullOrWhiteSpace(Current.MutedTextColor) ? "#6b7280" : Current.MutedTextColor;
            Current.BorderColor = string.IsNullOrWhiteSpace(Current.BorderColor) ? "#e5e7eb" : Current.BorderColor;
            Current.FontFamily = string.IsNullOrWhiteSpace(Current.FontFamily) ? "Segoe UI, Tahoma, Geneva, Verdana, sans-serif" : Current.FontFamily;
            Current.HeadingFontFamily = string.IsNullOrWhiteSpace(Current.HeadingFontFamily) ? Current.FontFamily : Current.HeadingFontFamily;
            if (Current.CardRadiusPx <= 0) Current.CardRadiusPx = 18;
        }

        private void NormalizeAutomationDefaults()
        {
            if (Current.ReminderIntervalHours <= 0) Current.ReminderIntervalHours = 12;
            if (Current.ReminderLookaheadHours <= 0) Current.ReminderLookaheadHours = 24;
            if (string.IsNullOrWhiteSpace(Current.DailyDigestTimeLocal)) Current.DailyDigestTimeLocal = "08:00";
        }
    }
}

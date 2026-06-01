using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace QuoteTrack.Web.Services
{
    public class SupportRequestService
    {
        private readonly AppConfigService _config;

        public SupportRequestService(AppConfigService config)
        {
            _config = config;
        }

        public async Task SendAsync(string fromUserEmail, string subject, string message, string? attachmentFileName, byte[]? attachmentBytes, string? attachmentContentType)
        {
            var smtp = _config.Current.SmtpAccounts.FirstOrDefault(a => a.IsActive);
            if (smtp == null) throw new InvalidOperationException("No active SMTP account configured.");

            var toEmail = _config.Current.SupportRequestEmail;
            if (string.IsNullOrWhiteSpace(toEmail)) throw new InvalidOperationException("SupportRequestEmail is not configured.");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(smtp.SenderName ?? "QuoteTrack", smtp.SenderEmail ?? smtp.Username));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = $"<p><b>From:</b> {System.Net.WebUtility.HtmlEncode(fromUserEmail)}</p>" +
                           $"<p><b>Message:</b></p><pre style='white-space:pre-wrap;font-family:system-ui,Segoe UI,Arial;'>{System.Net.WebUtility.HtmlEncode(message)}</pre>"
            };

            if (attachmentBytes != null && attachmentBytes.Length > 0 && !string.IsNullOrWhiteSpace(attachmentFileName))
            {
                builder.Attachments.Add(attachmentFileName, attachmentBytes, ContentType.Parse(string.IsNullOrWhiteSpace(attachmentContentType) ? "application/octet-stream" : attachmentContentType));
            }

            email.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseSsl);
            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password);
            }
            await client.SendAsync(email);
            await client.DisconnectAsync(true);
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using QuoteTrack.Application.Interfaces;

namespace QuoteTrack.Web.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/attachments")]
    public class AttachmentsController : ControllerBase
    {
        private readonly IAppDbContext _db;

        public AttachmentsController(IAppDbContext db)
        {
            _db = db;
        }

        // Force DOWNLOAD (kept)
        [HttpGet("download/{id:guid}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var att = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (att == null) return NotFound();

            var physicalPath = ResolvePhysicalPath(att.StoragePath);
            if (!System.IO.File.Exists(physicalPath)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
            var contentType = string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType;

            return File(bytes, contentType, att.FileName);
        }

        // ✅ INLINE endpoint (best for PDFs in iframe)
        [HttpGet("inline/{id:guid}")]
        public async Task<IActionResult> Inline(Guid id)
        {
            var att = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (att == null) return NotFound();

            var physicalPath = ResolvePhysicalPath(att.StoragePath);
            if (!System.IO.File.Exists(physicalPath)) return NotFound();

            var contentType = string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType;

            // Force INLINE
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{SanitizeFileName(att.FileName)}\"";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            var stream = System.IO.File.OpenRead(physicalPath);
            return File(stream, contentType, enableRangeProcessing: true);
        }

        // ✅ NEW: Render EML as HTML (prevents downloads, browser can display)
        [HttpGet("emlview/{id:guid}")]
        public async Task<IActionResult> EmlView(Guid id)
        {
            var att = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (att == null) return NotFound();

            var physicalPath = ResolvePhysicalPath(att.StoragePath);
            if (!System.IO.File.Exists(physicalPath)) return NotFound();

            // Only allow EML-like content here
            var isEml = (att.ContentType ?? "").Equals("message/rfc822", StringComparison.OrdinalIgnoreCase) ||
                        (att.FileName ?? "").EndsWith(".eml", StringComparison.OrdinalIgnoreCase);

            if (!isEml) return BadRequest("Not an EML file.");

            MimeMessage msg;
            using (var fs = System.IO.File.OpenRead(physicalPath))
            {
                msg = await MimeMessage.LoadAsync(fs);
            }

            var from = msg.From?.ToString() ?? "";
            var to = msg.To?.ToString() ?? "";
            var cc = msg.Cc?.ToString() ?? "";
            var subject = msg.Subject ?? "";
            var date = msg.Date.ToString("ddd, dd MMM yyyy HH:mm:ss zzz");

            // Prefer HTML body, fallback to text body
            string bodyHtml = msg.HtmlBody ?? "";
            string bodyText = msg.TextBody ?? "";

            // If HTML body missing, render text safely
            if (string.IsNullOrWhiteSpace(bodyHtml))
            {
                bodyHtml = "<pre style=\"white-space:pre-wrap; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;\">" +
                           HtmlEncode(bodyText) +
                           "</pre>";
            }
            else
            {
                // Safety: Keep it inside a sandboxed container.
                // We do NOT attempt to fully sanitize vendor HTML here, but we also don't execute scripts in iframe.
                // Still, we strip <script> blocks to be safe.
                bodyHtml = StripScripts(bodyHtml);
            }

            var html = BuildEmlHtml(from, to, cc, subject, date, bodyHtml);

            // Return as HTML so browser renders instead of downloading
            return Content(html, "text/html; charset=utf-8");
        }

        private static string BuildEmlHtml(string from, string to, string cc, string subject, string date, string bodyHtml)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\"/>");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
            sb.AppendLine("<title>Email Preview</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;margin:0;background:#f6f7f9;color:#111;}");
            sb.AppendLine(".wrap{padding:12px;}");
            sb.AppendLine(".card{background:#fff;border:1px solid #e5e7eb;border-radius:10px;box-shadow:0 1px 2px rgba(0,0,0,.04);overflow:hidden;}");
            sb.AppendLine(".hdr{padding:12px 14px;border-bottom:1px solid #eef0f3;}");
            sb.AppendLine(".row{display:flex;gap:10px;flex-wrap:wrap;font-size:13px;color:#333;}");
            sb.AppendLine(".k{color:#6b7280;font-weight:600;min-width:70px;}");
            sb.AppendLine(".v{color:#111;}");
            sb.AppendLine(".subj{font-size:16px;font-weight:800;margin:0 0 6px 0;}");
            sb.AppendLine(".body{padding:14px;background:#fff;}");
            sb.AppendLine("iframe, img{max-width:100%;}");
            sb.AppendLine("pre{margin:0;}");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"wrap\">");
            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<div class=\"hdr\">");
            sb.AppendLine($"<div class=\"subj\">{HtmlEncode(subject)}</div>");
            sb.AppendLine("<div class=\"row\"><div class=\"k\">From</div><div class=\"v\">" + HtmlEncode(from) + "</div></div>");
            sb.AppendLine("<div class=\"row\"><div class=\"k\">To</div><div class=\"v\">" + HtmlEncode(to) + "</div></div>");
            if (!string.IsNullOrWhiteSpace(cc))
                sb.AppendLine("<div class=\"row\"><div class=\"k\">Cc</div><div class=\"v\">" + HtmlEncode(cc) + "</div></div>");
            sb.AppendLine("<div class=\"row\"><div class=\"k\">Date</div><div class=\"v\">" + HtmlEncode(date) + "</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"body\">");
            sb.AppendLine(bodyHtml);
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private static string StripScripts(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // simple remove <script> blocks
            while (true)
            {
                var start = html.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;

                var end = html.IndexOf("</script>", start, StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                {
                    html = html.Substring(0, start);
                    break;
                }

                html = html.Remove(start, (end - start) + "</script>".Length);
            }

            return html;
        }

        private static string HtmlEncode(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#39;");
        }

        private string ResolvePhysicalPath(string storagePath)
        {
            var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var normalized = (storagePath ?? "")
                .Replace("/", Path.DirectorySeparatorChar.ToString())
                .Replace("\\", Path.DirectorySeparatorChar.ToString());
            return Path.Combine(root, normalized);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "file";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
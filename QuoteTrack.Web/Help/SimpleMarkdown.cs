using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace QuoteTrack.Web.Help
{
    public static class SimpleMarkdown
    {
        public static string ToHtml(string markdown, Func<string, string>? slugify = null)
        {
            slugify ??= DefaultSlugify;

            var sb = new StringBuilder();
            var lines = markdown.Replace("\r\n", "\n").Split('\n');

            bool inCode = false;
            bool inUl = false;

            void CloseUl()
            {
                if (inUl)
                {
                    sb.AppendLine("</ul>");
                    inUl = false;
                }
            }

            foreach (var raw in lines)
            {
                var line = raw;

                if (line.TrimStart().StartsWith("```"))
                {
                    if (!inCode)
                    {
                        CloseUl();
                        inCode = true;
                        sb.AppendLine("<pre class=\"help-code\"><code>");
                    }
                    else
                    {
                        inCode = false;
                        sb.AppendLine("</code></pre>");
                    }
                    continue;
                }

                if (inCode)
                {
                    sb.AppendLine(WebUtility.HtmlEncode(line));
                    continue;
                }

                var t = line.Trim();

                if (string.IsNullOrWhiteSpace(t))
                {
                    CloseUl();
                    sb.AppendLine("<div style=\"height:10px\"></div>");
                    continue;
                }

                if (t.StartsWith("#"))
                {
                    CloseUl();

                    int level = t.TakeWhile(c => c == '#').Count();
                    if (level < 1) level = 1;
                    if (level > 3) level = 3;

                    var text = t.Substring(level).Trim();
                    var anchor = slugify(text);

                    sb.AppendLine($"<h{level} id=\"{anchor}\" class=\"help-h{level}\">{Inline(text)}</h{level}>");
                    continue;
                }

                if (t.StartsWith("- ") || t.StartsWith("* "))
                {
                    if (!inUl)
                    {
                        sb.AppendLine("<ul class=\"help-ul\">");
                        inUl = true;
                    }

                    var item = t.Substring(2).Trim();
                    sb.AppendLine($"<li>{Inline(item)}</li>");
                    continue;
                }

                var numMatch = Regex.Match(t, @"^\d+\.\s+(.+)$");
                if (numMatch.Success)
                {
                    CloseUl();
                    sb.AppendLine($"<p class=\"help-p\"><b>{Inline(numMatch.Groups[0].Value)}</b></p>");
                    continue;
                }

                CloseUl();
                sb.AppendLine($"<p class=\"help-p\">{Inline(t)}</p>");
            }

            CloseUl();
            if (inCode)
                sb.AppendLine("</code></pre>");

            return sb.ToString();
        }

        private static string Inline(string text)
        {
            var s = WebUtility.HtmlEncode(text);
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<b>$1</b>");
            s = Regex.Replace(s, @"`(.+?)`", "<code class=\"help-inline\">$1</code>");
            return s;
        }

        private static string DefaultSlugify(string text)
        {
            text = text.Trim().ToLowerInvariant();
            text = Regex.Replace(text, @"[^\w\s-]", "");
            text = Regex.Replace(text, @"\s+", "-");
            text = Regex.Replace(text, @"-+", "-");
            return text;
        }
    }
}
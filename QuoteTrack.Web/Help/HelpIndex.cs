using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

namespace QuoteTrack.Web.Help
{
    public class HelpIndex
    {
        private readonly IWebHostEnvironment _env;

        public HelpIndex(IWebHostEnvironment env)
        {
            _env = env;
        }

        public List<HelpDoc> LoadAll()
        {
            var root = Path.Combine(_env.WebRootPath, "help");
            if (!Directory.Exists(root))
                return new List<HelpDoc>();

            var files = Directory.GetFiles(root, "*.md", SearchOption.TopDirectoryOnly)
                                 .OrderBy(x => x)
                                 .ToList();

            var list = new List<HelpDoc>();

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var meta = ParseFrontMatter(text, out var body);

                var slug = Path.GetFileNameWithoutExtension(file);

                list.Add(new HelpDoc
                {
                    Slug = slug,
                    Title = meta.TryGetValue("title", out var t) ? t : slug,
                    Category = meta.TryGetValue("category", out var c) ? c : "General",
                    Summary = meta.TryGetValue("summary", out var s) ? s : "",
                    FilePath = "/help/" + slug,
                    Content = body.Trim()
                });
            }

            return list
                .OrderBy(a => a.Category)
                .ThenBy(a => a.Title)
                .ToList();
        }

        public HelpDoc? LoadBySlug(string slug)
        {
            var root = Path.Combine(_env.WebRootPath, "help");
            var file = Path.Combine(root, slug + ".md");

            if (!File.Exists(file)) return null;

            var text = File.ReadAllText(file);
            var meta = ParseFrontMatter(text, out var body);

            return new HelpDoc
            {
                Slug = slug,
                Title = meta.TryGetValue("title", out var t) ? t : slug,
                Category = meta.TryGetValue("category", out var c) ? c : "General",
                Summary = meta.TryGetValue("summary", out var s) ? s : "",
                FilePath = "/help/" + slug,
                Content = body.Trim()
            };
        }

        public List<HelpDoc> Search(string query)
        {
            var all = LoadAll();
            if (string.IsNullOrWhiteSpace(query)) return all;

            query = query.Trim();
            var q = query.ToLowerInvariant();

            return all
                .Select(a =>
                {
                    var title = (a.Title ?? "").ToLowerInvariant();
                    var body = (a.Content ?? "").ToLowerInvariant();

                    int score = 0;
                    if (title.Contains(q)) score += 10;
                    if (body.Contains(q)) score += 3;

                    var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tok in tokens)
                    {
                        if (tok.Length < 2) continue;
                        if (title.Contains(tok)) score += 4;
                        if (body.Contains(tok)) score += 1;
                    }

                    return new { Article = a, Score = score };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Article.Category)
                .ThenBy(x => x.Article.Title)
                .Select(x => x.Article)
                .ToList();
        }

        private Dictionary<string, string> ParseFrontMatter(string markdown, out string body)
        {
            body = markdown;
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!markdown.StartsWith("---"))
                return meta;

            var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end < 0)
                return meta;

            var header = markdown.Substring(3, end - 3).Trim();
            body = markdown.Substring(end + 4).Trim();

            foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;

                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();

                meta[key] = val;
            }

            return meta;
        }

        public List<(string Text, string Anchor)> ExtractHeadings(string markdown)
        {
            var headings = new List<(string Text, string Anchor)>();

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!line.StartsWith("#")) continue;

                var level = line.TakeWhile(c => c == '#').Count();
                if (level < 1 || level > 3) continue;

                var text = line.Substring(level).Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                headings.Add((text, Slugify(text)));
            }

            return headings;
        }

        public string Slugify(string text)
        {
            text = text.Trim().ToLowerInvariant();
            text = Regex.Replace(text, @"[^\w\s-]", "");
            text = Regex.Replace(text, @"\s+", "-");
            text = Regex.Replace(text, @"-+", "-");
            return text;
        }
    }
}
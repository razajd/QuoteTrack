using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace QuoteTrack.Infrastructure.Extraction
{
    /// <summary>
    /// Template-aware PDF extractor:
    /// - Zoho: capture "Total without VAT" (last pages)
    /// - Manual Nexcel: capture Client from "To" (page 1) and Amount from:
    ///     (a) Investment Summary blocks OR
    ///     (b) BOQ end page: Total just above VAT
    /// Robust against PdfPig "column-merged lines".
    /// </summary>
    public class PdfPigQuotationExtractor : IPdfQuotationExtractor
    {
        public Task ExtractAsync(string filePath, Quote quote)
        {
            try
            {
                using var pdf = PdfDocument.Open(filePath);

                var pages = new List<string>();
                foreach (var page in pdf.GetPages())
                {
                    var text = ContentOrderTextExtractor.GetText(page);
                    pages.Add(text ?? "");
                }

                var fullText = string.Join("\n\n---PAGE---\n\n", pages);

                quote.Currency ??= DetectCurrency(fullText);

                var isZoho = fullText.IndexOf("Total without VAT", StringComparison.OrdinalIgnoreCase) >= 0
                             && fullText.IndexOf("Total with VAT", StringComparison.OrdinalIgnoreCase) >= 0;

                var isManual = fullText.IndexOf("NEXCEL", StringComparison.OrdinalIgnoreCase) >= 0
                               || (pages.Count > 0 && pages[0].IndexOf("To", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isZoho)
                {
                    ExtractZohoTotalWithoutVat(pages, quote);
                    ExtractZohoClientIfPossible(fullText, quote);
                }
                else if (isManual)
                {
                    ExtractManualClientFromToBlock(pages, quote);
                    ExtractManualTotalBeforeVat(pages, fullText, quote);
                }
                else
                {
                    // fallback
                    ExtractGenericTotalWithoutVat(fullText, quote);
                    ExtractClientNameGeneric(fullText, quote);
                }

                ExtractQuoteReference(fullText, quote);
                ExtractDate(fullText, quote);

                if (string.IsNullOrWhiteSpace(quote.SolutionSummary))
                    quote.SolutionSummary = isZoho
                        ? "Zoho quotation received (Total without VAT extracted)."
                        : isManual
                            ? "Manual quotation received (Client/Total extracted)."
                            : "Quotation received (basic extraction applied).";
            }
            catch
            {
                // don't crash ingestion
            }

            return Task.CompletedTask;
        }

        // -------------------------
        // ZOHO
        // -------------------------

        private void ExtractZohoTotalWithoutVat(List<string> pages, Quote quote)
        {
            var lastPages = GetLastPages(pages, 3);

            foreach (var p in lastPages)
            {
                // capture exactly after label
                var m = Regex.Match(p, @"Total\s*without\s*VAT\s*[:]*\s*([0-9][0-9\.,\s]{1,30}[0-9])", RegexOptions.IgnoreCase);
                if (m.Success && TryParseMoney(m.Groups[1].Value, out var val))
                {
                    quote.QuoteValue = val;
                    return;
                }
            }

            // fallback: Subtotal
            foreach (var p in lastPages)
            {
                var m = Regex.Match(p, @"Subtotal\s*[:]*\s*([0-9][0-9\.,\s]{1,30}[0-9])", RegexOptions.IgnoreCase);
                if (m.Success && TryParseMoney(m.Groups[1].Value, out var val))
                {
                    quote.QuoteValue = val;
                    return;
                }
            }
        }

        private void ExtractZohoClientIfPossible(string text, Quote quote)
        {
            var billTo = Regex.Match(text, @"Bill\s*To[\s\r\n]+([^\r\n]+)", RegexOptions.IgnoreCase);
            if (billTo.Success)
            {
                var c = CleanClientLine(billTo.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(c)) quote.ClientName = c;
            }
        }

        // -------------------------
        // MANUAL (Nexcel)
        // -------------------------

        private void ExtractManualClientFromToBlock(List<string> pages, Quote quote)
        {
            if (pages.Count == 0) return;

            var p1 = pages[0];

            // PdfPig sometimes collapses line breaks/columns, so do a DOTALL capture:
            // To, <client block> (stop at Sub:/Subject/Dear)
            var m = Regex.Match(
                p1,
                @"\bTo\s*,?\s*(?<block>[\s\S]{0,400}?)(?:\bSub\s*:|\bSubject\s*:|\bDear\b)",
                RegexOptions.IgnoreCase);

            if (m.Success)
            {
                var block = m.Groups["block"].Value;

                // Split block into "logical lines" and pick first organization-like line
                var lines = SplitLogicalLines(block)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (lines.Count > 0)
                {
                    // Prefer organization line
                    var org = lines.FirstOrDefault(x =>
                        Regex.IsMatch(x, @"\b(ministry|authority|company|bank|university|school|hospital|department|directorate|court)\b", RegexOptions.IgnoreCase))
                              ?? lines.FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(org))
                    {
                        org = CleanClientLine(org);

                        if (!string.IsNullOrWhiteSpace(org))
                        {
                            quote.ClientName = org;
                            return;
                        }
                    }
                }
            }

            // fallback: if we didn't find To block, try any line that looks like an org near "To"
            var fallback = Regex.Match(p1, @"\bTo\s*,?\s*([A-Za-z][^\r\n]{3,80})", RegexOptions.IgnoreCase);
            if (fallback.Success)
            {
                var c = CleanClientLine(fallback.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(c)) quote.ClientName = c;
            }
        }

        private void ExtractManualTotalBeforeVat(List<string> pages, string fullText, Quote quote)
        {
            // Manual totals appear in 2 patterns:
            // A) Investment Summary: Total, VAT, Total With VAT
            // B) End of BOQ: Total, VAT, Total (or Total With VAT)
            //
            // Problem: PdfPig may put them on one line or reorder. So we use a block-regex:
            // Total <amount> ... VAT <amount>  => pick Total amount (first one after "Total" not "Total with VAT")

            // 1) Try pages containing "Investment Summary"
            var invPages = pages.Where(p => p.IndexOf("Investment Summary", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var p in invPages)
            {
                if (TryExtractTotalBeforeVatFromAnyLayout(p, out var val))
                {
                    quote.QuoteValue = val;
                    return;
                }
            }

            // 2) Try last pages (BOQ end)
            var lastPages = GetLastPages(pages, 5);
            foreach (var p in lastPages)
            {
                if (TryExtractTotalBeforeVatFromAnyLayout(p, out var val))
                {
                    quote.QuoteValue = val;
                    return;
                }
            }

            // 3) Last fallback: global search
            if (TryExtractTotalBeforeVatFromAnyLayout(fullText, out var globalVal))
            {
                quote.QuoteValue = globalVal;
                return;
            }

            // 4) final fallback
            ExtractGenericTotalWithoutVat(fullText, quote);
        }

        private bool TryExtractTotalBeforeVatFromAnyLayout(string text, out decimal totalBeforeVat)
        {
            totalBeforeVat = 0;

            // A) Explicit "Total without VAT" present in some manual summaries
            var m0 = Regex.Match(text, @"Total\s*without\s*VAT\s*[:]*\s*([0-9][0-9\.,\s]{1,30}[0-9])", RegexOptions.IgnoreCase);
            if (m0.Success && TryParseMoney(m0.Groups[1].Value, out var v0))
            {
                totalBeforeVat = v0;
                return true;
            }

            // B) Block pattern: Total <amt> ... VAT <amt>
            // Make sure we don't match "Total with VAT" as the Total label
            var m1 = Regex.Match(
                NormalizeSpaces(text),
                @"(?<!with\s)Total\s*[:]*\s*(?<t>[0-9][0-9\.,\s]{1,30}[0-9])\s*(?:[A-Z]{0,5}\s*)?(?:.*?\s)?VAT\s*(?:\(?\s*10%?\s*\)?)?\s*[:]*\s*(?<v>[0-9][0-9\.,\s]{1,30}[0-9])",
                RegexOptions.IgnoreCase);

            if (m1.Success && TryParseMoney(m1.Groups["t"].Value, out var tVal))
            {
                totalBeforeVat = tVal;
                return true;
            }

            // C) Line-oriented: find VAT line and take nearest number before it in same block
            // This catches cases where PdfPig merges: "Total 9275.000 VAT 927.500 Total 10202.500"
            var compact = NormalizeSpaces(text);

            var vatIndex = IndexOfIgnoreCase(compact, "VAT");
            if (vatIndex > 0)
            {
                // Take a window around VAT and find the first "Total <amt>" BEFORE VAT
                var start = Math.Max(0, vatIndex - 200);
                var window = compact.Substring(start, vatIndex - start);

                var m2 = Regex.Matches(window, @"(?<!with\s)Total\s*[:]*\s*([0-9][0-9\.,\s]{1,30}[0-9])", RegexOptions.IgnoreCase);
                if (m2.Count > 0)
                {
                    // take the last Total before VAT
                    var last = m2[m2.Count - 1];
                    if (TryParseMoney(last.Groups[1].Value, out var tVal2))
                    {
                        totalBeforeVat = tVal2;
                        return true;
                    }
                }
            }

            return false;
        }

        // -------------------------
        // Generic helpers
        // -------------------------

        private void ExtractGenericTotalWithoutVat(string text, Quote quote)
        {
            var exclMatch = Regex.Match(
                text,
                @"(?:Subtotal|Total\s*Before\s*VAT|Total\s*Excluding\s*VAT|Total\s*\(Excl\.?\s*VAT\)|Total\s*Excl\.?\s*VAT|Total\s*without\s*VAT)[\s\S]{0,120}?([0-9][0-9\.,\s]{1,30}[0-9])",
                RegexOptions.IgnoreCase);

            if (exclMatch.Success && TryParseMoney(exclMatch.Groups[1].Value, out var val))
            {
                quote.QuoteValue = val;
                return;
            }
        }

        private void ExtractClientNameGeneric(string text, Quote quote)
        {
            var billTo = Regex.Match(text, @"Bill\s*To[\s\r\n]+([^\r\n]+)", RegexOptions.IgnoreCase);
            if (billTo.Success)
            {
                var c = CleanClientLine(billTo.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(c)) quote.ClientName = c;
            }
        }

        private void ExtractQuoteReference(string text, Quote quote)
        {
            // Manual format: Ref: MOJ/250423-4/2025
            var m1 = Regex.Match(text, @"\bRef\s*[:\-]\s*([A-Z0-9\/\-\.]+)", RegexOptions.IgnoreCase);
            if (m1.Success)
            {
                quote.QuoteReference = m1.Groups[1].Value.Trim();
                return;
            }

            // fallback
            var m2 = Regex.Match(text, @"(?:Quote\s*#|Quotation\s*#|Estimate\s*#)\s*[:\-]?\s*([A-Z0-9\/\-\.]+)", RegexOptions.IgnoreCase);
            if (m2.Success) quote.QuoteReference = m2.Groups[1].Value.Trim();
        }

        private void ExtractDate(string text, Quote quote)
        {
            var m1 = Regex.Match(text, @"\bDate\s*[:\-]\s*([0-9]{1,2}(?:st|nd|rd|th)?\s+[A-Za-z]+\s*,\s*[0-9]{4})", RegexOptions.IgnoreCase);
            if (m1.Success && DateTime.TryParse(m1.Groups[1].Value, out var dt1))
            {
                quote.QuoteDate = DateTime.SpecifyKind(dt1, DateTimeKind.Utc);
                return;
            }

            var m2 = Regex.Match(text, @"\bDate\s*[:\-]\s*(\d{1,2}\/\d{1,2}\/\d{2,4})", RegexOptions.IgnoreCase);
            if (m2.Success && DateTime.TryParse(m2.Groups[1].Value, out var dt2))
                quote.QuoteDate = DateTime.SpecifyKind(dt2, DateTimeKind.Utc);
        }

        private string DetectCurrency(string text)
        {
            var m = Regex.Match(text, @"\b(BHD|BD|SAR|USD)\b", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim().ToUpperInvariant() : "BHD";
        }

        private List<string> GetLastPages(List<string> pages, int count)
        {
            if (pages.Count <= count) return pages;
            return pages.Skip(Math.Max(0, pages.Count - count)).ToList();
        }

        private List<string> SplitLogicalLines(string block)
        {
            // PdfPig can return with weird spacing; treat double spaces as separators too
            var normalized = block.Replace("\r\n", "\n").Replace("\r", "\n");
            var parts = normalized.Split('\n').Select(x => x.Trim()).ToList();

            // if it is one long line, try splitting by 2+ spaces
            if (parts.Count <= 2)
            {
                var one = NormalizeSpaces(normalized);
                var split = Regex.Split(one, @"\s{2,}").Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (split.Count > 0) return split;
            }

            return parts;
        }

        private string NormalizeSpaces(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private int IndexOfIgnoreCase(string s, string needle)
        {
            return s.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        }

        private string CleanClientLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var x = NormalizeSpaces(s);

            // Strip contact info
            x = Regex.Replace(x, @"\b(Mob|Mobile|Tel|Phone|Fax)\b\s*[:\-]?\s*.*$", "", RegexOptions.IgnoreCase).Trim();
            x = Regex.Replace(x, @"\bEmail\b\s*[:\-]?\s*.*$", "", RegexOptions.IgnoreCase).Trim();

            return x.Trim();
        }

        private bool TryParseMoney(string raw, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var s = raw.Trim().Replace(" ", "");
            s = Regex.Replace(s, @"[^0-9\.,]", "");
            if (string.IsNullOrWhiteSpace(s)) return false;

            var lastDot = s.LastIndexOf('.');
            var lastComma = s.LastIndexOf(',');

            if (lastDot >= 0 && lastComma >= 0)
            {
                if (lastComma > lastDot)
                {
                    s = s.Replace(".", "");
                    s = s.Replace(",", ".");
                }
                else
                {
                    s = s.Replace(",", "");
                }
            }
            else if (lastComma >= 0 && lastDot < 0)
            {
                var parts = s.Split(',');
                if (parts.Length == 2 && (parts[1].Length == 2 || parts[1].Length == 3))
                    s = s.Replace(",", ".");
                else
                    s = s.Replace(",", "");
            }

            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
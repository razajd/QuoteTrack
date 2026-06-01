// QuoteTrack.Infrastructure/Extraction/ClosedXmlQuotationExtractor.cs
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Infrastructure.Extraction
{
    public class ClosedXmlQuotationExtractor : IExcelQuotationExtractor
    {
        public Task ExtractAsync(string filePath, Quote quote)
        {
            try
            {
                using var workbook = new XLWorkbook(filePath);

                foreach (var worksheet in workbook.Worksheets)
                {
                    var usedCells = worksheet.CellsUsed();

                    foreach (var cell in usedCells)
                    {
                        var cellText = cell.GetString().Trim();
                        if (string.IsNullOrWhiteSpace(cellText)) continue;

                        // 1. Extract Value (Look for Total, etc. and check cell to the right)
                        if (Regex.IsMatch(cellText, @"^(Grand Total|Total Amount|Net Total|Total)$", RegexOptions.IgnoreCase))
                        {
                            var rightCell = cell.CellRight();
                            if (rightCell.TryGetValue<decimal>(out var value))
                            {
                                quote.QuoteValue = value;
                                quote.Currency = "BHD"; // Defaulting to base currency
                            }
                        }

                        // 2. Extract Reference
                        if (Regex.IsMatch(cellText, @"^(Quote Ref|Reference|Ref No|Quotation No)$", RegexOptions.IgnoreCase))
                        {
                            var refText = cell.CellRight().GetString().Trim();
                            if (!string.IsNullOrEmpty(refText))
                            {
                                quote.QuoteReference = refText;
                            }
                        }

                        // 3. Extract Client Name
                        if (Regex.IsMatch(cellText, @"^(To|Client|Customer|M/s)$", RegexOptions.IgnoreCase))
                        {
                            var clientName = cell.CellRight().GetString().Trim();
                            if (!string.IsNullOrEmpty(clientName))
                            {
                                quote.ClientName = clientName;
                            }
                        }

                        // 4. Extract Date
                        if (Regex.IsMatch(cellText, @"^(Date)$", RegexOptions.IgnoreCase))
                        {
                            var rightCell = cell.CellRight();
                            if (rightCell.TryGetValue<DateTime>(out var date))
                            {
                                quote.QuoteDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                            }
                            else if (DateTime.TryParse(rightCell.GetString(), out var parsedDate))
                            {
                                quote.QuoteDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(quote.SolutionSummary))
                {
                    quote.SolutionSummary = "Extracted from Excel attached to email.";
                }
            }
            catch (Exception)
            {
                // Swallow exception so worker doesn't crash on corrupted/password-protected excel files
            }

            return Task.CompletedTask;
        }
    }
}
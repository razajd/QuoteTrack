using System;
using System.Collections.Generic;

namespace QuoteTrack.Application.DTOs
{
    public class CorporateReportDto
    {
        public string ReportCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public List<CorporateReportCardDto> Cards { get; set; } = new();
        public List<string> Columns { get; set; } = new();
        public List<CorporateReportRowDto> Rows { get; set; } = new();
        public List<string> TrendColumns { get; set; } = new();
        public List<CorporateReportRowDto> TrendRows { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    public class CorporateReportCardDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Tone { get; set; } = "primary";
    }

    public class CorporateReportRowDto
    {
        public Dictionary<string, string> Values { get; set; } = new();
    }
}

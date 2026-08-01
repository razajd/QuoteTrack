using System;
using System.Collections.Generic;

namespace QuoteTrack.Application.DTOs
{
    public class KpiReportDto
    {
        public Guid? Id { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string TemplateCode { get; set; } = "ELV 01";
        public string ReportType { get; set; } = "Weekly";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int? WeekNumber { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public string ManagerReview { get; set; } = string.Empty;
        public string MainAchievements { get; set; } = string.Empty;
        public string AdditionalComments { get; set; } = string.Empty;
        public DateTime? LastSavedAt { get; set; }
        public List<KpiReportLineDto> Lines { get; set; } = new();
    }

    public class KpiReportLineDto
    {
        public int SortOrder { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public string ValueText { get; set; } = string.Empty;
        public string AutoValueText { get; set; } = string.Empty;
        public string ManualValueText { get; set; } = string.Empty;
        public string Guidance { get; set; } = string.Empty;
        public string EvidenceText { get; set; } = string.Empty;
        public bool IsManualInput { get; set; }
    }
}

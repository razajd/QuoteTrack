using System;
using System.Collections.Generic;

namespace QuoteTrack.Domain.Entities
{
    public class KpiReport
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string TemplateCode { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int? WeekNumber { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public string ManagerReview { get; set; } = string.Empty;
        public string MainAchievements { get; set; } = string.Empty;
        public string AdditionalComments { get; set; } = string.Empty;
        public string SubmittedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<KpiReportLine> Lines { get; set; } = new List<KpiReportLine>();
    }
}

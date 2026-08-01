using System;

namespace QuoteTrack.Domain.Entities
{
    public class KpiReportLine
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid KpiReportId { get; set; }
        public KpiReport? KpiReport { get; set; }
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

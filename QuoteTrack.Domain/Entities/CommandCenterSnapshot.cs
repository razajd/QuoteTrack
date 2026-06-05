using System;

namespace QuoteTrack.Domain.Entities
{
    public class CommandCenterSnapshot
    {
        public string ScopeKey { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? OwnerId { get; set; }
        public bool IsAdminScope { get; set; }

        public DateTime LastRefreshedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RefreshStartedAt { get; set; }
        public bool IsRefreshing { get; set; }
        public bool IsStale { get; set; } = true;
        public string? LastError { get; set; }

        public decimal PipelineValue { get; set; }
        public int ActiveQuotesCount { get; set; }
        public decimal WonThisMonth { get; set; }
        public int WonCountThisMonth { get; set; }
        public int OverdueCount { get; set; }
        public decimal OverdueValue { get; set; }
        public int DueTodayCount { get; set; }
        public decimal DueTodayValue { get; set; }
        public int NewLeads7d { get; set; }
        public int UnassignedLeads { get; set; }
        public int UnassignedCount { get; set; }
        public int HighValueCount { get; set; }
        public int MissingFollowUpCount { get; set; }
        public int ValueTbdCount { get; set; }
        public int MissingClientLinkCount { get; set; }
    }
}

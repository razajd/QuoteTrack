using System;
using System.Collections.Generic;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Application.DTOs
{
    public class CommandCenterSnapshotDto
    {
        public string ScopeKey { get; set; } = string.Empty;
        public DateTime LastRefreshedAt { get; set; }
        public bool IsRefreshing { get; set; }
        public bool IsStale { get; set; }
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

        public List<CommandCenterQueueItemDto> QueueItems { get; set; } = new();
        public List<CommandCenterActivityItemDto> ActivityItems { get; set; } = new();
        public List<CommandCenterRadarItemDto> StatusRadarRows { get; set; } = new();
        public List<CommandCenterRadarItemDto> AgingRadarRows { get; set; } = new();
    }

    public class CommandCenterQueueItemDto
    {
        public Guid QuoteId { get; set; }
        public QuoteRecordType RecordType { get; set; }
        public string RecordLabel { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerLabel { get; set; } = string.Empty;
        public string ClientLabel { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime? DueUtc { get; set; }
        public string DueLocalText { get; set; } = string.Empty;
        public decimal? Value { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string LastNotePreview { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
    }

    public class CommandCenterActivityItemDto
    {
        public DateTime WhenUtc { get; set; }
        public string WhenLocal { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
    }

    public class CommandCenterRadarItemDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percent { get; set; }
        public string ValueText { get; set; } = string.Empty;
    }
}

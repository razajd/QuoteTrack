using System;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Domain.Entities
{
    public class CommandCenterQueueItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ScopeKey { get; set; } = string.Empty;
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
        public int SortRank { get; set; }
    }
}

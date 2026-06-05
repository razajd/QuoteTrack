using System;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Domain.Entities
{
    public class QuoteListItem
    {
        public Guid QuoteId { get; set; }
        public QuoteRecordType RecordType { get; set; }
        public QuoteStatus Status { get; set; }
        public bool IsDeleteRequested { get; set; }

        public string? OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;

        public Guid? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string QuoteReference { get; set; } = string.Empty;

        public decimal? QuoteValue { get; set; }
        public int WinProbability { get; set; }
        public string Currency { get; set; } = string.Empty;

        public DateTime? NextFollowUpDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime EmailReceivedDateTime { get; set; }
        public DateTime? RepCompletedAt { get; set; }

        public DateTime? LastNoteAt { get; set; }
        public string LastNotePreview { get; set; } = string.Empty;

        public bool IsClosed { get; set; }
        public bool IsUnassigned { get; set; }
        public bool MissingFollowUp { get; set; }
        public bool ValueTbd { get; set; }
        public bool MissingClientLink { get; set; }

        public string DeleteRequestReason { get; set; } = string.Empty;
        public string DeleteRequestedByUserId { get; set; } = string.Empty;
        public string LeadSource { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
    }
}

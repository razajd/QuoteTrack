using System;
using System.Collections.Generic;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Domain.Entities
{
    public class Quote
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;

        public DateTime EmailReceivedDateTime { get; set; }
        public DateTime EmailSentDateTime { get; set; }
        public string EmailMessageId { get; set; } = string.Empty;

        public QuoteRecordType RecordType { get; set; } = QuoteRecordType.Lead;

        public decimal? QuoteValue { get; set; }
        public string? Currency { get; set; }
        public string? QuoteReference { get; set; }
        public string? ClientName { get; set; }

        public string? SolutionSummary { get; set; }
        public DateTime? QuoteDate { get; set; }

        // ✅ Discovery / Requirements notes
        public string? DiscoveryNotes { get; set; }
        public DateTime? DiscoveryNotesUpdatedAt { get; set; }
        public string? DiscoveryNotesUpdatedByUserId { get; set; }

        public QuoteStatus Status { get; set; } = QuoteStatus.LeadNew;

        public DateTime? NextFollowUpDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int WinProbability { get; set; } = 10;

        public bool IsDeleteRequested { get; set; } = false;
        public string? DeleteRequestReason { get; set; }
        public string? DeleteRequestedByUserId { get; set; }

        public string? LeadSource { get; set; }

        public string? RepAttachedQuoteReference { get; set; }
        public string? RepCompletionNotes { get; set; }
        public DateTime? RepCompletedAt { get; set; }

        public string? ClosedByUserId { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? ClosureNotes { get; set; }

        public bool IsMerged { get; set; } = false;
        public Guid? MergedIntoQuoteId { get; set; }
        public string? MergeNotes { get; set; }

        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();

        public Guid? ClientId { get; set; }
        public Client? Client { get; set; }

        public string? OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }
    }
}
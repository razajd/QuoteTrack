using System;

namespace QuoteTrack.Domain.Entities
{
    public class MergeRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SourceQuoteId { get; set; }
        public Guid TargetQuoteId { get; set; }

        public string RequestedByUserId { get; set; } = string.Empty;
        public string? Reason { get; set; }

        public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected

        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
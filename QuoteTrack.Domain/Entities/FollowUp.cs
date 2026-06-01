// QuoteTrack.Domain/Entities/FollowUp.cs
using System;

namespace QuoteTrack.Domain.Entities
{
    public class FollowUp
    {
        public Guid Id { get; set; }
        public Guid QuoteId { get; set; }

        // Audit: who created the note/snooze
        public string? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public bool IsCompleted { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Quote? Quote { get; set; }
    }
}
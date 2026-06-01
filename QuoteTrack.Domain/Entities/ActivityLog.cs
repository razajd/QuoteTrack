using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuoteTrack.Domain.Entities
{
    public class ActivityLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty; // e.g., "Updated Quote", "Assigned Lead"

        public string? Details { get; set; } // e.g., "Changed status to Won for Ministry of Justice"

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional link back to a specific quote if we want to filter history by project
        public Guid? RelatedQuoteId { get; set; }
    }
}
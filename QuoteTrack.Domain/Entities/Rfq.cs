using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Domain.Entities
{
    public class Rfq
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string ClientEmail { get; set; } = string.Empty;

        public string? ClientName { get; set; }

        [Required]
        public string Subject { get; set; } = string.Empty;

        public string? Body { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        public RfqStatus Status { get; set; } = RfqStatus.Unassigned;

        // --- The 3 Missing Properties for the Email Parser ---
        public string? ContactNumber { get; set; }

        public string? Website { get; set; }

        public string? OriginalForwarderEmail { get; set; }
        // ---------------------------------------------------

        // The team member assigned to handle this lead
        public string? AssignedUserId { get; set; }

        [ForeignKey("AssignedUserId")]
        public ApplicationUser? AssignedUser { get; set; }
    }
}
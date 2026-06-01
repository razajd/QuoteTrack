using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Domain.Entities
{
    public class Client
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string CompanyName { get; set; } = string.Empty;

        public string? Industry { get; set; }
        public string? PrimaryContactName { get; set; }
        public string? PrimaryContactDesignation { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }

        // Additional commonly used fields
        public string? Website { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Notes { get; set; }

        // --- Bahrain Financial Tracking ---
        public string? CommercialRegistrationNumber { get; set; } // CR Number
        public string? TaxRegistrationNumber { get; set; }        // TRN (VAT)
        public string? BillingAddress { get; set; }

        // --- Workflow Moderation ---
        public ClientApprovalStatus ApprovalStatus { get; set; } = ClientApprovalStatus.Pending;

        public string? SubmittedByUserId { get; set; }
        [ForeignKey("SubmittedByUserId")]
        public ApplicationUser? SubmittedBy { get; set; }

        public string? ApprovedByUserId { get; set; }
        [ForeignKey("ApprovedByUserId")]
        public ApplicationUser? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // --- Relationships ---
        // One client can have many quotes
        public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    }
}
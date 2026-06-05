using System;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Domain.Entities
{
    public class QuoteEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid QuoteId { get; set; }
        public Quote? Quote { get; set; }

        public QuoteEventType EventType { get; set; }

        public QuoteStatus? FromStatus { get; set; }
        public QuoteStatus? ToStatus { get; set; }

        public string? FromOwnerId { get; set; }
        public string? ToOwnerId { get; set; }

        public string? ActorUserId { get; set; }
        public ApplicationUser? ActorUser { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string? Details { get; set; }
        public string? MetadataJson { get; set; }
    }
}

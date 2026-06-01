// QuoteTrack.Domain/Entities/Attachment.cs
using System;

namespace QuoteTrack.Domain.Entities
{
    public class Attachment
    {
        public Guid Id { get; set; }
        public Guid QuoteId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty; // SHA256 for deduplication

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Quote? Quote { get; set; }
    }
}
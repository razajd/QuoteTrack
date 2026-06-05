using System;

namespace QuoteTrack.Domain.Entities
{
    public class CommandCenterActivityItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ScopeKey { get; set; } = string.Empty;
        public Guid QuoteId { get; set; }
        public DateTime WhenUtc { get; set; }
        public string WhenLocal { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public int SortRank { get; set; }
    }
}

using System;

namespace QuoteTrack.Domain.Entities
{
    public class ReadModelState
    {
        public string Key { get; set; } = string.Empty;
        public DateTime LastRefreshedAt { get; set; } = DateTime.MinValue;
        public bool IsRefreshing { get; set; }
        public bool IsStale { get; set; } = true;
        public string? LastError { get; set; }
    }
}

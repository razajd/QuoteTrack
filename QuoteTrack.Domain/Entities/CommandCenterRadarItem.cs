using System;

namespace QuoteTrack.Domain.Entities
{
    public class CommandCenterRadarItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ScopeKey { get; set; } = string.Empty;
        public string RadarType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percent { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public int SortRank { get; set; }
    }
}

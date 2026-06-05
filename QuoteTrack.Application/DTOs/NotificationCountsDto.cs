namespace QuoteTrack.Application.DTOs
{
    public class NotificationCountsDto
    {
        public int PendingClientCount { get; set; }
        public int UnassignedLeadCount { get; set; }
        public int PendingDeletionCount { get; set; }
    }
}

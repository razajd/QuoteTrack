namespace QuoteTrack.Domain.Enums
{
    public enum QuoteEventType
    {
        Created = 0,
        StatusChanged = 1,
        OwnerChanged = 2,
        FollowUpAdded = 3,
        FollowUpDateChanged = 4,
        ClientLinked = 5,
        ValueChanged = 6,
        LeadRepCompleted = 7,
        LeadClosed = 8,
        MergeRequested = 9,
        MergeApproved = 10,
        MergeRejected = 11,
        Deleted = 12
    }
}

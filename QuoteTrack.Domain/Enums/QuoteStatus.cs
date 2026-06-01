// QuoteTrack.Domain/Enums/QuoteStatus.cs
namespace QuoteTrack.Domain.Enums
{
    public enum QuoteStatus
    {
        // ===== EXISTING (DO NOT CHANGE VALUES) =====
        New = 0,
        Sent = 1,
        FollowUp = 2,
        Won = 3,
        Lost = 4,
        Cancelled = 5,

        // ===== EXISTING Lead lifecycle (kept for compatibility) =====
        LeadNew = 10,
        LeadInProgress = 11,
        LeadRepCompleted = 12,
        LeadClosed = 13,

        // ===== NEW basic lead workflow statuses =====
        Assigned = 14,
        InProgress = 15,
        ContactMade = 16,
        WaitingClientResponse = 17,
        QuotationInPreparation = 18,
        OnHold = 19,

        // ===== EXISTING Optional Quote lifecycle =====
        QuoteNew = 20,
        QuoteReviewed = 21,
        QuoteApproved = 22,
        QuoteRejected = 23,

        // ===== NEW basic lead workflow status =====
        NoResponse = 24,

        // ===== System =====
        Merged = 90
    }
}
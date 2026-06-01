namespace QuoteTrack.Domain.Enums
{
    public enum ClientApprovalStatus
    {
        Pending,   // Newly added by a Sales Rep, waiting for Finance
        Approved,  // Verified CR/TRN, ready for billing
        Rejected   // Duplicate, invalid CR, or blacklisted
    }
}
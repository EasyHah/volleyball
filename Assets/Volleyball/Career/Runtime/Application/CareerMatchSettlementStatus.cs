namespace Volleyball.Career.Application
{
    public enum CareerMatchSettlementStatus
    {
        Settled = 0,
        Existing = 1,
        SessionResultConflict = 2,
        NotFound = 3,
        InvalidState = 4,
        Abandoned = 5,
        RevisionConflict = 6,
        ValidationFailed = 7
    }

    public enum CareerMatchSettlementFailureKind
    {
        None = 0,
        Command = 1,
        CanonicalPair = 2,
        Persistence = 3,
        Rules = 4
    }
}

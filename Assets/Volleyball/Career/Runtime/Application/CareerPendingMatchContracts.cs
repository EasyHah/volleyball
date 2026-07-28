using System;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public sealed class CreatePendingMatchCommand
    {
        public CreatePendingMatchCommand(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            long completedAtUtcMs,
            Guid sessionId,
            WeekPlanId weekPlanId,
            SlotActionId slotActionId,
            OccurrenceId actionOccurrenceId,
            CareerMatchPriority preMatchPriority)
        {
            ProfileId = profileId;
            SaveId = saveId;
            ExpectedVersionToken = expectedVersionToken;
            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
            SessionId = sessionId;
            WeekPlanId = weekPlanId;
            SlotActionId = slotActionId;
            ActionOccurrenceId = actionOccurrenceId;
            PreMatchPriority = preMatchPriority;
        }

        public ProfileId ProfileId { get; }
        public SaveId SaveId { get; }
        public CareerVersionToken ExpectedVersionToken { get; }
        public OperationId OperationId { get; }
        public long CompletedAtUtcMs { get; }
        public Guid SessionId { get; }
        public WeekPlanId WeekPlanId { get; }
        public SlotActionId SlotActionId { get; }
        public OccurrenceId ActionOccurrenceId { get; }
        public CareerMatchPriority PreMatchPriority { get; }
    }

    public sealed class RetryPendingMatchExecutionCommand
    {
        public RetryPendingMatchExecutionCommand(ProfileId profileId, SaveId saveId, Guid sessionId)
        {
            ProfileId = profileId;
            SaveId = saveId;
            SessionId = sessionId;
        }

        public ProfileId ProfileId { get; }
        public SaveId SaveId { get; }
        public Guid SessionId { get; }
    }

    public enum CareerPendingCreationDisposition
    {
        Created = 0,
        Existing = 1
    }

    public enum CareerPendingMatchFlowStatus
    {
        AwaitingSettlement = 0,
        ExecutionFailed = 1,
        NotFound = 2,
        InvalidState = 3,
        OperationConflict = 4,
        RevisionConflict = 5,
        ValidationFailed = 6,
        Cancelled = 7
    }

    public enum CareerPendingMatchFailureKind
    {
        None = 0,
        Command = 1,
        Persistence = 2,
        Dependency = 3,
        Execution = 4,
        Cancellation = 5
    }

    public sealed class CareerPendingMatchFlowResult
    {
        private readonly byte[] _canonicalContextUtf8;
        private readonly byte[] _canonicalResultUtf8;

        public CareerPendingMatchFlowResult(
            CareerPendingMatchFlowStatus status,
            CareerPendingCreationDisposition? creationDisposition,
            PersistenceResultKind? persistenceKind,
            CareerSaveSnapshot snapshot,
            long? committedRevision,
            OperationReceipt conflictingReceipt,
            Guid? sessionId,
            Sha256Digest? contextDigest,
            byte[] canonicalContextUtf8,
            Sha256Digest? resultDigest,
            byte[] canonicalResultUtf8,
            CareerPendingMatchFailureKind failureKind,
            string failureCode)
        {
            Status = status;
            CreationDisposition = creationDisposition;
            PersistenceKind = persistenceKind;
            Snapshot = snapshot;
            CommittedRevision = committedRevision;
            ConflictingReceipt = conflictingReceipt;
            SessionId = sessionId;
            ContextDigest = contextDigest;
            _canonicalContextUtf8 = canonicalContextUtf8 == null
                ? null
                : (byte[])canonicalContextUtf8.Clone();
            ResultDigest = resultDigest;
            _canonicalResultUtf8 = canonicalResultUtf8 == null
                ? null
                : (byte[])canonicalResultUtf8.Clone();
            FailureKind = failureKind;
            FailureCode = failureCode;
        }

        public CareerPendingMatchFlowStatus Status { get; }
        public CareerPendingCreationDisposition? CreationDisposition { get; }
        public PersistenceResultKind? PersistenceKind { get; }
        public CareerSaveSnapshot Snapshot { get; }
        public OperationReceipt ConflictingReceipt { get; }
        public Guid? SessionId { get; }
        public Sha256Digest? ContextDigest { get; }
        public byte[] CanonicalContextUtf8 => _canonicalContextUtf8 == null
            ? null
            : (byte[])_canonicalContextUtf8.Clone();
        public Sha256Digest? ResultDigest { get; }
        public byte[] CanonicalResultUtf8 => _canonicalResultUtf8 == null
            ? null
            : (byte[])_canonicalResultUtf8.Clone();
        public long? CommittedRevision { get; }
        public CareerPendingMatchFailureKind FailureKind { get; }
        public string FailureCode { get; }
    }

    public sealed class CareerFirstMatchLaunchRequest
    {
        public CareerFirstMatchLaunchRequest(
            CareerMatchVersions versions,
            Guid sessionId,
            uint matchSeed,
            TeamId homeTeamId,
            PlayerId protagonistPlayerId,
            int protagonistJerseyNumber,
            int protagonistFatigue,
            CareerPlayerAttributes protagonistAttributes,
            CareerMatchPriority preMatchPriority)
        {
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
            SessionId = sessionId;
            MatchSeed = matchSeed;
            HomeTeamId = homeTeamId;
            ProtagonistPlayerId = protagonistPlayerId;
            ProtagonistJerseyNumber = protagonistJerseyNumber;
            ProtagonistFatigue = protagonistFatigue;
            ProtagonistAttributes = protagonistAttributes ??
                                    throw new ArgumentNullException(nameof(protagonistAttributes));
            PreMatchPriority = preMatchPriority;
        }

        public CareerMatchVersions Versions { get; }
        public Guid SessionId { get; }
        public uint MatchSeed { get; }
        public TeamId HomeTeamId { get; }
        public PlayerId ProtagonistPlayerId { get; }
        public int ProtagonistJerseyNumber { get; }
        public int ProtagonistFatigue { get; }
        public CareerPlayerAttributes ProtagonistAttributes { get; }
        public CareerMatchPriority PreMatchPriority { get; }
    }

    public interface ICareerFirstMatchLaunchFactory
    {
        CareerMatchLaunch Create(CareerFirstMatchLaunchRequest request);
    }
}

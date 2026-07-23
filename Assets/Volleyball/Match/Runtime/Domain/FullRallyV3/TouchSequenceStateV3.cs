using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum RallyContactClassificationV3
    {
        ServeContact,
        TeamContact,
        BlockContact,
        SimultaneousTeamContact,
        EnvironmentContact
    }

    public enum RuleRejectionReasonV3
    {
        None,
        DuplicateContactGroup,
        RallyClosed,
        ConsecutiveCountedContact,
        FourthCountedContact,
        ActorNotOnCourt,
        ActionIneligible
    }

    public sealed class ActualContactEventV3
    {
        public ActualContactEventV3(
            PlayerId actor,
            TeamSide team,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            if (classification == RallyContactClassificationV3.EnvironmentContact)
            {
                throw new ArgumentException("Use Environment for environment contacts.", nameof(classification));
            }

            PlayerWorldSnapshotV3.RequirePlayerId(actor, nameof(actor));
            Team = PlayerWorldSnapshotV3.RequireDefinedEnum(team, nameof(team));
            Classification = RequireClassification(classification, nameof(classification));
            ContactGroup = RequireContactGroup(contactGroup, nameof(contactGroup));
            Actor = actor;
        }

        private ActualContactEventV3(long contactGroup)
        {
            Classification = RallyContactClassificationV3.EnvironmentContact;
            ContactGroup = RequireContactGroup(contactGroup, nameof(contactGroup));
        }

        public PlayerId? Actor { get; }

        public TeamSide? Team { get; }

        public RallyContactClassificationV3 Classification { get; }

        public long ContactGroup { get; }

        public static ActualContactEventV3 Environment(long contactGroup)
        {
            return new ActualContactEventV3(contactGroup);
        }

        private static RallyContactClassificationV3 RequireClassification(
            RallyContactClassificationV3 value,
            string paramName)
        {
            if (!Enum.IsDefined(typeof(RallyContactClassificationV3), value))
            {
                throw new ArgumentOutOfRangeException(paramName, "Contact classification is not supported.");
            }

            return value;
        }

        private static long RequireContactGroup(long value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Contact group must be non-negative.");
            }

            return value;
        }
    }

    public sealed class RuleTransitionV3
    {
        internal RuleTransitionV3(
            bool accepted,
            RuleRejectionReasonV3 rejectionReason,
            TouchSequenceStateV3 before,
            TouchSequenceStateV3 after)
        {
            if (!Enum.IsDefined(typeof(RuleRejectionReasonV3), rejectionReason))
            {
                throw new ArgumentOutOfRangeException(nameof(rejectionReason), "Rejection reason is not supported.");
            }

            if (accepted != (rejectionReason == RuleRejectionReasonV3.None))
            {
                throw new ArgumentException(
                    "Accepted transitions require None; rejected transitions require a rejection reason.",
                    nameof(rejectionReason));
            }

            Accepted = accepted;
            RejectionReason = rejectionReason;
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
        }

        public bool Accepted { get; }

        public RuleRejectionReasonV3 RejectionReason { get; }

        public TouchSequenceStateV3 Before { get; }

        public TouchSequenceStateV3 After { get; }
    }

    public sealed class TouchSequenceStateV3
    {
        internal TouchSequenceStateV3(
            TeamSide? lastLegalPhysicalContactTeam,
            TeamSide? currentCountedSequenceTeam,
            int countedHits,
            PlayerId? lastCountedActor,
            RallyContactClassificationV3? lastContactClassification,
            long? lastContactGroup,
            bool isTerminal)
        {
            if (lastLegalPhysicalContactTeam.HasValue)
            {
                PlayerWorldSnapshotV3.RequireDefinedEnum(lastLegalPhysicalContactTeam.Value, nameof(lastLegalPhysicalContactTeam));
            }

            if (currentCountedSequenceTeam.HasValue)
            {
                PlayerWorldSnapshotV3.RequireDefinedEnum(currentCountedSequenceTeam.Value, nameof(currentCountedSequenceTeam));
            }

            if (countedHits < 0 || countedHits > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(countedHits), "Counted hits must be from zero through three.");
            }

            if (countedHits == 0 && (currentCountedSequenceTeam.HasValue || lastCountedActor.HasValue))
            {
                throw new ArgumentException("A zero-hit state cannot retain counted-contact ownership.");
            }

            if (countedHits > 0 && (!currentCountedSequenceTeam.HasValue || !lastCountedActor.HasValue))
            {
                throw new ArgumentException("A counted state requires a team and actor.");
            }

            LastLegalPhysicalContactTeam = lastLegalPhysicalContactTeam;
            CurrentCountedSequenceTeam = currentCountedSequenceTeam;
            CountedHits = countedHits;
            LastCountedActor = lastCountedActor;
            LastContactClassification = lastContactClassification;
            LastContactGroup = lastContactGroup;
            IsTerminal = isTerminal;
        }

        public static TouchSequenceStateV3 Initial { get; } = new TouchSequenceStateV3(
            null, null, 0, null, null, null, false);

        public TeamSide? LastLegalPhysicalContactTeam { get; }

        public TeamSide? CurrentCountedSequenceTeam { get; }

        public int CountedHits { get; }

        public PlayerId? LastCountedActor { get; }

        public RallyContactClassificationV3? LastContactClassification { get; }

        public long? LastContactGroup { get; }

        public int RemainingHits => 3 - CountedHits;

        public bool IsTerminal { get; }
    }
}

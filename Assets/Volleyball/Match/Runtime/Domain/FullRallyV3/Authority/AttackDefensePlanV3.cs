using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;
using static Volleyball.Match.Domain.FullRallyV3.AttackDefensePlanValidationV3;

namespace Volleyball.Match.Domain.FullRallyV3
{
    // Gate I values deliberately contain facts and selections only. Command creation
    // belongs to the coordinator/controller boundary, never to this Domain assembly.
    public enum AttackActionClassV3
    {
        PowerLine, PowerCross, PowerEdge, PowerOverHand, Tip, Roll, Push,
        HighSurvival, BlockOut, BlockToolRecovery
    }

    public enum DefenseResponsibilityKindV3
    {
        PrimaryBlock, SupportingBlock, LineDefense, CrossDefense, DeepDefense,
        TipDefense, BlockShadow, ReboundCoverage
    }

    public sealed class AttackCandidateV3
    {
        public AttackCandidateV3(string candidateIdentity, PlayerId actor,
            AttackActionClassV3 actionClass, SimVector3 contactCenter,
            SimVector3 target, float expectedRallyValue, float legalSampleRatio,
            bool isQualifiedPowerRoute, string eliminationReason,
            string envelopeIdentity, string trajectoryArtifactIdentity,
            string reorganizationExitIdentity = "")
        {
            CandidateIdentity = Text(candidateIdentity, nameof(candidateIdentity));
            Actor = PlayerWorldSnapshotV3.RequirePlayerId(actor, nameof(actor));
            ActionClass = EnumValue(actionClass, nameof(actionClass));
            ContactCenter = Finite(contactCenter, nameof(contactCenter));
            Target = Finite(target, nameof(target));
            ExpectedRallyValue = Finite(expectedRallyValue, nameof(expectedRallyValue));
            if (float.IsNaN(legalSampleRatio) || float.IsInfinity(legalSampleRatio) || legalSampleRatio < 0f || legalSampleRatio > 1f)
                throw new ArgumentOutOfRangeException(nameof(legalSampleRatio));
            LegalSampleRatio = legalSampleRatio;
            IsQualifiedPowerRoute = isQualifiedPowerRoute;
            EliminationReason = eliminationReason == null ? string.Empty : eliminationReason.Trim();
            EnvelopeIdentity = Text(envelopeIdentity, nameof(envelopeIdentity));
            TrajectoryArtifactIdentity = Text(trajectoryArtifactIdentity, nameof(trajectoryArtifactIdentity));
            ReorganizationExitIdentity = reorganizationExitIdentity == null ? string.Empty : reorganizationExitIdentity.Trim();
        }
        public string CandidateIdentity { get; }
        public PlayerId Actor { get; }
        public AttackActionClassV3 ActionClass { get; }
        public SimVector3 ContactCenter { get; }
        public SimVector3 Target { get; }
        public float ExpectedRallyValue { get; }
        public float LegalSampleRatio { get; }
        public bool IsQualifiedPowerRoute { get; }
        public string EliminationReason { get; }
        public string EnvelopeIdentity { get; }
        public string TrajectoryArtifactIdentity { get; }
        // A tool-recovery candidate must name the only declared continuation exit.
        // Other attack classes deliberately leave this empty.
        public string ReorganizationExitIdentity { get; }
    }

    public sealed class PublicAttackThreatEntryV3
    {
        public PublicAttackThreatEntryV3(AttackActionClassV3 actionClass, string zone, float probability, float arrivalTime)
        {
            ActionClass = EnumValue(actionClass, nameof(actionClass));
            Zone = Text(zone, nameof(zone));
            if (float.IsNaN(probability) || float.IsInfinity(probability) || probability < 0f || probability > 1f) throw new ArgumentOutOfRangeException(nameof(probability));
            Probability = probability;
            ArrivalTime = Finite(arrivalTime, nameof(arrivalTime));
        }
        public AttackActionClassV3 ActionClass { get; }
        public string Zone { get; }
        public float Probability { get; }
        public float ArrivalTime { get; }
    }

    public sealed class PublicAttackThreatV3
    {
        public PublicAttackThreatV3(string threatIdentity, IReadOnlyList<PublicAttackThreatEntryV3> entries)
        {
            ThreatIdentity = Text(threatIdentity, nameof(threatIdentity));
            if (entries == null || entries.Count == 0) throw new ArgumentException("Threat entries are required.", nameof(entries));
            Entries = new ReadOnlyCollection<PublicAttackThreatEntryV3>(entries.Select(value => value ?? throw new ArgumentException("Threat entries cannot be null.", nameof(entries))).ToArray());
        }
        public string ThreatIdentity { get; }
        public IReadOnlyList<PublicAttackThreatEntryV3> Entries { get; }
    }

    public sealed class DefenseResponsibilityV3
    {
        public DefenseResponsibilityV3(PlayerId actor, DefenseResponsibilityKindV3 kind, string zone, RallyPlanBranchV3 branch)
        {
            Actor = PlayerWorldSnapshotV3.RequirePlayerId(actor, nameof(actor));
            Kind = EnumValue(kind, nameof(kind));
            Zone = Text(zone, nameof(zone));
            Branch = PlayerWorldSnapshotV3.RequireDefinedEnum(branch, nameof(branch));
        }
        public PlayerId Actor { get; }
        public DefenseResponsibilityKindV3 Kind { get; }
        public string Zone { get; }
        public RallyPlanBranchV3 Branch { get; }
    }

    public sealed class ReorganizationExitV3
    {
        public ReorganizationExitV3(string identity, PlayerId actor, string kind)
        {
            Identity = Text(identity, nameof(identity));
            Actor = PlayerWorldSnapshotV3.RequirePlayerId(actor, nameof(actor));
            Kind = Text(kind, nameof(kind));
        }
        public string Identity { get; }
        public PlayerId Actor { get; }
        public string Kind { get; }
    }

    public sealed class GateISetIntentV3
    {
        public GateISetIntentV3(long planRevision, long sourceSequence, PlayerId organizer,
            PlayerId preparedAttacker, SimVector3 target, float gateHExpectedContactTime,
            ExecutionSampleClassificationV4 executionClassification,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact)
        {
            if (planRevision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(planRevision < 0 ? nameof(planRevision) : nameof(sourceSequence));
            PlanRevision = planRevision; SourceSequence = sourceSequence;
            Organizer = PlayerWorldSnapshotV3.RequirePlayerId(organizer, nameof(organizer));
            PreparedAttacker = PlayerWorldSnapshotV3.RequirePlayerId(preparedAttacker, nameof(preparedAttacker));
            Target = Finite(target, nameof(target)); GateHExpectedContactTime = Finite(gateHExpectedContactTime, nameof(gateHExpectedContactTime));
            ExecutionClassification = executionClassification ?? throw new ArgumentNullException(nameof(executionClassification));
            if (executionClassification.ExecutableEnvelope == null) throw new ArgumentException("Set intent requires an executable envelope.", nameof(executionClassification));
            TrajectoryArtifact = trajectoryArtifact ?? throw new ArgumentNullException(nameof(trajectoryArtifact));
        }
        public long PlanRevision { get; }
        public long SourceSequence { get; }
        public PlayerId Organizer { get; }
        public PlayerId PreparedAttacker { get; }
        public SimVector3 Target { get; }
        public float GateHExpectedContactTime { get; }
        public ExecutionSampleClassificationV4 ExecutionClassification { get; }
        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
    }

    public sealed class JointDefensePlanV3
    {
        public JointDefensePlanV3(IReadOnlyList<DefenseResponsibilityV3> responsibilities, IReadOnlyList<ReorganizationExitV3> reorganizationExits)
            : this(string.Empty, responsibilities, reorganizationExits, Array.Empty<string>(), Array.Empty<string>())
        {
        }

        public JointDefensePlanV3(string sourceThreatIdentity, IReadOnlyList<DefenseResponsibilityV3> responsibilities,
            IReadOnlyList<ReorganizationExitV3> reorganizationExits, IReadOnlyList<string> blockedZones,
            IReadOnlyList<string> floorCoveredZones)
        {
            SourceThreatIdentity = sourceThreatIdentity ?? string.Empty;
            if (responsibilities == null || responsibilities.Count != 6) throw new ArgumentException("Exactly six defense responsibilities are required.", nameof(responsibilities));
            var copy = responsibilities.Select(value => value ?? throw new ArgumentException("Responsibilities cannot be null.", nameof(responsibilities))).ToArray();
            if (copy.Select(value => value.Actor).Distinct().Count() != 6) throw new ArgumentException("Responsibilities require distinct actors.", nameof(responsibilities));
            Responsibilities = new ReadOnlyCollection<DefenseResponsibilityV3>(copy);
            ReorganizationExits = CopyExits(reorganizationExits);
            BlockedZones = CopyZones(blockedZones, nameof(blockedZones));
            FloorCoveredZones = CopyZones(floorCoveredZones, nameof(floorCoveredZones));
        }
        public string SourceThreatIdentity { get; }
        public IReadOnlyList<DefenseResponsibilityV3> Responsibilities { get; }
        public IReadOnlyList<ReorganizationExitV3> ReorganizationExits { get; }
        public IReadOnlyList<string> BlockedZones { get; }
        public IReadOnlyList<string> FloorCoveredZones { get; }

        public override bool Equals(object obj)
        {
            if (!(obj is JointDefensePlanV3 other) || SourceThreatIdentity != other.SourceThreatIdentity) return false;
            return Responsibilities.Select(ValueKey).SequenceEqual(other.Responsibilities.Select(ValueKey)) &&
                ReorganizationExits.Select(value => value.Identity).SequenceEqual(other.ReorganizationExits.Select(value => value.Identity)) &&
                BlockedZones.SequenceEqual(other.BlockedZones) && FloorCoveredZones.SequenceEqual(other.FloorCoveredZones);
        }

        public override int GetHashCode() => SourceThreatIdentity.GetHashCode();
        private static string ValueKey(DefenseResponsibilityV3 value) => value.Actor.Value + ":" + (int)value.Kind + ":" + value.Zone + ":" + (int)value.Branch;
        private static IReadOnlyList<string> CopyZones(IReadOnlyList<string> zones, string name)
        {
            if (zones == null) throw new ArgumentNullException(name);
            return new ReadOnlyCollection<string>(zones.Select(value => Text(value, name)).Distinct(StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class AttackDefensePlanV3
    {
        public AttackDefensePlanV3(TeamSide attackingSide, long revision, string sourcePlanIdentity,
            GateISetIntentV3 setIntent, IReadOnlyList<AttackCandidateV3> attackCandidates,
            PublicAttackThreatV3 publicThreat, JointDefensePlanV3 defense,
            AttackCandidateV3 selectedAction, IReadOnlyList<ReorganizationExitV3> reorganizationExits)
        {
            AttackingSide = PlayerWorldSnapshotV3.RequireDefinedEnum(attackingSide, nameof(attackingSide));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision; SourcePlanIdentity = Text(sourcePlanIdentity, nameof(sourcePlanIdentity));
            SetIntent = setIntent ?? throw new ArgumentNullException(nameof(setIntent));
            if (setIntent.PlanRevision != revision) throw new ArgumentException("Set intent revision must match plan revision.", nameof(setIntent));
            if (attackCandidates == null || attackCandidates.Count == 0) throw new ArgumentException("Attack candidates are required.", nameof(attackCandidates));
            var candidates = attackCandidates.Select(value => value ?? throw new ArgumentException("Candidates cannot be null.", nameof(attackCandidates))).ToArray();
            if (candidates.Select(value => value.CandidateIdentity).Distinct(StringComparer.Ordinal).Count() != candidates.Length) throw new ArgumentException("Candidate identities must be distinct.", nameof(attackCandidates));
            AttackCandidates = new ReadOnlyCollection<AttackCandidateV3>(candidates);
            PublicThreat = publicThreat ?? throw new ArgumentNullException(nameof(publicThreat));
            Defense = defense ?? throw new ArgumentNullException(nameof(defense));
            if (selectedAction != null && !candidates.Any(value => value.CandidateIdentity == selectedAction.CandidateIdentity)) throw new ArgumentException("Selected action must be an attack candidate.", nameof(selectedAction));
            SelectedAction = selectedAction; ReorganizationExits = CopyExits(reorganizationExits);
        }
        public TeamSide AttackingSide { get; }
        public long Revision { get; }
        public string SourcePlanIdentity { get; }
        public GateISetIntentV3 SetIntent { get; }
        public IReadOnlyList<AttackCandidateV3> AttackCandidates { get; }
        public PublicAttackThreatV3 PublicThreat { get; }
        public JointDefensePlanV3 Defense { get; }
        public AttackCandidateV3 SelectedAction { get; }
        public IReadOnlyList<ReorganizationExitV3> ReorganizationExits { get; }
    }

    internal static class AttackDefensePlanValidationV3
    {
        public static string Text(string value, string name) => PlayerWorldSnapshotV3.RequireText(value, name);
        public static SimVector3 Finite(SimVector3 value, string name) { if (!value.IsFinite) throw new ArgumentOutOfRangeException(name); return value; }
        public static float Finite(float value, string name) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); return value; }
        public static T EnumValue<T>(T value, string name) where T : struct { if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(name); return value; }
        public static IReadOnlyList<ReorganizationExitV3> CopyExits(IReadOnlyList<ReorganizationExitV3> exits)
        {
            if (exits == null || exits.Count == 0) throw new ArgumentException("At least one reorganization exit is required.", nameof(exits));
            var copy = exits.Select(value => value ?? throw new ArgumentException("Reorganization exits cannot be null.", nameof(exits))).ToArray();
            if (copy.Select(value => value.Identity).Distinct(StringComparer.Ordinal).Count() != copy.Length) throw new ArgumentException("Reorganization exit identities must be distinct.", nameof(exits));
            return new ReadOnlyCollection<ReorganizationExitV3>(copy);
        }
    }
}

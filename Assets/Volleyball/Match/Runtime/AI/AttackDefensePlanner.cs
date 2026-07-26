using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.AI
{
    // These values form the pre-/post-Set boundary.  In particular, neither the
    // request nor the planner has a Set command, timing command, or facade input.
    public sealed class SetIntentPlanningRequestV3
    {
        public SetIntentPlanningRequestV3(long revision, long sourceSequence, TeamSide attackingSide, PlayerId organizer,
            PlayerId preparedAttacker, SimVector3 target, float expectedSetContactTime,
            ExecutionSampleClassificationV4 executionClassification,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact)
        {
            if (revision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(revision < 0 ? nameof(revision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(TeamSide), attackingSide)) throw new ArgumentOutOfRangeException(nameof(attackingSide));
            Revision = revision; SourceSequence = sourceSequence; AttackingSide = attackingSide; Organizer = organizer; PreparedAttacker = preparedAttacker;
            if (!target.IsFinite || float.IsNaN(expectedSetContactTime) || float.IsInfinity(expectedSetContactTime)) throw new ArgumentOutOfRangeException(nameof(target));
            if (executionClassification == null || executionClassification.ExecutableEnvelope == null) throw new ArgumentException("An executable Set envelope is required.", nameof(executionClassification));
            Target = target; ExpectedSetContactTime = expectedSetContactTime; ExecutionClassification = executionClassification;
            TrajectoryArtifact = trajectoryArtifact ?? throw new ArgumentNullException(nameof(trajectoryArtifact));
        }
        public long Revision { get; } public long SourceSequence { get; } public TeamSide AttackingSide { get; } public PlayerId Organizer { get; } public PlayerId PreparedAttacker { get; }
        public SimVector3 Target { get; } public float ExpectedSetContactTime { get; }
        public ExecutionSampleClassificationV4 ExecutionClassification { get; } public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
    }

    public sealed class AcceptedSetEvidenceV3
    {
        public AcceptedSetEvidenceV3(PlayerId actor, string envelopeIdentity, string trajectoryArtifactIdentity)
        {
            Actor = actor;
            EnvelopeIdentity = Require(envelopeIdentity, nameof(envelopeIdentity));
            TrajectoryArtifactIdentity = Require(trajectoryArtifactIdentity, nameof(trajectoryArtifactIdentity));
        }
        public PlayerId Actor { get; } public string EnvelopeIdentity { get; } public string TrajectoryArtifactIdentity { get; }
        private static string Require(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Value is required.", name);
    }

    public sealed class AttackPlanningRequestV3
    {
        public AttackPlanningRequestV3(long revision, GateISetIntentV3 setIntent, AcceptedSetEvidenceV3 actualSet,
            IReadOnlyList<RallyPlayerSnapshot> attackers, OnCourtEligibilitySnapshot eligibility,
            IReadOnlyList<AttackCandidateV3> candidates)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision; SetIntent = setIntent ?? throw new ArgumentNullException(nameof(setIntent));
            ActualSet = actualSet ?? throw new ArgumentNullException(nameof(actualSet)); Eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            if (attackers == null || attackers.Count == 0) throw new ArgumentException("At least one attacker is required.", nameof(attackers));
            Attackers = new ReadOnlyCollection<RallyPlayerSnapshot>(attackers.ToArray());
            Candidates = Copy(candidates, nameof(candidates));
        }
        public long Revision { get; } public GateISetIntentV3 SetIntent { get; } public AcceptedSetEvidenceV3 ActualSet { get; }
        public IReadOnlyList<RallyPlayerSnapshot> Attackers { get; } public OnCourtEligibilitySnapshot Eligibility { get; }
        // Candidate generation is deliberately injected as immutable evaluator output for this task.
        public IReadOnlyList<AttackCandidateV3> Candidates { get; }
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, string name) where T : class
        {
            if (values == null || values.Count == 0) throw new ArgumentException("At least one value is required.", name);
            return new ReadOnlyCollection<T>(values.Select(x => x ?? throw new ArgumentException("Values cannot contain null.", name)).ToArray());
        }
    }

    public sealed class AttackPlanningResultV3
    {
        public AttackPlanningResultV3(IReadOnlyList<AttackCandidateV3> candidates, IReadOnlyList<AttackCandidateV3> qualifiedPowerRoutes,
            IReadOnlyList<AttackCandidateV3> fallbackCandidates, PublicAttackThreatV3 publicThreat)
        {
            Candidates = Copy(candidates, nameof(candidates)); QualifiedPowerRoutes = Copy(qualifiedPowerRoutes, nameof(qualifiedPowerRoutes));
            FallbackCandidates = Copy(fallbackCandidates, nameof(fallbackCandidates)); PublicThreat = publicThreat ?? throw new ArgumentNullException(nameof(publicThreat));
        }
        // No AttackDefensePlanV3 exists here: defense is not available until Task 5.
        public IReadOnlyList<AttackCandidateV3> Candidates { get; } public IReadOnlyList<AttackCandidateV3> QualifiedPowerRoutes { get; }
        public IReadOnlyList<AttackCandidateV3> FallbackCandidates { get; } public PublicAttackThreatV3 PublicThreat { get; }
        private static IReadOnlyList<AttackCandidateV3> Copy(IReadOnlyList<AttackCandidateV3> values, string name) =>
            new ReadOnlyCollection<AttackCandidateV3>((values ?? throw new ArgumentNullException(name)).ToArray());
    }

    public sealed class FinalAttackChoiceV3
    {
        public FinalAttackChoiceV3(AttackCandidateV3 candidate, IReadOnlyList<AttackCandidateV3> comparableFallbacks)
        { Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate)); ComparableFallbacks = new ReadOnlyCollection<AttackCandidateV3>((comparableFallbacks ?? throw new ArgumentNullException(nameof(comparableFallbacks))).ToArray()); }
        public AttackCandidateV3 Candidate { get; } public IReadOnlyList<AttackCandidateV3> ComparableFallbacks { get; }
    }

    public sealed class AttackDefensePlanner
    {
        public GateISetIntentV3 PlanSetIntent(SetIntentPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new GateISetIntentV3(request.Revision, request.SourceSequence, request.Organizer, request.PreparedAttacker,
                request.Target, request.ExpectedSetContactTime, request.ExecutionClassification, request.TrajectoryArtifact);
        }

        public AttackPlanningResultV3 PlanAttack(AttackPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.SetIntent.PlanRevision != request.Revision || !request.SetIntent.Organizer.Equals(request.ActualSet.Actor) ||
                request.SetIntent.ExecutionClassification.ExecutableEnvelope.Identity != request.ActualSet.EnvelopeIdentity ||
                request.SetIntent.TrajectoryArtifact.ArtifactIdentity != request.ActualSet.TrajectoryArtifactIdentity)
                throw new ArgumentException("Accepted Set evidence does not match the immutable SetIntent.", nameof(request));
            var ordered = request.Candidates.OrderByDescending(c => c.IsQualifiedPowerRoute).ThenByDescending(c => c.ExpectedRallyValue)
                .ThenBy(c => c.Actor.ToString(), StringComparer.Ordinal).ThenBy(c => (int)c.ActionClass).ThenBy(c => c.CandidateIdentity, StringComparer.Ordinal).ToArray();
            var power = ordered.Where(c => IsPower(c.ActionClass) && c.IsQualifiedPowerRoute && c.LegalSampleRatio >= 0.6f).ToArray();
            var fallback = ordered.Where(c => !IsPower(c.ActionClass)).ToArray();
            var threatSource = power.Length > 0 ? power : fallback;
            if (threatSource.Length == 0) throw new ArgumentException("At least one qualified or fallback candidate is required.", nameof(request));
            var total = threatSource.Sum(c => Math.Max(0.0001f, c.ExpectedRallyValue - threatSource.Min(x => x.ExpectedRallyValue) + 0.0001f));
            var entries = threatSource.Select(c => new PublicAttackThreatEntryV3(c.ActionClass, Zone(c.Target), Math.Max(0.0001f, c.ExpectedRallyValue - threatSource.Min(x => x.ExpectedRallyValue) + 0.0001f) / total, 0f)).ToArray();
            return new AttackPlanningResultV3(ordered, power, fallback, new PublicAttackThreatV3("gate-i-threat-" + request.Revision, entries));
        }

        public FinalAttackChoiceV3 ChooseFinal(AttackPlanningResultV3 result, JointDefensePlanV3 committedDefense)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (committedDefense == null) throw new ArgumentNullException(nameof(committedDefense));
            var pool = result.QualifiedPowerRoutes.Count > 0 ? result.QualifiedPowerRoutes : result.FallbackCandidates;
            if (pool.Count == 0) throw new InvalidOperationException("No attack candidate is available.");
            var chosen = pool.OrderByDescending(c => c.ExpectedRallyValue).ThenBy(c => c.Actor.ToString(), StringComparer.Ordinal)
                .ThenBy(c => (int)c.ActionClass).ThenBy(c => c.CandidateIdentity, StringComparer.Ordinal).First();
            return new FinalAttackChoiceV3(chosen, result.FallbackCandidates);
        }

        // Tool recovery is comparable to the existing soft-action fallbacks only
        // after its five-link continuation has been qualified by the pure planner.
        public static IReadOnlyList<AttackCandidateV3> AddQualifiedToolRecoveryFallback(
            IReadOnlyList<AttackCandidateV3> fallbackCandidates, BlockToolRecoveryResultV3 recovery,
            AttackCandidateV3 toolRecoveryCandidate)
        {
            if (fallbackCandidates == null) throw new ArgumentNullException(nameof(fallbackCandidates));
            if (recovery == null) throw new ArgumentNullException(nameof(recovery));
            if (toolRecoveryCandidate == null) throw new ArgumentNullException(nameof(toolRecoveryCandidate));
            var values = fallbackCandidates.Select(value => value ?? throw new ArgumentException("Fallback candidates cannot contain null.", nameof(fallbackCandidates))).ToList();
            if (recovery.IsQualified)
            {
                if (toolRecoveryCandidate.ActionClass != AttackActionClassV3.BlockToolRecovery || !toolRecoveryCandidate.Actor.Equals(recovery.Attacker))
                    throw new ArgumentException("Tool recovery candidate must match the qualified attacker.", nameof(toolRecoveryCandidate));
                values.Add(toolRecoveryCandidate);
            }
            return new ReadOnlyCollection<AttackCandidateV3>(values);
        }

        private static bool IsPower(AttackActionClassV3 value) => value == AttackActionClassV3.PowerLine || value == AttackActionClassV3.PowerCross || value == AttackActionClassV3.PowerEdge || value == AttackActionClassV3.PowerOverHand;
        private static string Zone(SimVector3 target) => target.X < -1f ? "Line" : target.X > 1f ? "Cross" : "Middle";
    }
}

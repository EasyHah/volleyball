using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.AI
{
    // Tactical inputs are facts captured at the Gate H handoff.  They deliberately
    // contain no selected target, envelope, trajectory identity, or candidate.
    public sealed class GateITacticalPlayerV3
    {
        public GateITacticalPlayerV3(PlayerId player, TeamSide side, SimVector3 worldPosition,
            bool canAttack, DerivedMatchAttributesV4 attributes)
        {
            Player = player; Side = side; WorldPosition = worldPosition;
            CanAttack = canAttack; Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            if (!worldPosition.IsFinite || !Enum.IsDefined(typeof(TeamSide), side)) throw new ArgumentOutOfRangeException(!worldPosition.IsFinite ? nameof(worldPosition) : nameof(side));
        }
        public PlayerId Player { get; } public TeamSide Side { get; } public SimVector3 WorldPosition { get; }
        public bool CanAttack { get; } public DerivedMatchAttributesV4 Attributes { get; }
    }

    public sealed class SetIntentPlanningRequestV3
    {
        public SetIntentPlanningRequestV3(long revision, long sourceSequence, TeamSide attackingSide, PlayerId organizer,
            float expectedSetContactTime, BallState acceptedPass, IReadOnlyList<GateITacticalPlayerV3> players,
            DerivedMatchAttributesV4 organizerAttributes, BallTrajectoryPredictionArtifactV4 passPrediction)
        {
            if (revision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(revision < 0 ? nameof(revision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(TeamSide), attackingSide) || !acceptedPass.Position.IsFinite || !acceptedPass.Velocity.IsFinite || float.IsNaN(expectedSetContactTime) || float.IsInfinity(expectedSetContactTime)) throw new ArgumentOutOfRangeException(nameof(attackingSide));
            Revision = revision; SourceSequence = sourceSequence; AttackingSide = attackingSide; Organizer = organizer;
            ExpectedSetContactTime = expectedSetContactTime; AcceptedPass = acceptedPass;
            Players = Copy(players, nameof(players)); OrganizerAttributes = organizerAttributes ?? throw new ArgumentNullException(nameof(organizerAttributes));
            PassPrediction = passPrediction ?? throw new ArgumentNullException(nameof(passPrediction));
        }
        public long Revision { get; } public long SourceSequence { get; } public TeamSide AttackingSide { get; } public PlayerId Organizer { get; }
        public float ExpectedSetContactTime { get; } public BallState AcceptedPass { get; } public IReadOnlyList<GateITacticalPlayerV3> Players { get; }
        public DerivedMatchAttributesV4 OrganizerAttributes { get; } public BallTrajectoryPredictionArtifactV4 PassPrediction { get; }
        internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, string name) where T : class =>
            new ReadOnlyCollection<T>((values ?? throw new ArgumentNullException(name)).Select(x => x ?? throw new ArgumentException("Values cannot contain null.", name)).ToArray());
    }

    public sealed class AcceptedSetEvidenceV3
    {
        public AcceptedSetEvidenceV3(PlayerId actor, string envelopeIdentity, string trajectoryArtifactIdentity)
        { Actor = actor; EnvelopeIdentity = Require(envelopeIdentity, nameof(envelopeIdentity)); TrajectoryArtifactIdentity = Require(trajectoryArtifactIdentity, nameof(trajectoryArtifactIdentity)); }
        public PlayerId Actor { get; } public string EnvelopeIdentity { get; } public string TrajectoryArtifactIdentity { get; }
        private static string Require(string value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Value is required.", name);
    }

    public sealed class AttackPlanningRequestV3
    {
        public AttackPlanningRequestV3(long revision, GateISetIntentV3 setIntent, AcceptedSetEvidenceV3 actualSet,
            IReadOnlyList<GateITacticalPlayerV3> players)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision)); Revision = revision;
            SetIntent = setIntent ?? throw new ArgumentNullException(nameof(setIntent)); ActualSet = actualSet ?? throw new ArgumentNullException(nameof(actualSet));
            Players = SetIntentPlanningRequestV3.Copy(players, nameof(players));
        }
        public long Revision { get; } public GateISetIntentV3 SetIntent { get; } public AcceptedSetEvidenceV3 ActualSet { get; }
        public IReadOnlyList<GateITacticalPlayerV3> Players { get; }
    }

    public sealed class AttackPlanningResultV3
    {
        public AttackPlanningResultV3(IReadOnlyList<AttackCandidateV3> candidates, IReadOnlyList<AttackCandidateV3> qualifiedPowerRoutes, IReadOnlyList<AttackCandidateV3> fallbackCandidates, IReadOnlyList<GateIAttackExecutionEvidenceV3> executionEvidence, PublicAttackThreatV3 publicThreat)
        { Candidates = Copy(candidates, nameof(candidates)); QualifiedPowerRoutes = Copy(qualifiedPowerRoutes, nameof(qualifiedPowerRoutes)); FallbackCandidates = Copy(fallbackCandidates, nameof(fallbackCandidates)); ExecutionEvidence = CopyEvidence(executionEvidence); PublicThreat = publicThreat ?? throw new ArgumentNullException(nameof(publicThreat)); }
        public IReadOnlyList<AttackCandidateV3> Candidates { get; } public IReadOnlyList<AttackCandidateV3> QualifiedPowerRoutes { get; } public IReadOnlyList<AttackCandidateV3> FallbackCandidates { get; } public PublicAttackThreatV3 PublicThreat { get; }
        // Pure domain lookup for the coordinator; no Presentation command types leak here.
        public IReadOnlyList<GateIAttackExecutionEvidenceV3> ExecutionEvidence { get; }
        public GateIAttackExecutionEvidenceV3 EvidenceFor(AttackCandidateV3 candidate) => ExecutionEvidence.Single(x => x.CandidateIdentity == (candidate ?? throw new ArgumentNullException(nameof(candidate))).CandidateIdentity);
        private static IReadOnlyList<AttackCandidateV3> Copy(IReadOnlyList<AttackCandidateV3> values, string name) => new ReadOnlyCollection<AttackCandidateV3>((values ?? throw new ArgumentNullException(name)).ToArray());
        private static IReadOnlyList<GateIAttackExecutionEvidenceV3> CopyEvidence(IReadOnlyList<GateIAttackExecutionEvidenceV3> values) => new ReadOnlyCollection<GateIAttackExecutionEvidenceV3>((values ?? throw new ArgumentNullException(nameof(values))).ToArray());
    }

    public sealed class GateIAttackExecutionEvidenceV3
    {
        public GateIAttackExecutionEvidenceV3(AttackCandidateV3 candidate, ExecutionSampleClassificationV4 executionClassification, BallTrajectoryPredictionArtifactV4 trajectoryArtifact)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate)); ExecutionClassification = executionClassification ?? throw new ArgumentNullException(nameof(executionClassification));
            TrajectoryArtifact = trajectoryArtifact ?? throw new ArgumentNullException(nameof(trajectoryArtifact));
            if (ExecutionClassification.ExecutableEnvelope == null || Candidate.EnvelopeIdentity != ExecutionClassification.ExecutableEnvelope.Identity || Candidate.TrajectoryArtifactIdentity != TrajectoryArtifact.ArtifactIdentity) throw new ArgumentException("Candidate identity must match its exact execution evidence.");
        }
        public AttackCandidateV3 Candidate { get; } public string CandidateIdentity => Candidate.CandidateIdentity;
        public ExecutionSampleClassificationV4 ExecutionClassification { get; } public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
    }

    public sealed class FinalAttackChoiceV3
    {
        public FinalAttackChoiceV3(AttackCandidateV3 candidate, IReadOnlyList<AttackCandidateV3> comparableFallbacks) { Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate)); ComparableFallbacks = new ReadOnlyCollection<AttackCandidateV3>((comparableFallbacks ?? throw new ArgumentNullException(nameof(comparableFallbacks))).ToArray()); }
        public AttackCandidateV3 Candidate { get; } public IReadOnlyList<AttackCandidateV3> ComparableFallbacks { get; }
    }

    public sealed class AttackDefensePlanner
    {
        public GateISetIntentV3 PlanSetIntent(SetIntentPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var attacker = Eligible(request).OrderByDescending(AttackScore).ThenBy(x => x.Player.ToString(), StringComparer.Ordinal).FirstOrDefault();
            if (attacker == null) throw new ArgumentException("Gate I requires an eligible attacking player.", nameof(request));
            var target = new SimVector3(attacker.WorldPosition.X, Math.Max(2.35f, attacker.Attributes.Attributes.Attack.ContactHeightMeters - .35f), attacker.WorldPosition.Z);
            var velocity = new SimVector3(0f, 2.5f, attacker.WorldPosition.Z >= request.AcceptedPass.Position.Z ? 2f : -2f);
            var envelope = ExecutionEnvelopeFactoryV4.Create(request.OrganizerAttributes,
                new ExecutionIntentV4("gate-i-set-" + request.Revision + "-" + request.SourceSequence, ExecutionCandidateCategoryV4.Set, target, velocity, .5f),
                "gate-i-set-" + request.Revision + "-" + request.SourceSequence, ExecutionEnvelopePolicyV4.GateI);
            var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey, ExecutionCandidateCategoryV4.Set, target, velocity, .5f);
            return new GateISetIntentV3(request.Revision, request.SourceSequence, request.Organizer, attacker.Player, target, request.ExpectedSetContactTime, envelope.Classify(sample), request.PassPrediction);
        }

        public AttackPlanningResultV3 PlanAttack(AttackPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.SetIntent.PlanRevision != request.Revision || !request.SetIntent.Organizer.Equals(request.ActualSet.Actor) || request.SetIntent.ExecutionClassification.ExecutableEnvelope.Identity != request.ActualSet.EnvelopeIdentity || request.SetIntent.TrajectoryArtifact.ArtifactIdentity != request.ActualSet.TrajectoryArtifactIdentity) throw new ArgumentException("Accepted Set evidence does not match the immutable SetIntent.", nameof(request));
            var actor = request.Players.FirstOrDefault(x => x.Player.Equals(request.SetIntent.PreparedAttacker) && x.CanAttack);
            if (actor == null) throw new ArgumentException("Accepted Set attacker is not eligible.", nameof(request));
            var generatedEvidence = Generate(actor, request.SetIntent).OrderByDescending(c => c.Candidate.IsQualifiedPowerRoute).ThenByDescending(c => c.Candidate.ExpectedRallyValue).ThenBy(c => c.Candidate.Actor.ToString(), StringComparer.Ordinal).ThenBy(c => (int)c.Candidate.ActionClass).ThenBy(c => c.Candidate.CandidateIdentity, StringComparer.Ordinal).ToArray();
            var generated = generatedEvidence.Select(x => x.Candidate).ToArray();
            var power = generated.Where(c => IsPower(c.ActionClass) && c.IsQualifiedPowerRoute && c.LegalSampleRatio >= .6f).ToArray();
            // Tool recovery stays visible as an eliminated tactical branch, but does not
            // become a comparable fallback until AddQualifiedToolRecoveryFallback.
            var fallback = generated.Where(c => !IsPower(c.ActionClass) && c.ActionClass != AttackActionClassV3.BlockToolRecovery).ToArray();
            var threatSource = power.Length > 0 ? power : fallback;
            var min = threatSource.Min(c => c.ExpectedRallyValue); var total = threatSource.Sum(c => Math.Max(.0001f, c.ExpectedRallyValue - min + .0001f));
            var entries = threatSource.Select(c => new PublicAttackThreatEntryV3(c.ActionClass, Zone(c.Target), Math.Max(.0001f, c.ExpectedRallyValue - min + .0001f) / total, ArrivalTime(c, request.SetIntent))).ToArray();
            return new AttackPlanningResultV3(generated, power, fallback, generatedEvidence, new PublicAttackThreatV3("gate-i-threat-" + request.Revision, entries));
        }

        public FinalAttackChoiceV3 ChooseFinal(AttackPlanningResultV3 result, JointDefensePlanV3 committedDefense) { if (result == null) throw new ArgumentNullException(nameof(result)); if (committedDefense == null) throw new ArgumentNullException(nameof(committedDefense)); var pool = result.QualifiedPowerRoutes.Count > 0 ? result.QualifiedPowerRoutes : result.FallbackCandidates; if (pool.Count == 0) throw new InvalidOperationException("No attack candidate is available."); return new FinalAttackChoiceV3(pool.OrderByDescending(c => c.ExpectedRallyValue).ThenBy(c => c.Actor.ToString(), StringComparer.Ordinal).ThenBy(c => (int)c.ActionClass).ThenBy(c => c.CandidateIdentity, StringComparer.Ordinal).First(), result.FallbackCandidates); }

        public static IReadOnlyList<AttackCandidateV3> AddQualifiedToolRecoveryFallback(IReadOnlyList<AttackCandidateV3> fallbackCandidates, BlockToolRecoveryResultV3 recovery, AttackCandidateV3 toolRecoveryCandidate)
        { if (fallbackCandidates == null) throw new ArgumentNullException(nameof(fallbackCandidates)); if (recovery == null) throw new ArgumentNullException(nameof(recovery)); if (toolRecoveryCandidate == null) throw new ArgumentNullException(nameof(toolRecoveryCandidate)); var values = fallbackCandidates.Select(x => x ?? throw new ArgumentException("Fallback candidates cannot contain null.", nameof(fallbackCandidates))).ToList(); if (recovery.IsQualified) { if (toolRecoveryCandidate.ActionClass != AttackActionClassV3.BlockToolRecovery || !toolRecoveryCandidate.Actor.Equals(recovery.Attacker)) throw new ArgumentException("Tool recovery candidate must match the qualified attacker.", nameof(toolRecoveryCandidate)); values.Add(toolRecoveryCandidate); } return new ReadOnlyCollection<AttackCandidateV3>(values); }

        private static IEnumerable<GateITacticalPlayerV3> Eligible(SetIntentPlanningRequestV3 request) => request.Players.Where(x => x.Side == request.AttackingSide && x.CanAttack);
        private static float AttackScore(GateITacticalPlayerV3 x) { var a = x.Attributes.Attributes.Attack; return a.PowerCapacity + a.DirectionControl + a.SpeedControl + a.ApproachMobility; }
        private static IEnumerable<GateIAttackExecutionEvidenceV3> Generate(GateITacticalPlayerV3 actor, GateISetIntentV3 set)
        {
            var a = actor.Attributes.Attributes.Attack; var distance = (actor.WorldPosition - set.Target).Magnitude; var ratio = Math.Max(0f, Math.Min(1f, 1f - (distance / 8f))); var powerQualified = ratio >= .6f && a.PowerCapacity >= .45f;
            foreach (var action in new[] { AttackActionClassV3.PowerLine, AttackActionClassV3.PowerCross, AttackActionClassV3.PowerEdge, AttackActionClassV3.PowerOverHand, AttackActionClassV3.Tip, AttackActionClassV3.Roll, AttackActionClassV3.Push, AttackActionClassV3.HighSurvival, AttackActionClassV3.BlockOut, AttackActionClassV3.BlockToolRecovery })
            {
                var power = IsPower(action); var tool = action == AttackActionClassV3.BlockToolRecovery; var target = Target(set.Target, action); var qualified = power && powerQualified;
                var value = (power ? .65f + a.PowerCapacity : .25f + a.DirectionControl) + (ratio * .2f) - ((int)action * .001f);
                var identity = "gate-i-" + set.PlanRevision + "-" + action;
                var category = power ? ExecutionCandidateCategoryV4.Attack : ExecutionCandidateCategoryV4.SoftAction;
                var velocity = new SimVector3(0f, 1.5f, target.Z >= set.Target.Z ? 2f : -2f);
                var envelope = ExecutionEnvelopeFactoryV4.Create(actor.Attributes, new ExecutionIntentV4(identity, category, target, velocity, .5f), identity, ExecutionEnvelopePolicyV4.GateI);
                var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey, category, target, velocity, .5f);
                var classification = envelope.Classify(sample);
                var trajectory = set.TrajectoryArtifact.ForCandidate(identity, classification.ExecutableEnvelope.Identity);
                var candidate = new AttackCandidateV3(identity, actor.Player, action, set.Target, target, value, power ? ratio : 1f, qualified, qualified || !power ? (tool ? "Tool recovery requires qualification." : string.Empty) : "Set geometry does not qualify power.", classification.ExecutableEnvelope.Identity, trajectory.ArtifactIdentity);
                yield return new GateIAttackExecutionEvidenceV3(candidate, classification, trajectory);
            }
        }
        private static SimVector3 Target(SimVector3 center, AttackActionClassV3 action) { var offset = action == AttackActionClassV3.PowerLine ? -3f : action == AttackActionClassV3.PowerCross ? 3f : 0f; return new SimVector3(center.X + offset, 1f, center.Z + 7f); }
        // Public timing is a shared, evidence-derived prediction: the accepted Set
        // contact time plus the distance to the generated landing target.  It is
        // intentionally never a presentation or legacy-decision input.
        private static float ArrivalTime(AttackCandidateV3 candidate, GateISetIntentV3 set) =>
            set.GateHExpectedContactTime + Math.Max(.01f, (candidate.Target - candidate.ContactCenter).Magnitude / 18f);
        private static bool IsPower(AttackActionClassV3 value) => value == AttackActionClassV3.PowerLine || value == AttackActionClassV3.PowerCross || value == AttackActionClassV3.PowerEdge || value == AttackActionClassV3.PowerOverHand;
        private static string Zone(SimVector3 target) => target.X < -1f ? "Line" : target.X > 1f ? "Cross" : "Middle";
    }
}

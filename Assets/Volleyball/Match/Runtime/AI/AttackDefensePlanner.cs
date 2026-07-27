using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using ContactCapsuleFrame = Volleyball.Domain.Simulation.ContactCapsuleFrame;
using TeamCourtFrame = Volleyball.Domain.Prototype.TeamCourtFrame;
using TeamId = Volleyball.Domain.Prototype.TeamId;

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
        // Compatibility overload for existing domain fixtures.  It still creates a
        // fresh provider request; production formal handoff supplies its exact
        // runtime provider and physics facts through the full constructor below.
        public SetIntentPlanningRequestV3(long revision, long sourceSequence, TeamSide attackingSide, PlayerId organizer,
            float expectedSetContactTime, BallState acceptedPass, IReadOnlyList<GateITacticalPlayerV3> players,
            DerivedMatchAttributesV4 organizerAttributes, BallTrajectoryPredictionArtifactV4 passPrediction)
            : this(revision, sourceSequence, attackingSide, organizer, expectedSetContactTime,
                acceptedPass, players, organizerAttributes, passPrediction,
                new BallTrajectoryPredictionProviderV4(new TrajectoryPredictionProviderConfigurationV4(
                    32, TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    passPrediction?.PredictorVersion ?? BallTrajectoryPredictionProviderV4.CurrentPredictorVersion,
                    passPrediction?.PredictorConfigurationHash ?? BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash)),
                new BallSimulationParameters(-9.8f, .9995f),
                BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(new BallSimulationParameters(-9.8f, .9995f)),
                sourceSequence)
        {
        }

        public SetIntentPlanningRequestV3(long revision, long sourceSequence, TeamSide attackingSide, PlayerId organizer,
            float expectedSetContactTime, BallState acceptedPass, IReadOnlyList<GateITacticalPlayerV3> players,
            DerivedMatchAttributesV4 organizerAttributes, BallTrajectoryPredictionArtifactV4 passPrediction,
            BallTrajectoryPredictionProviderV4 trajectoryProvider,
            BallSimulationParameters simulationParameters, string physicsConfigurationHash,
            long acceptedPassStateVersion)
        {
            if (revision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(revision < 0 ? nameof(revision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(TeamSide), attackingSide) || !acceptedPass.Position.IsFinite || !acceptedPass.Velocity.IsFinite || float.IsNaN(expectedSetContactTime) || float.IsInfinity(expectedSetContactTime)) throw new ArgumentOutOfRangeException(nameof(attackingSide));
            Revision = revision; SourceSequence = sourceSequence; AttackingSide = attackingSide; Organizer = organizer;
            ExpectedSetContactTime = expectedSetContactTime; AcceptedPass = acceptedPass;
            Players = Copy(players, nameof(players)); OrganizerAttributes = organizerAttributes ?? throw new ArgumentNullException(nameof(organizerAttributes));
            PassPrediction = passPrediction ?? throw new ArgumentNullException(nameof(passPrediction));
            TrajectoryProvider = trajectoryProvider ?? throw new ArgumentNullException(nameof(trajectoryProvider));
            SimulationParameters = simulationParameters;
            PhysicsConfigurationHash = string.IsNullOrWhiteSpace(physicsConfigurationHash) ? throw new ArgumentException("Value is required.", nameof(physicsConfigurationHash)) : physicsConfigurationHash;
            if (acceptedPassStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(acceptedPassStateVersion));
            AcceptedPassStateVersion = acceptedPassStateVersion;
        }
        public long Revision { get; } public long SourceSequence { get; } public TeamSide AttackingSide { get; } public PlayerId Organizer { get; }
        public float ExpectedSetContactTime { get; } public BallState AcceptedPass { get; } public IReadOnlyList<GateITacticalPlayerV3> Players { get; }
        public DerivedMatchAttributesV4 OrganizerAttributes { get; } public BallTrajectoryPredictionArtifactV4 PassPrediction { get; }
        // The planner predicts a fresh Set artifact from the accepted pass state.
        // The pass artifact remains evidence of the preceding contact only.
        public BallTrajectoryPredictionProviderV4 TrajectoryProvider { get; }
        public BallSimulationParameters SimulationParameters { get; }
        public string PhysicsConfigurationHash { get; }
        public long AcceptedPassStateVersion { get; }
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
            : this(revision, setIntent, actualSet, players,
                new BallTrajectoryPredictionProviderV4(new TrajectoryPredictionProviderConfigurationV4(
                    32, TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    setIntent?.TrajectoryArtifact.PredictorVersion ?? BallTrajectoryPredictionProviderV4.CurrentPredictorVersion,
                    setIntent?.TrajectoryArtifact.PredictorConfigurationHash ?? BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash)),
                new BallSimulationParameters(-9.8f, .9995f),
                BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(new BallSimulationParameters(-9.8f, .9995f)),
                setIntent?.TrajectoryArtifact.Key.BallStateVersion + 1 ?? 0)
        {
        }

        public AttackPlanningRequestV3(long revision, GateISetIntentV3 setIntent, AcceptedSetEvidenceV3 actualSet,
            IReadOnlyList<GateITacticalPlayerV3> players,
            BallTrajectoryPredictionProviderV4 trajectoryProvider,
            BallSimulationParameters simulationParameters, string physicsConfigurationHash,
            long attackBallStateVersion)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision)); Revision = revision;
            SetIntent = setIntent ?? throw new ArgumentNullException(nameof(setIntent)); ActualSet = actualSet ?? throw new ArgumentNullException(nameof(actualSet));
            Players = SetIntentPlanningRequestV3.Copy(players, nameof(players));
            TrajectoryProvider = trajectoryProvider ?? throw new ArgumentNullException(nameof(trajectoryProvider));
            SimulationParameters = simulationParameters;
            PhysicsConfigurationHash = string.IsNullOrWhiteSpace(physicsConfigurationHash) ? throw new ArgumentException("Value is required.", nameof(physicsConfigurationHash)) : physicsConfigurationHash;
            if (attackBallStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(attackBallStateVersion));
            AttackBallStateVersion = attackBallStateVersion;
        }
        public long Revision { get; } public GateISetIntentV3 SetIntent { get; } public AcceptedSetEvidenceV3 ActualSet { get; }
        public IReadOnlyList<GateITacticalPlayerV3> Players { get; }
        // Post-set trajectories are fresh, candidate-owned physical predictions.
        // This preserves the accepted Set artifact as Set evidence rather than
        // relabeling it as an attack trajectory.
        public BallTrajectoryPredictionProviderV4 TrajectoryProvider { get; }
        public BallSimulationParameters SimulationParameters { get; }
        public string PhysicsConfigurationHash { get; }
        public long AttackBallStateVersion { get; }
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
        private const float FixedSimulationStepSeconds = 1f / 120f;
        // A Gate-I set arrives at the player's executable contact center rather
        // than a presentation-only apex; this deterministic flight gives the
        // route solver enough net clearance at that real contact height.
        private const float GateIAttackFlightSeconds = .85f;
        public GateISetIntentV3 PlanSetIntent(SetIntentPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var attacker = Eligible(request).OrderByDescending(AttackScore).ThenBy(x => x.Player.ToString(), StringComparer.Ordinal).FirstOrDefault();
            if (attacker == null) throw new ArgumentException("Gate I requires an eligible attacking player.", nameof(request));
            var target = new SimVector3(attacker.WorldPosition.X, Math.Max(2.35f, attacker.Attributes.Attributes.Attack.ContactHeightMeters - .35f), attacker.WorldPosition.Z);
            // The handoff state is the predicted Gate H Set contact, never the
            // earlier receive state.  Solve the actual discrete flight before
            // minting its envelope and immutable trajectory evidence.
            var flight = SolveSetFlight(request, target);
            var velocity = flight.InitialVelocity;
            var envelope = ExecutionEnvelopeFactoryV4.Create(request.OrganizerAttributes,
                new ExecutionIntentV4("gate-i-set-" + request.Revision + "-" + request.SourceSequence, ExecutionCandidateCategoryV4.Set, target, velocity, .5f),
                "gate-i-set-" + request.Revision + "-" + request.SourceSequence, ExecutionEnvelopePolicyV4.GateI);
            var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey, ExecutionCandidateCategoryV4.Set, target, velocity, .5f);
            var classification = envelope.Classify(sample);
            var setBall = new BallState(request.AcceptedPass.Position, velocity, request.AcceptedPass.Radius);
            var trajectory = request.TrajectoryProvider.Predict(new BallTrajectoryPredictionRequestV4(
                request.AttackingSide, request.AcceptedPassStateVersion, setBall,
                request.SimulationParameters, request.PhysicsConfigurationHash,
                "gate-i-set-trajectory-" + request.Revision + "-" + request.SourceSequence,
                request.TrajectoryProvider.PredictorVersion,
                request.TrajectoryProvider.PredictorConfigurationHash,
                classification.ExecutableEnvelope.Identity,
                ExecutionDegradationStepV4.FullSampling));
            return new GateISetIntentV3(request.Revision, request.SourceSequence, request.Organizer, attacker.Player, target, request.ExpectedSetContactTime, classification, trajectory, flight.FlightSeconds);
        }

        public AttackPlanningResultV3 PlanAttack(AttackPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.SetIntent.PlanRevision != request.Revision || !request.SetIntent.Organizer.Equals(request.ActualSet.Actor) || request.SetIntent.ExecutionClassification.ExecutableEnvelope.Identity != request.ActualSet.EnvelopeIdentity || request.SetIntent.TrajectoryArtifact.ArtifactIdentity != request.ActualSet.TrajectoryArtifactIdentity) throw new ArgumentException("Accepted Set evidence does not match the immutable SetIntent.", nameof(request));
            var actor = request.Players.FirstOrDefault(x => x.Player.Equals(request.SetIntent.PreparedAttacker) && x.CanAttack);
            if (actor == null) throw new ArgumentException("Accepted Set attacker is not eligible.", nameof(request));
            var generatedEvidence = Generate(actor, request).OrderByDescending(c => c.Candidate.IsQualifiedPowerRoute).ThenByDescending(c => c.Candidate.ExpectedRallyValue).ThenBy(c => c.Candidate.Actor.ToString(), StringComparer.Ordinal).ThenBy(c => (int)c.Candidate.ActionClass).ThenBy(c => c.Candidate.CandidateIdentity, StringComparer.Ordinal).ToArray();
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
        {
            if (fallbackCandidates == null) throw new ArgumentNullException(nameof(fallbackCandidates));
            if (recovery == null) throw new ArgumentNullException(nameof(recovery));
            if (toolRecoveryCandidate == null) throw new ArgumentNullException(nameof(toolRecoveryCandidate));
            var values = fallbackCandidates.Select(x => x ?? throw new ArgumentException("Fallback candidates cannot contain null.", nameof(fallbackCandidates))).ToList();
            if (recovery.IsQualified)
            {
                var qualified = recovery.ToolRecoveryCandidate;
                if (qualified == null || recovery.ReboundEvidence == null || recovery.ReorganizationExit == null ||
                    toolRecoveryCandidate.CandidateIdentity != qualified.CandidateIdentity ||
                    toolRecoveryCandidate.ActionClass != AttackActionClassV3.BlockToolRecovery ||
                    !toolRecoveryCandidate.Actor.Equals(recovery.Attacker) ||
                    toolRecoveryCandidate.EnvelopeIdentity != recovery.PlanEnvelopeIdentity ||
                    toolRecoveryCandidate.TrajectoryArtifactIdentity != recovery.ReboundEvidence.TrajectoryArtifactIdentity ||
                    toolRecoveryCandidate.ReorganizationExitIdentity != recovery.ReorganizationExit.Identity ||
                    toolRecoveryCandidate.ExpectedRallyValue != recovery.Value)
                    throw new ArgumentException("Tool recovery candidate must exactly match qualified rebound evidence, exit, and value.", nameof(toolRecoveryCandidate));
                if (!recovery.RecoveryActor.HasValue || recovery.RemainingTouches <= 0 ||
                    recovery.HomeReboundProbability <= 0f ||
                    recovery.BlockContactProbability <= 0f ||
                    recovery.RecoveryActor.Value.Equals(recovery.Attacker))
                    throw new ArgumentException("Qualified recovery must retain exact legal rebound and non-attacker continuation evidence.", nameof(recovery));
                values.Add(new AttackCandidateV3(
                    qualified.CandidateIdentity, qualified.Actor, qualified.ActionClass,
                    qualified.ContactCenter, qualified.Target, qualified.ExpectedRallyValue,
                    qualified.LegalSampleRatio, qualified.IsQualifiedPowerRoute,
                    qualified.EliminationReason, qualified.EnvelopeIdentity,
                    qualified.TrajectoryArtifactIdentity,
                    qualified.ReorganizationExitIdentity,
                    new ToolRecoveryEvidenceV3(
                        qualified.CandidateIdentity,
                        recovery.ReboundEvidence.Blocker,
                        recovery.ReboundSide,
                        recovery.RecoveryActor.Value,
                        recovery.RemainingTouches,
                        recovery.ReorganizationExit.Identity,
                        recovery.PlanEnvelopeIdentity,
                        recovery.ReboundEvidence.TrajectoryArtifactIdentity,
                        recovery.ReboundEvidence.SampleIdentity,
                        recovery.ReboundEvidence.BlockContactIdentity)));
            }
            return new ReadOnlyCollection<AttackCandidateV3>(values);
        }

        private static IEnumerable<GateITacticalPlayerV3> Eligible(SetIntentPlanningRequestV3 request) => request.Players.Where(x => x.Side == request.AttackingSide && x.CanAttack);
        private static float AttackScore(GateITacticalPlayerV3 x) { var a = x.Attributes.Attributes.Attack; return a.PowerCapacity + a.DirectionControl + a.SpeedControl + a.ApproachMobility; }
        private static IEnumerable<GateIAttackExecutionEvidenceV3> Generate(GateITacticalPlayerV3 actor, AttackPlanningRequestV3 request)
        {
            var set = request.SetIntent;
            var a = actor.Attributes.Attributes.Attack;
            foreach (var action in new[] { AttackActionClassV3.PowerLine, AttackActionClassV3.PowerCross, AttackActionClassV3.PowerEdge, AttackActionClassV3.PowerOverHand, AttackActionClassV3.Tip, AttackActionClassV3.Roll, AttackActionClassV3.Push, AttackActionClassV3.HighSurvival, AttackActionClassV3.BlockOut, AttackActionClassV3.BlockToolRecovery })
            {
                var power = IsPower(action); var tool = action == AttackActionClassV3.BlockToolRecovery;
                var identity = "gate-i-" + set.PlanRevision + "-" + action;
                var category = power ? ExecutionCandidateCategoryV4.Attack : ExecutionCandidateCategoryV4.SoftAction;
                var route = RouteFor(action, set.Target);
                var selection = TrySelectRoute(actor, set.Target, route, request.SimulationParameters);
                var target = selection.HasValue ? selection.Value.Target : Target(set.Target, action, actor.Side);
                var velocity = selection.HasValue
                    ? selection.Value.InitialVelocity
                    : SafeRejectedVelocity(set.Target, request.SimulationParameters);
                var envelope = ExecutionEnvelopeFactoryV4.Create(actor.Attributes, new ExecutionIntentV4(identity, category, target, velocity, .5f), identity, ExecutionEnvelopePolicyV4.GateI);
                var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey, category, target, velocity, .5f);
                var classification = envelope.Classify(sample);
                // The baseline artifact is the only artifact execution may use.
                // Reliability is separately derived from the Gate-I policy's seven
                // deterministic envelope samples, each predicted by the shared
                // provider with the same physics configuration.
                var trajectory = PredictTrajectory(request, actor.Side, set.Target,
                    classification.ExecutableSample.Velocity, identity + ":trajectory:0",
                    classification.ExecutableEnvelope.Identity);
                var ratio = LegalSampleRatio(request, actor.Side, set.Target,
                    classification.ExecutableEnvelope, classification.ExecutableSample,
                    identity);
                var arrivalFeasible = (actor.WorldPosition - set.Target).Magnitude <= 8f;
                var contactGeometryFeasible = set.Target.Y >= 2.60f;
                var qualified = power && arrivalFeasible && contactGeometryFeasible && a.PowerCapacity >= .45f && ratio >= .6f;
                var value = (power ? .65f + a.PowerCapacity : .25f + a.DirectionControl) + (ratio * .2f) - ((int)action * .001f);
                var elimination = power && !qualified
                    ? !arrivalFeasible ? "ArrivalInfeasible" : !contactGeometryFeasible
                        ? "ContactGeometryInfeasible" : "InsufficientLegalCrossRatio"
                    : (tool ? "Tool recovery requires qualification." : string.Empty);
                var candidate = new AttackCandidateV3(identity, actor.Player, action, set.Target, target, value, ratio, qualified, elimination, classification.ExecutableEnvelope.Identity, trajectory.ArtifactIdentity);
                yield return new GateIAttackExecutionEvidenceV3(candidate, classification, trajectory);
            }
        }
        private static AttackRouteSelection? TrySelectRoute(GateITacticalPlayerV3 actor, SimVector3 contactCenter,
            GeometricAttackRoute route, BallSimulationParameters parameters)
        {
            var input = new AttackRouteSelectionInput(actor.Side == TeamSide.Home ? TeamId.Blue : TeamId.Orange,
                contactCenter, GateIAttackFlightSeconds, Array.Empty<ContactCapsuleFrame>(), parameters, FixedSimulationStepSeconds);
            try
            {
                return AttackRouteSelector.EvaluateAll(input,
                    ExecutionEnvelopeFactoryV4.Create(actor.Attributes,
                        new ExecutionIntentV4("gate-i-route-evaluation", ExecutionCandidateCategoryV4.Attack,
                            contactCenter, new SimVector3(0f, 1f, 1f), .5f),
                        "gate-i-route-evaluation", ExecutionEnvelopePolicyV4.GateI),
                    Array.Empty<BallTrajectoryPredictionArtifactV4>())
                    .Single(value => value.Route == route).Selection;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        private static GeometricAttackRoute RouteFor(AttackActionClassV3 action, SimVector3 center) => action switch
        {
            AttackActionClassV3.PowerLine => GeometricAttackRoute.Line,
            AttackActionClassV3.PowerCross => GeometricAttackRoute.CrossCourt,
            AttackActionClassV3.PowerEdge => center.X <= 0f ? GeometricAttackRoute.EdgeRight : GeometricAttackRoute.EdgeLeft,
            AttackActionClassV3.PowerOverHand => GeometricAttackRoute.OverHand,
            _ => GeometricAttackRoute.Line
        };
        private static SimVector3 Target(SimVector3 center, AttackActionClassV3 action, TeamSide side)
        {
            var frame = new TeamCourtFrame(side == TeamSide.Home ? TeamId.Blue : TeamId.Orange);
            var offset = action == AttackActionClassV3.PowerLine ? -3f : action == AttackActionClassV3.PowerCross ? 3f : 0f;
            return frame.ToWorld(new SimVector3(center.X + offset, .12f, 5.25f));
        }
        private static SimVector3 SafeRejectedVelocity(SimVector3 center, BallSimulationParameters parameters)
        {
            return ReturnVelocitySolver.Solve(center, new SimVector3(center.X, .12f, center.Z),
                .55f, FixedSimulationStepSeconds, parameters).InitialVelocity;
        }
        private static float LegalSampleRatio(AttackPlanningRequestV3 request, TeamSide side,
            SimVector3 contactCenter, ExecutionEnvelopeV4 envelope,
            ExecutionSampleV4 baseline, string candidateIdentity)
        {
            var legal = 0;
            var offsets = DeterministicVelocityOffsets(envelope.VelocityError);
            for (var index = 0; index < offsets.Count; index++)
            {
                var velocity = baseline.Velocity + offsets[index];
                var trajectory = PredictTrajectory(request, side, contactCenter, velocity,
                    candidateIdentity + ":trajectory:" + index, envelope.Identity);
                if (IsLegalAttackTrajectory(trajectory, side)) legal++;
            }
            return legal / (float)offsets.Count;
        }
        private static BallTrajectoryPredictionArtifactV4 PredictTrajectory(
            AttackPlanningRequestV3 request, TeamSide side, SimVector3 contactCenter,
            SimVector3 velocity, string samplingKey, string envelopeIdentity) =>
            request.TrajectoryProvider.Predict(new BallTrajectoryPredictionRequestV4(
                side, request.AttackBallStateVersion, new BallState(contactCenter, velocity, .12f),
                request.SimulationParameters, request.PhysicsConfigurationHash, samplingKey,
                request.TrajectoryProvider.PredictorVersion,
                request.TrajectoryProvider.PredictorConfigurationHash, envelopeIdentity,
                ExecutionDegradationStepV4.FullSampling));
        private static IReadOnlyList<SimVector3> DeterministicVelocityOffsets(BoundedErrorDistributionV4 error) =>
            new[]
            {
                SimVector3.Zero,
                new SimVector3(error.MinimumError.X, 0f, 0f),
                new SimVector3(error.MaximumError.X, 0f, 0f),
                new SimVector3(0f, error.MinimumError.Y, 0f),
                new SimVector3(0f, error.MaximumError.Y, 0f),
                new SimVector3(0f, 0f, error.MinimumError.Z),
                new SimVector3(0f, 0f, error.MaximumError.Z)
            };
        private static bool IsLegalAttackTrajectory(BallTrajectoryPredictionArtifactV4 trajectory, TeamSide side)
        {
            var frame = new TeamCourtFrame(side == TeamSide.Home ? TeamId.Blue : TeamId.Orange);
            var samples = trajectory.PredictionSnapshot.Samples;
            var crossed = false;
            for (var index = 1; index < samples.Count; index++)
            {
                var before = frame.ToLocal(samples[index - 1].Position);
                var after = frame.ToLocal(samples[index].Position);
                if (before.Z < 0f && after.Z >= 0f)
                {
                    var alpha = -before.Z / (after.Z - before.Z);
                    var crossing = SimVector3.Lerp(samples[index - 1].Position, samples[index].Position, alpha);
                    crossed = crossing.Y - .12f > 2.48f && Math.Abs(crossing.X) + .12f <= 4.5f;
                    break;
                }
            }
            var landing = trajectory.PredictionSnapshot.GroundLanding;
            if (!crossed || !landing.HasValue) return false;
            var localLanding = frame.ToLocal(landing.Value.Position);
            return Math.Abs(localLanding.X) + .12f <= 4.5f && localLanding.Z >= 0f && localLanding.Z + .12f <= 9f;
        }
        // Public timing is a shared, evidence-derived prediction: the accepted Set
        // contact time plus the distance to the generated landing target.  It is
        // intentionally never a presentation or legacy-decision input.
        private static float ArrivalTime(AttackCandidateV3 candidate, GateISetIntentV3 set) =>
            set.AttackReadyArrivalTime + Math.Max(.01f, (candidate.Target - candidate.ContactCenter).Magnitude / 18f);
        private static bool IsPower(AttackActionClassV3 value) => value == AttackActionClassV3.PowerLine || value == AttackActionClassV3.PowerCross || value == AttackActionClassV3.PowerEdge || value == AttackActionClassV3.PowerOverHand;
        private static string Zone(SimVector3 target) => target.X < -1f ? "Line" : target.X > 1f ? "Cross" : "Middle";
        private static SetFlightSolution SolveSetFlight(SetIntentPlanningRequestV3 request, SimVector3 target)
        {
            // Formal plans use the same deterministic solver/fallback ladder as
            // the physical director; the chosen velocity is then evidence-owned.
            foreach (var rhythm in new[] { SetRhythm.FastPin, SetRhythm.Adjustment, SetRhythm.HighBall })
            {
                try
                {
                    return SetFlightSolver.Solve(new SetFlightRequest(rhythm,
                        request.AcceptedPass.Position, target, 1f, .8f,
                        request.SimulationParameters, FixedSimulationStepSeconds));
                }
                catch (InvalidOperationException) { }
            }
            throw new InvalidOperationException("No deterministic Gate I set flight exists.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Prototype;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using RuntimeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.AI
{
    public readonly struct ReceiveOrganizationPlayerBindingV3
    {
        public ReceiveOrganizationPlayerBindingV3(
            RuntimePlayerId runtimePlayerId,
            StablePlayerId stablePlayerId)
        {
            RallyDecisionCandidate.ValidatePlayerId(
                runtimePlayerId,
                nameof(runtimePlayerId));
            if (string.IsNullOrWhiteSpace(stablePlayerId.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(stablePlayerId));
            }

            RuntimePlayerId = runtimePlayerId;
            StablePlayerId = stablePlayerId;
        }

        public RuntimePlayerId RuntimePlayerId { get; }

        public StablePlayerId StablePlayerId { get; }
    }

    public readonly struct SetterReachabilityEvidenceV3
    {
        public SetterReachabilityEvidenceV3(
            RuntimePlayerId setter,
            bool isAvailable,
            bool isLegal,
            bool wasPreviousTouch,
            bool isReachable,
            float movementMeters,
            float reactionDelaySeconds,
            float reachMarginMeters)
        {
            RallyDecisionCandidate.ValidatePlayerId(setter, nameof(setter));
            ValidateNonNegativeFinite(movementMeters, nameof(movementMeters));
            ValidateNonNegativeFinite(
                reactionDelaySeconds,
                nameof(reactionDelaySeconds));
            if (!IsFinite(reachMarginMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(reachMarginMeters));
            }

            Setter = setter;
            IsAvailable = isAvailable;
            IsLegal = isLegal;
            WasPreviousTouch = wasPreviousTouch;
            IsReachable = isReachable;
            MovementMeters = movementMeters;
            ReactionDelaySeconds = reactionDelaySeconds;
            ReachMarginMeters = reachMarginMeters;
        }

        public RuntimePlayerId Setter { get; }

        public bool IsAvailable { get; }

        public bool IsLegal { get; }

        public bool WasPreviousTouch { get; }

        public bool IsReachable { get; }

        public float MovementMeters { get; }

        public float ReactionDelaySeconds { get; }

        public float ReachMarginMeters { get; }

        private static void ValidateNonNegativeFinite(
            float value,
            string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class ReceiveOrganizationPlanningResult
    {
        public ReceiveOrganizationPlanningResult(
            ReceiveOrganizationPlanV3 plan,
            TeamRallyDecision decision,
            TeamRallyDecision attackPreparationDecision,
            SetterReachabilityEvidenceV3 setterEvidence,
            OrganizationFallbackReasonV3 fallbackReason)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            AttackPreparationDecision = attackPreparationDecision ??
                                        throw new ArgumentNullException(
                                            nameof(attackPreparationDecision));
            if (!Enum.IsDefined(
                    typeof(OrganizationFallbackReasonV3),
                    fallbackReason))
            {
                throw new ArgumentOutOfRangeException(nameof(fallbackReason));
            }

            SetterEvidence = setterEvidence;
            FallbackReason = fallbackReason;
        }

        public ReceiveOrganizationPlanV3 Plan { get; }

        public TeamRallyDecision Decision { get; }

        public TeamRallyDecision AttackPreparationDecision { get; }

        public SetterReachabilityEvidenceV3 SetterEvidence { get; }

        public OrganizationFallbackReasonV3 FallbackReason { get; }
    }

    public sealed class ReceiveOrganizationResponsibilityPlanner
    {
        private readonly TeamRallyDecisionPlanner _decisionPlanner;

        public ReceiveOrganizationResponsibilityPlanner(
            TeamRallyDecisionPlanner decisionPlanner)
        {
            _decisionPlanner = decisionPlanner ??
                               throw new ArgumentNullException(nameof(decisionPlanner));
        }

        public ReceiveOrganizationPlanningResult PlanReceive(
            TeamRallyDecisionInput receiveInput,
            TeamRallyDecisionInput attackPreparationInput,
            OnCourtEligibilitySnapshot eligibility,
            IReadOnlyList<ReceiveOrganizationPlayerBindingV3> bindings,
            long revision)
        {
            return PlanReceive(
                receiveInput,
                attackPreparationInput,
                eligibility,
                bindings,
                revision,
                null);
        }

        public ReceiveOrganizationPlanningResult PlanReceive(
            TeamRallyDecisionInput receiveInput,
            TeamRallyDecisionInput attackPreparationInput,
            OnCourtEligibilitySnapshot eligibility,
            IReadOnlyList<ReceiveOrganizationPlayerBindingV3> bindings,
            long revision,
            StablePlayerId? committedContinuationReceiver)
        {
            var context = ValidateAndResolve(
                receiveInput,
                RallyDecisionStage.Receive,
                attackPreparationInput,
                eligibility,
                bindings,
                revision);
            var ordered = _decisionPlanner.OrderedCandidates(receiveInput);
            var feasible = ordered
                .Where(candidate => candidate.IsFeasible)
                .ToArray();
            if (feasible.Length == 0)
            {
                throw new InvalidOperationException(
                    "Receive planning requires at least one reachable receiver.");
            }

            var selected = feasible[0];
            if (committedContinuationReceiver.HasValue)
            {
                var matched = feasible
                    .Where(candidate => context.StableFor(candidate.Actor)
                        .Equals(committedContinuationReceiver.Value))
                    .ToArray();
                if (matched.Length != 1)
                {
                    throw new InvalidOperationException(
                        "The committed continuation receiver must be a reachable on-court candidate.");
                }

                selected = matched[0];
            }

            var decision = _decisionPlanner.MaterializeCandidate(
                receiveInput,
                selected.Actor);
            var attackDecision = _decisionPlanner.Plan(attackPreparationInput);
            var plan = CreatePlan(
                context,
                ordered,
                selected.Actor,
                attackDecision,
                revision);
            return new ReceiveOrganizationPlanningResult(
                plan,
                decision,
                attackDecision,
                CreateSetterEvidence(
                    receiveInput,
                    ordered,
                    context.RegisteredSetter.RuntimePlayerId),
                OrganizationFallbackReasonV3.None);
        }

        public ReceiveOrganizationPlanningResult PlanOrganization(
            TeamRallyDecisionInput organizationInput,
            TeamRallyDecisionInput attackPreparationInput,
            OnCourtEligibilitySnapshot eligibility,
            IReadOnlyList<ReceiveOrganizationPlayerBindingV3> bindings,
            long revision)
        {
            var context = ValidateAndResolve(
                organizationInput,
                RallyDecisionStage.Organize,
                attackPreparationInput,
                eligibility,
                bindings,
                revision);
            var ordered = _decisionPlanner.OrderedCandidates(organizationInput);
            var setterId = context.RegisteredSetter.RuntimePlayerId;
            var setterCandidate = FindCandidate(ordered, setterId);
            var wasPreviousTouch =
                organizationInput.LastCountedActor.HasValue &&
                organizationInput.LastCountedActor.Value.Equals(setterId);
            var selected = setterCandidate.IsFeasible && !wasPreviousTouch
                ? setterCandidate
                : FirstFeasibleBackup(ordered, setterId);
            var decision = selected.HasValue
                ? _decisionPlanner.MaterializeCandidate(
                    organizationInput,
                    selected.Value.Actor)
                : TeamRallyDecision.NoDecision;
            var fallback = ResolveFallback(
                selected.HasValue,
                setterCandidate,
                wasPreviousTouch);
            var attackDecision = _decisionPlanner.Plan(attackPreparationInput);
            var primary = FirstFeasibleActor(ordered) ??
                          FirstNonSetterActor(ordered, setterId);
            var plan = CreatePlan(
                context,
                ordered,
                primary,
                attackDecision,
                revision);
            return new ReceiveOrganizationPlanningResult(
                plan,
                decision,
                attackDecision,
                CreateSetterEvidence(organizationInput, ordered, setterId),
                fallback);
        }

        private static PlanningContext ValidateAndResolve(
            TeamRallyDecisionInput input,
            RallyDecisionStage expectedStage,
            TeamRallyDecisionInput attackInput,
            OnCourtEligibilitySnapshot eligibility,
            IReadOnlyList<ReceiveOrganizationPlayerBindingV3> bindings,
            long revision)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (attackInput == null)
            {
                throw new ArgumentNullException(nameof(attackInput));
            }

            if (eligibility == null)
            {
                throw new ArgumentNullException(nameof(eligibility));
            }

            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            if (input.Stage != expectedStage)
            {
                throw new ArgumentException(
                    $"Expected a {expectedStage} decision input.",
                    nameof(input));
            }

            if (attackInput.Stage != RallyDecisionStage.Attack ||
                attackInput.Team != input.Team)
            {
                throw new ArgumentException(
                    "Attack preparation must use an Attack-stage input for the same team.",
                    nameof(attackInput));
            }

            var side = input.Team == RuntimeTeamId.Blue
                ? TeamSide.Home
                : TeamSide.Away;
            var byRuntime = new Dictionary<RuntimePlayerId, ReceiveOrganizationPlayerBindingV3>();
            var stableIds = new HashSet<StablePlayerId>();
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.RuntimePlayerId.Team != input.Team ||
                    !byRuntime.TryAdd(binding.RuntimePlayerId, binding) ||
                    !stableIds.Add(binding.StablePlayerId))
                {
                    throw new ArgumentException(
                        "Bindings must be distinct members of the acting runtime team.",
                        nameof(bindings));
                }

                OnCourtPlayerEligibilityV3 onCourt;
                try
                {
                    onCourt = eligibility.For(binding.StablePlayerId);
                }
                catch (KeyNotFoundException exception)
                {
                    throw new ArgumentException(
                        "Every bound stable player must be on court.",
                        nameof(bindings),
                        exception);
                }

                if (onCourt.Side != side)
                {
                    throw new ArgumentException(
                        "Bindings must belong to the acting stable team side.",
                        nameof(bindings));
                }
            }

            ValidatePlayersAreBound(input.Players, byRuntime, nameof(input));
            ValidatePlayersAreBound(
                attackInput.Players,
                byRuntime,
                nameof(attackInput));

            var registeredSetters = bindings
                .Where(binding =>
                    eligibility.For(binding.StablePlayerId).RegisteredPosition ==
                    PlayerPosition.Setter)
                .ToArray();
            if (registeredSetters.Length != 1)
            {
                throw new ArgumentException(
                    "Exactly one bound on-court registered setter is required.",
                    nameof(bindings));
            }

            return new PlanningContext(
                side,
                new ReadOnlyDictionary<RuntimePlayerId, ReceiveOrganizationPlayerBindingV3>(
                    byRuntime),
                registeredSetters[0]);
        }

        private static void ValidatePlayersAreBound(
            IReadOnlyList<RallyPlayerSnapshot> players,
            IReadOnlyDictionary<RuntimePlayerId, ReceiveOrganizationPlayerBindingV3> bindings,
            string parameterName)
        {
            for (var index = 0; index < players.Count; index++)
            {
                if (!bindings.ContainsKey(players[index].Id))
                {
                    throw new ArgumentException(
                        "Every decision player requires a stable-ID binding.",
                        parameterName);
                }
            }
        }

        private static ReceiveOrganizationPlanV3 CreatePlan(
            PlanningContext context,
            IReadOnlyList<RallyDecisionCandidate> ordered,
            RuntimePlayerId primaryActor,
            TeamRallyDecision attackDecision,
            long revision)
        {
            var primary = context.StableFor(primaryActor);
            var emergencies = ordered
                .Where(candidate =>
                    candidate.IsFeasible &&
                    !candidate.Actor.Equals(primaryActor))
                .Take(2)
                .Select(candidate => context.StableFor(candidate.Actor))
                .ToArray();
            var backups = ordered
                .Where(candidate =>
                    candidate.IsFeasible &&
                    !candidate.Actor.Equals(
                        context.RegisteredSetter.RuntimePlayerId))
                .Take(5)
                .Select(candidate => context.StableFor(candidate.Actor))
                .ToArray();
            var attackPreparation = attackDecision.HasDecision
                ? context.StableFor(attackDecision.Actor)
                : primary;
            var runtimeTeam = context.Side == TeamSide.Home
                ? RuntimeTeamId.Blue
                : RuntimeTeamId.Orange;
            return new ReceiveOrganizationPlanV3(
                context.Side,
                revision,
                primary,
                context.RegisteredSetter.StablePlayerId,
                emergencies,
                backups,
                attackPreparation,
                SetterOrganizationZone.DefaultWorldTarget(runtimeTeam));
        }

        private static SetterReachabilityEvidenceV3 CreateSetterEvidence(
            TeamRallyDecisionInput input,
            IReadOnlyList<RallyDecisionCandidate> ordered,
            RuntimePlayerId setter)
        {
            var candidate = FindCandidate(ordered, setter);
            var player = input.Players.Single(snapshot => snapshot.Id.Equals(setter));
            var wasPreviousTouch =
                input.LastCountedActor.HasValue &&
                input.LastCountedActor.Value.Equals(setter);
            return new SetterReachabilityEvidenceV3(
                setter,
                isAvailable: true,
                isLegal: !wasPreviousTouch,
                wasPreviousTouch,
                isReachable: candidate.IsFeasible && !wasPreviousTouch,
                TeamRallyDecisionPlanner.MovementMeters(input, player),
                TeamRallyDecisionPlanner.ReactionDelaySeconds(player),
                candidate.Score.Reachability);
        }

        private static RallyDecisionCandidate FindCandidate(
            IReadOnlyList<RallyDecisionCandidate> candidates,
            RuntimePlayerId actor)
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Actor.Equals(actor))
                {
                    return candidates[index];
                }
            }

            throw new ArgumentException(
                "The registered setter is not a decision candidate.",
                nameof(actor));
        }

        private static RallyDecisionCandidate? FirstFeasibleBackup(
            IReadOnlyList<RallyDecisionCandidate> ordered,
            RuntimePlayerId setter)
        {
            for (var index = 0; index < ordered.Count; index++)
            {
                if (ordered[index].IsFeasible &&
                    !ordered[index].Actor.Equals(setter))
                {
                    return ordered[index];
                }
            }

            return null;
        }

        private static RuntimePlayerId? FirstFeasibleActor(
            IReadOnlyList<RallyDecisionCandidate> ordered)
        {
            for (var index = 0; index < ordered.Count; index++)
            {
                if (ordered[index].IsFeasible)
                {
                    return ordered[index].Actor;
                }
            }

            return null;
        }

        private static RuntimePlayerId FirstNonSetterActor(
            IReadOnlyList<RallyDecisionCandidate> ordered,
            RuntimePlayerId setter)
        {
            for (var index = 0; index < ordered.Count; index++)
            {
                if (!ordered[index].Actor.Equals(setter))
                {
                    return ordered[index].Actor;
                }
            }

            throw new InvalidOperationException(
                "Receive and organization planning requires a non-setter teammate.");
        }

        private static OrganizationFallbackReasonV3 ResolveFallback(
            bool hasSelectedOrganizer,
            RallyDecisionCandidate setter,
            bool wasPreviousTouch)
        {
            if (!hasSelectedOrganizer)
            {
                return OrganizationFallbackReasonV3.NoLegalOrganizer;
            }

            if (wasPreviousTouch)
            {
                return OrganizationFallbackReasonV3.SetterPreviousTouch;
            }

            return setter.IsFeasible
                ? OrganizationFallbackReasonV3.None
                : OrganizationFallbackReasonV3.SetterUnreachable;
        }

        private sealed class PlanningContext
        {
            public PlanningContext(
                TeamSide side,
                IReadOnlyDictionary<RuntimePlayerId, ReceiveOrganizationPlayerBindingV3> byRuntime,
                ReceiveOrganizationPlayerBindingV3 registeredSetter)
            {
                Side = side;
                ByRuntime = byRuntime;
                RegisteredSetter = registeredSetter;
            }

            public TeamSide Side { get; }

            public IReadOnlyDictionary<RuntimePlayerId, ReceiveOrganizationPlayerBindingV3> ByRuntime { get; }

            public ReceiveOrganizationPlayerBindingV3 RegisteredSetter { get; }

            public StablePlayerId StableFor(RuntimePlayerId runtimeId)
            {
                return ByRuntime[runtimeId].StablePlayerId;
            }
        }
    }
}

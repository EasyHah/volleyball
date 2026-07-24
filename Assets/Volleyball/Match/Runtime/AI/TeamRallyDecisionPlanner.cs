using System;
using System.Collections.Generic;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public enum RallyDecisionStage
    {
        Receive,
        Organize,
        Attack,
        Block
    }

    public readonly struct RallyPlayerSnapshot
    {
        public RallyPlayerSnapshot(PlayerId id, SimVector3 worldPosition, PlayerAbilityProfile ability)
        {
            ValidatePlayerId(id, nameof(id));
            if (!worldPosition.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            }

            Id = id;
            WorldPosition = worldPosition;
            Ability = ability;
        }

        public PlayerId Id { get; }

        public SimVector3 WorldPosition { get; }

        public PlayerAbilityProfile Ability { get; }

        private static void ValidatePlayerId(PlayerId id, string parameterName)
        {
            if (!Enum.IsDefined(typeof(TeamId), id.Team) || !Enum.IsDefined(typeof(PlayerRole), id.Role))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public readonly struct AttackApproachPlan : IEquatable<AttackApproachPlan>
    {
        public AttackApproachPlan(
            SimVector3 approachStart,
            SimVector3 takeoff,
            float distance,
            float jumpQuality,
            float anglePenalty)
        {
            if (!approachStart.IsFinite || !takeoff.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(approachStart));
            }

            if (!IsFinite(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            if (!IsUnitInterval(jumpQuality))
            {
                throw new ArgumentOutOfRangeException(nameof(jumpQuality));
            }

            if (!IsUnitInterval(anglePenalty))
            {
                throw new ArgumentOutOfRangeException(nameof(anglePenalty));
            }

            ApproachStart = approachStart;
            Takeoff = takeoff;
            Distance = distance;
            JumpQuality = jumpQuality;
            AnglePenalty = anglePenalty;
        }

        public SimVector3 ApproachStart { get; }

        public SimVector3 Takeoff { get; }

        public float Distance { get; }

        public float JumpQuality { get; }

        public float AnglePenalty { get; }

        public bool Equals(AttackApproachPlan other)
        {
            return ApproachStart.Equals(other.ApproachStart) &&
                   Takeoff.Equals(other.Takeoff) &&
                   Distance.Equals(other.Distance) &&
                   JumpQuality.Equals(other.JumpQuality) &&
                   AnglePenalty.Equals(other.AnglePenalty);
        }

        public override bool Equals(object obj)
        {
            return obj is AttackApproachPlan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ApproachStart.GetHashCode();
                hashCode = (hashCode * 397) ^ Takeoff.GetHashCode();
                hashCode = (hashCode * 397) ^ Distance.GetHashCode();
                hashCode = (hashCode * 397) ^ JumpQuality.GetHashCode();
                return (hashCode * 397) ^ AnglePenalty.GetHashCode();
            }
        }

        private static bool IsUnitInterval(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class AttackApproachStaging
    {
        public static SimVector3 TargetAtSetContact(
            AttackApproachPlan approach,
            float setFlightSeconds,
            float movementSpeed,
            float jumpLeadSeconds)
        {
            if (!IsFinite(setFlightSeconds) || setFlightSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(setFlightSeconds));
            }

            if (!IsFinite(movementSpeed) || movementSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementSpeed));
            }

            if (!IsFinite(jumpLeadSeconds) || jumpLeadSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(jumpLeadSeconds));
            }

            var route = approach.Takeoff - approach.ApproachStart;
            var routeDistance = GroundDistance(route);
            if (routeDistance <= 0.00001f)
            {
                return approach.ApproachStart;
            }

            var postSetApproachDistance = movementSpeed *
                                          Math.Max(0f, setFlightSeconds - jumpLeadSeconds);
            var preSetAdvance = Math.Max(0f, routeDistance - postSetApproachDistance);
            var progress = Math.Min(1f, preSetAdvance / routeDistance);
            return approach.ApproachStart + (route * progress);
        }

        private static float GroundDistance(SimVector3 value)
        {
            return (float)Math.Sqrt((value.X * value.X) + (value.Z * value.Z));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct RallyDecisionScore : IEquatable<RallyDecisionScore>
    {
        public RallyDecisionScore(float reachability, float nominalRole, float approach, float angle, float total)
        {
            ValidateFinite(reachability, nameof(reachability));
            ValidateFinite(nominalRole, nameof(nominalRole));
            ValidateFinite(approach, nameof(approach));
            ValidateFinite(angle, nameof(angle));
            ValidateFinite(total, nameof(total));
            Reachability = reachability;
            NominalRole = nominalRole;
            Approach = approach;
            Angle = angle;
            Total = total;
        }

        public float Reachability { get; }

        public float NominalRole { get; }

        public float Approach { get; }

        public float Angle { get; }

        public float Total { get; }

        public bool Equals(RallyDecisionScore other)
        {
            return Reachability.Equals(other.Reachability) &&
                   NominalRole.Equals(other.NominalRole) &&
                   Approach.Equals(other.Approach) &&
                   Angle.Equals(other.Angle) &&
                   Total.Equals(other.Total);
        }

        public override bool Equals(object obj)
        {
            return obj is RallyDecisionScore other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Reachability.GetHashCode();
                hashCode = (hashCode * 397) ^ NominalRole.GetHashCode();
                hashCode = (hashCode * 397) ^ Approach.GetHashCode();
                hashCode = (hashCode * 397) ^ Angle.GetHashCode();
                return (hashCode * 397) ^ Total.GetHashCode();
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public readonly struct RallyDecisionCandidate
    {
        public RallyDecisionCandidate(PlayerId actor, bool isFeasible, RallyDecisionScore score)
        {
            ValidatePlayerId(actor, nameof(actor));
            ValidateScore(score, nameof(score));
            Actor = actor;
            IsFeasible = isFeasible;
            Score = score;
        }

        public PlayerId Actor { get; }

        public bool IsFeasible { get; }

        public RallyDecisionScore Score { get; }

        internal static void ValidatePlayerId(PlayerId id, string parameterName)
        {
            if (!Enum.IsDefined(typeof(TeamId), id.Team) || !Enum.IsDefined(typeof(PlayerRole), id.Role))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        internal static void ValidateScore(RallyDecisionScore score, string parameterName)
        {
            if (!IsFinite(score.Reachability) ||
                !IsFinite(score.NominalRole) ||
                !IsFinite(score.Approach) ||
                !IsFinite(score.Angle) ||
                !IsFinite(score.Total))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class TeamRallyDecision
    {
        private static readonly IReadOnlyList<RallyDecisionCandidate> EmptyCandidates =
            new List<RallyDecisionCandidate>().AsReadOnly();

        public static TeamRallyDecision NoDecision { get; } = new TeamRallyDecision();

        private TeamRallyDecision()
        {
            Candidates = EmptyCandidates;
        }

        public TeamRallyDecision(
            PlayerId actor,
            TechniqueAction action,
            SimVector3 contactTarget,
            SimVector3 movementTarget,
            SimVector3 ballTarget,
            RallyDecisionScore score,
            IEnumerable<RallyDecisionCandidate> candidates,
            AttackApproachPlan? attackApproach,
            AttackContactPlan? attackContactPlan = null)
        {
            RallyDecisionCandidate.ValidatePlayerId(actor, nameof(actor));
            ValidateAction(action, nameof(action));
            RallyDecisionCandidate.ValidateScore(score, nameof(score));
            if (!contactTarget.IsFinite || !movementTarget.IsFinite || !ballTarget.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(contactTarget));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (action == TechniqueAction.Attack && !attackApproach.HasValue)
            {
                throw new ArgumentException("Attack decisions require an approach plan.", nameof(attackApproach));
            }

            if (action == TechniqueAction.Attack && !attackContactPlan.HasValue)
            {
                throw new ArgumentException("Attack decisions require a contact plan.", nameof(attackContactPlan));
            }

            if (action != TechniqueAction.Attack && attackApproach.HasValue)
            {
                throw new ArgumentException("Only attack decisions may include an approach plan.", nameof(attackApproach));
            }

            if (action != TechniqueAction.Attack && attackContactPlan.HasValue)
            {
                throw new ArgumentException("Only attack decisions may include a contact plan.", nameof(attackContactPlan));
            }

            if (attackApproach.HasValue && attackContactPlan.HasValue &&
                !attackApproach.Value.Takeoff.Equals(attackContactPlan.Value.Takeoff))
            {
                throw new ArgumentException("Attack approach and contact plan must use the same takeoff.", nameof(attackContactPlan));
            }

            if (attackContactPlan.HasValue)
            {
                attackContactPlan.Value.Validate();
            }

            var candidateCopy = CopyAndValidateCandidates(candidates, actor);

            HasDecision = true;
            Actor = actor;
            Action = action;
            ContactTarget = contactTarget;
            MovementTarget = movementTarget;
            BallTarget = ballTarget;
            Score = score;
            Candidates = candidateCopy;
            AttackApproach = attackApproach;
            AttackContactPlan = attackContactPlan;
        }

        public bool HasDecision { get; }

        public PlayerId Actor { get; }

        public TechniqueAction Action { get; }

        public SimVector3 ContactTarget { get; }

        public SimVector3 MovementTarget { get; }

        public SimVector3 BallTarget { get; }

        public RallyDecisionScore Score { get; }

        public IReadOnlyList<RallyDecisionCandidate> Candidates { get; }

        public AttackApproachPlan? AttackApproach { get; }

        public AttackContactPlan? AttackContactPlan { get; }

        private static IReadOnlyList<RallyDecisionCandidate> CopyAndValidateCandidates(
            IEnumerable<RallyDecisionCandidate> candidates,
            PlayerId actor)
        {
            var copy = new List<RallyDecisionCandidate>();
            var containsActor = false;
            foreach (var candidate in candidates)
            {
                RallyDecisionCandidate.ValidatePlayerId(candidate.Actor, nameof(candidates));
                RallyDecisionCandidate.ValidateScore(candidate.Score, nameof(candidates));
                if (candidate.Actor.Team != actor.Team)
                {
                    throw new ArgumentException("Candidates must belong to the decision actor's team.", nameof(candidates));
                }

                containsActor |= candidate.Actor.Equals(actor);
                copy.Add(candidate);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("A decision requires at least one candidate.", nameof(candidates));
            }

            if (!containsActor)
            {
                throw new ArgumentException("Candidates must include the selected actor.", nameof(candidates));
            }

            return copy.AsReadOnly();
        }

        private static void ValidateAction(TechniqueAction action, string parameterName)
        {
            if (!Enum.IsDefined(typeof(TechniqueAction), action))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class TeamRallyDecisionInput
    {
        private readonly IReadOnlyList<RallyPlayerSnapshot> _players;

        public TeamRallyDecisionInput(
            TeamId team,
            TeamRallyTactic tactic,
            IEnumerable<RallyPlayerSnapshot> players,
            SimVector3 predictedBallCenter,
            float availableSeconds,
            float baseMovementSpeed,
            int countedTouches,
            PlayerId? lastCountedActor,
            int tacticRevision,
            int decisionIndex,
            RallyDecisionStage stage,
            RallyTacticalWeights weights)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (!predictedBallCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(predictedBallCenter));
            }

            ValidatePositiveFinite(availableSeconds, nameof(availableSeconds));
            ValidatePositiveFinite(baseMovementSpeed, nameof(baseMovementSpeed));
            if (countedTouches < 0 || countedTouches > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(countedTouches));
            }

            if (tacticRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tacticRevision));
            }

            if (decisionIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decisionIndex));
            }

            if (!Enum.IsDefined(typeof(RallyDecisionStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }

            var playerCopy = CopyAndValidatePlayers(players, team);
            if (lastCountedActor.HasValue && lastCountedActor.Value.Team != team)
            {
                throw new ArgumentOutOfRangeException(nameof(lastCountedActor));
            }

            Team = team;
            Tactic = tactic;
            _players = playerCopy;
            PredictedBallCenter = predictedBallCenter;
            AvailableSeconds = availableSeconds;
            BaseMovementSpeed = baseMovementSpeed;
            CountedTouches = countedTouches;
            LastCountedActor = lastCountedActor;
            TacticRevision = tacticRevision;
            DecisionIndex = decisionIndex;
            Stage = stage;
            Weights = weights;
        }

        public TeamId Team { get; }

        public TeamRallyTactic Tactic { get; }

        public IReadOnlyList<RallyPlayerSnapshot> Players => _players;

        public SimVector3 PredictedBallCenter { get; }

        public float AvailableSeconds { get; }

        public float BaseMovementSpeed { get; }

        public int CountedTouches { get; }

        public PlayerId? LastCountedActor { get; }

        public int TacticRevision { get; }

        public int DecisionIndex { get; }

        public RallyDecisionStage Stage { get; }

        public RallyTacticalWeights Weights { get; }

        private static IReadOnlyList<RallyPlayerSnapshot> CopyAndValidatePlayers(
            IEnumerable<RallyPlayerSnapshot> players,
            TeamId team)
        {
            var copy = new List<RallyPlayerSnapshot>();
            var ids = new HashSet<PlayerId>();
            foreach (var player in players)
            {
                if (player.Id.Team != team || !ids.Add(player.Id))
                {
                    throw new ArgumentException("Players must be distinct members of the acting team.", nameof(players));
                }

                copy.Add(player);
            }

            if (copy.Count < 3 || copy.Count > 6)
            {
                throw new ArgumentException("A rally team requires three to six players.", nameof(players));
            }

            return copy.AsReadOnly();
        }

        private static void ValidatePositiveFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class TeamRallyDecisionPlanner
    {
        private const float ReactionSeconds = 0.22f;
        private const float AttackContactPreparationSeconds = 0.2f;
        private const float FullJumpPreparationSeconds = 0.38f;
        private const float MaximumApproachDistance = 2f;
        private const float FullJumpApproachDistance = 1.5f;
        private readonly int _seed;

        public TeamRallyDecisionPlanner(int seed)
        {
            _seed = seed;
        }

        public TeamRallyDecision Plan(TeamRallyDecisionInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Stage == RallyDecisionStage.Block)
            {
                return TeamRallyDecision.NoDecision;
            }

            var targets = CreateTargets(input);
            var candidates = new List<RallyDecisionCandidate>();
            var bestIndex = -1;
            for (var index = 0; index < input.Players.Count; index++)
            {
                var player = input.Players[index];
                var excluded = ExcludesActor(input, player.Id);
                var approach = input.Stage == RallyDecisionStage.Attack
                    ? CreateAttackApproach(input, player, targets.Takeoff, targets.BallTarget)
                    : (AttackApproachPlan?)null;
                var score = Score(input, player, targets.MovementTarget, approach);
                var feasible = !excluded && score.Reachability >= 0f;
                candidates.Add(new RallyDecisionCandidate(player.Id, feasible, score));
                if (feasible && IsBetter(candidates, index, bestIndex))
                {
                    bestIndex = index;
                }
            }

            bestIndex = PreferEligibleSetterForOrganization(input, candidates, bestIndex);
            if (bestIndex < 0)
            {
                return TeamRallyDecision.NoDecision;
            }

            var winner = input.Players[bestIndex];
            var winningApproach = input.Stage == RallyDecisionStage.Attack
                ? CreateAttackApproach(input, winner, targets.Takeoff, targets.BallTarget)
                : (AttackApproachPlan?)null;
            var winningContactPlan = winningApproach.HasValue
                ? CreateAttackContactPlan(input, winner, winningApproach.Value)
                : (AttackContactPlan?)null;
            return new TeamRallyDecision(
                winner.Id,
                targets.Action,
                winningContactPlan?.ContactCenter ?? targets.ContactTarget,
                targets.MovementTarget,
                targets.BallTarget,
                candidates[bestIndex].Score,
                candidates,
                winningApproach,
                winningContactPlan);
        }

        private static bool ExcludesActor(TeamRallyDecisionInput input, PlayerId actor)
        {
            return input.Stage != RallyDecisionStage.Receive &&
                   input.LastCountedActor.HasValue &&
                   input.LastCountedActor.Value.Equals(actor);
        }

        private static int PreferEligibleSetterForOrganization(
            TeamRallyDecisionInput input,
            IReadOnlyList<RallyDecisionCandidate> candidates,
            int fallbackIndex)
        {
            if (input.Stage != RallyDecisionStage.Organize)
            {
                return fallbackIndex;
            }

            var setterIndex = -1;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.IsFeasible || candidate.Actor.Role != PlayerRole.Setter)
                {
                    continue;
                }

                if (setterIndex < 0 || IsBetter(candidates, index, setterIndex))
                {
                    setterIndex = index;
                }
            }

            return setterIndex >= 0 ? setterIndex : fallbackIndex;
        }

        private static bool IsBetter(IReadOnlyList<RallyDecisionCandidate> candidates, int index, int bestIndex)
        {
            if (bestIndex < 0)
            {
                return true;
            }

            var current = candidates[index];
            var best = candidates[bestIndex];
            if (current.Score.Total != best.Score.Total)
            {
                return current.Score.Total > best.Score.Total;
            }

            if (current.Actor.Role != best.Actor.Role)
            {
                return (int)current.Actor.Role < (int)best.Actor.Role;
            }

            return current.Actor.GetHashCode() < best.Actor.GetHashCode();
        }

        private static RallyDecisionScore Score(
            TeamRallyDecisionInput input,
            RallyPlayerSnapshot player,
            SimVector3 movementTarget,
            AttackApproachPlan? approach)
        {
            var effectiveSeconds = EffectiveSeconds(input, player);
            var speed = MovementSpeed(input, player);
            var distance = GroundDistance(player.WorldPosition, movementTarget);
            var reachability = (speed * effectiveSeconds) - distance;
            var nominalRole = NominalRole(input.Stage, player.Id.Role) * input.Weights.RolePreference;
            var approachScore = approach.HasValue
                ? approach.Value.JumpQuality *
                  player.Ability.Jump *
                  input.Weights.ApproachDistance
                : 0f;
            var angleScore = approach.HasValue
                ? -approach.Value.AnglePenalty * (1f - (0.5f * input.Weights.DirectionTolerance))
                : 0f;
            var techniqueScore = input.Stage == RallyDecisionStage.Attack
                ? 0f
                : player.Ability.TechniqueFor(ToTechniqueAction(input.Stage));
            // Feasibility is selected separately, so role preference cannot rescue a negative margin.
            var total = (reachability * input.Weights.Reachability) + nominalRole + approachScore + angleScore + techniqueScore;
            return new RallyDecisionScore(reachability, nominalRole, approachScore, angleScore, total);
        }

        private static float NominalRole(RallyDecisionStage stage, PlayerRole role)
        {
            return stage switch
            {
                RallyDecisionStage.Receive => role switch
                {
                    PlayerRole.Defender => 1f,
                    PlayerRole.OutsideHitter => 0.78f,
                    PlayerRole.Attacker => 0.68f,
                    PlayerRole.Opposite => 0.42f,
                    _ => 0f
                },
                RallyDecisionStage.Organize => role == PlayerRole.Setter ? 1f : 0f,
                RallyDecisionStage.Attack => role switch
                {
                    PlayerRole.Attacker => 1f,
                    PlayerRole.OutsideHitter => 0.96f,
                    PlayerRole.Opposite => 0.94f,
                    PlayerRole.MiddleBlocker => 0.86f,
                    _ => 0f
                },
                _ => 0f
            };
        }

        private static TechniqueAction ToTechniqueAction(RallyDecisionStage stage)
        {
            return stage == RallyDecisionStage.Receive ? TechniqueAction.Receive : TechniqueAction.Set;
        }

        private static DecisionTargets CreateTargets(TeamRallyDecisionInput input)
        {
            var groundBall = new SimVector3(input.PredictedBallCenter.X, 0f, input.PredictedBallCenter.Z);
            if (input.Stage == RallyDecisionStage.Receive)
            {
                return new DecisionTargets(TechniqueAction.Receive, groundBall, groundBall, groundBall, groundBall);
            }

            if (input.Stage == RallyDecisionStage.Organize)
            {
                var organizeFrame = new TeamCourtFrame(input.Team);
                var organizeTakeoff = AttackTakeoff(input.Tactic, organizeFrame);
                var futureContact = ProvisionalAttackContact(input, organizeTakeoff);
                return new DecisionTargets(
                    TechniqueAction.Set,
                    input.PredictedBallCenter,
                    groundBall,
                    futureContact,
                    organizeTakeoff);
            }

            var frame = new TeamCourtFrame(input.Team);
            var takeoff = AttackTakeoff(input.Tactic, frame);
            return new DecisionTargets(
                TechniqueAction.Attack,
                takeoff,
                takeoff,
                LandingTarget(input.Tactic, frame),
                takeoff);
        }

        private static SimVector3 AttackTakeoff(TeamRallyTactic tactic, TeamCourtFrame frame)
        {
            var authoredWorld = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            var local = frame.ToLocal(authoredWorld);
            return frame.ToWorld(new SimVector3(local.X, 0f, local.Z));
        }

        private static SimVector3 LandingTarget(TeamRallyTactic tactic, TeamCourtFrame frame)
        {
            var attackLocal = frame.ToLocal(new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z));
            var x = tactic.SpikeRoute switch
            {
                SpikeRoute.Line => attackLocal.X,
                SpikeRoute.CrossCourt => -attackLocal.X * 0.78f,
                SpikeRoute.DeepSeam => 0f,
                SpikeRoute.RollShot => -attackLocal.X * 0.35f,
                _ => attackLocal.X
            };
            var depth = tactic.SpikeRoute == SpikeRoute.RollShot ? 4.05f : 5.25f;
            return frame.ToWorld(new SimVector3(Clamp(x, -3.6f, 3.6f), 0f, depth));
        }

        private static AttackApproachPlan CreateAttackApproach(
            TeamRallyDecisionInput input,
            RallyPlayerSnapshot player,
            SimVector3 takeoff,
            SimVector3 landing)
        {
            var frame = new TeamCourtFrame(input.Team);
            var localTakeoff = frame.ToLocal(takeoff);
            var desiredDistance = 0.6f + (1.4f * player.Ability.Mobility);
            var effectiveSeconds = EffectiveSeconds(input, player);
            var speed = MovementSpeed(input, player);
            var availablePathDistance = speed * Math.Max(0f, effectiveSeconds - AttackContactPreparationSeconds);
            var usableDistance = FindReachableApproachDistance(
                frame.ToLocal(player.WorldPosition),
                localTakeoff,
                Math.Min(desiredDistance, MaximumApproachDistance),
                availablePathDistance);
            var localStart = ApproachStart(localTakeoff, usableDistance);
            var distance = GroundDistance(localStart, localTakeoff);
            var jumpQuality = SmoothCappedJumpQuality(distance);
            var localLanding = frame.ToLocal(landing);
            var approachDirection = HorizontalDirection(localTakeoff - localStart);
            var spikeDirection = HorizontalDirection(localLanding - localTakeoff);
            var cosine = Clamp(SimVector3.Dot(approachDirection, spikeDirection), -1f, 1f);
            var rawPenalty = (1f - cosine) * 0.5f;
            var tolerance = Clamp(input.Weights.DirectionTolerance * 0.5f, 0f, 1f);
            var anglePenalty = Clamp(rawPenalty * (1f - tolerance), 0f, 1f);
            return new AttackApproachPlan(frame.ToWorld(localStart), takeoff, distance, jumpQuality, anglePenalty);
        }

        private static SimVector3 ProvisionalAttackContact(
            TeamRallyDecisionInput input,
            SimVector3 takeoff)
        {
            RallyPlayerSnapshot? provisional = null;
            var bestRole = float.MinValue;
            foreach (var player in input.Players)
            {
                if (input.LastCountedActor.HasValue && input.LastCountedActor.Value.Equals(player.Id))
                {
                    continue;
                }

                var role = NominalRole(RallyDecisionStage.Attack, player.Id.Role);
                if (!provisional.HasValue || role > bestRole ||
                    role.Equals(bestRole) &&
                    player.Ability.PlannedAttackContactHeightMeters >
                    provisional.Value.Ability.PlannedAttackContactHeightMeters)
                {
                    provisional = player;
                    bestRole = role;
                }
            }

            if (!provisional.HasValue)
            {
                return new SimVector3(takeoff.X, AttackContactPlanner.MinimumAttackReach, takeoff.Z);
            }

            var landing = LandingTarget(input.Tactic, new TeamCourtFrame(input.Team));
            var approach = CreateAttackApproach(input, provisional.Value, takeoff, landing);
            return CreateAttackContactPlan(input, provisional.Value, approach).ContactCenter;
        }

        private static AttackContactPlan CreateAttackContactPlan(
            TeamRallyDecisionInput input,
            RallyPlayerSnapshot player,
            AttackApproachPlan approach)
        {
            var availableSeconds = EffectiveSeconds(input, player);
            var movementSpeed = MovementSpeed(input, player);
            var requiredSeconds = AttackContactPreparationSeconds +
                                  (movementSpeed <= 0f ? 0f : approach.Distance / movementSpeed);
            var jumpTiming = Clamp(availableSeconds / FullJumpPreparationSeconds, 0f, 1f);
            return AttackContactPlanner.Plan(new AttackContactInput(
                player.Ability.PlannedAttackContactHeightMeters,
                approach.JumpQuality,
                jumpTiming,
                SetQualityGrade.A,
                approach.Takeoff,
                requiredSeconds,
                availableSeconds));
        }

        private static float FindReachableApproachDistance(
            SimVector3 localPlayer,
            SimVector3 localTakeoff,
            float desiredDistance,
            float availablePathDistance)
        {
            var low = 0f;
            var high = desiredDistance;
            for (var iteration = 0; iteration < 20; iteration++)
            {
                var candidate = (low + high) * 0.5f;
                var start = ApproachStart(localTakeoff, candidate);
                var routeDistance = GroundDistance(localPlayer, start) + candidate;
                if (routeDistance <= availablePathDistance)
                {
                    low = candidate;
                }
                else
                {
                    high = candidate;
                }
            }

            return low;
        }

        private static SimVector3 ApproachStart(SimVector3 localTakeoff, float distance)
        {
            return new SimVector3(localTakeoff.X, localTakeoff.Y, localTakeoff.Z - distance);
        }

        private static float SmoothCappedJumpQuality(float distance)
        {
            var normalized = Clamp(distance / FullJumpApproachDistance, 0f, 1f);
            return normalized * normalized * (3f - (2f * normalized));
        }

        private static float EffectiveSeconds(TeamRallyDecisionInput input, RallyPlayerSnapshot player)
        {
            return Math.Max(0f, input.AvailableSeconds - (ReactionSeconds * (1f - player.Ability.Reaction)));
        }

        private static float MovementSpeed(TeamRallyDecisionInput input, RallyPlayerSnapshot player)
        {
            return input.BaseMovementSpeed * (0.65f + (player.Ability.Mobility * 0.5f));
        }

        private static SimVector3 HorizontalDirection(SimVector3 value)
        {
            var horizontal = new SimVector3(value.X, 0f, value.Z);
            return horizontal.Normalized;
        }

        private static float GroundDistance(SimVector3 first, SimVector3 second)
        {
            var delta = first - second;
            return (float)Math.Sqrt((delta.X * delta.X) + (delta.Z * delta.Z));
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private readonly struct DecisionTargets
        {
            public DecisionTargets(
                TechniqueAction action,
                SimVector3 contactTarget,
                SimVector3 movementTarget,
                SimVector3 ballTarget,
                SimVector3 takeoff)
            {
                Action = action;
                ContactTarget = contactTarget;
                MovementTarget = movementTarget;
                BallTarget = ballTarget;
                Takeoff = takeoff;
            }

            public TechniqueAction Action { get; }

            public SimVector3 ContactTarget { get; }

            public SimVector3 MovementTarget { get; }

            public SimVector3 BallTarget { get; }

            public SimVector3 Takeoff { get; }
        }
    }
}

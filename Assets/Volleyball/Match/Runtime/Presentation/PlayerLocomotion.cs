using System;
using System.Collections;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    public readonly struct PlayerLocomotionSample
    {
        public PlayerLocomotionSample(Vector3 position, bool movementComplete)
        {
            Position = position;
            MovementComplete = movementComplete;
        }

        public Vector3 Position { get; }

        public bool MovementComplete { get; }
    }

    /// <summary>
    /// Owns player root motion. Inputs are immutable timeline samples and execution plans.
    /// </summary>
    public sealed class PlayerLocomotion
    {
        // A bounded portion of each live-root budget is reserved for persistent
        // contact alignment; it is intentionally part of the public route bound.
        private const float AttackAlignmentSpeedAllowance = 0.60f;
        private readonly Transform _root;
        private readonly TeamId _team;
        private readonly float _courtHalfLength;
        private readonly float _moveSpeed;
        private Vector3 _movementStartPosition;
        private Vector3 _movementTargetPosition;
        private float _movementStartSimulationTime;
        private float _movementEndSimulationTime;
        private bool _hasScheduledMovement;
        private bool _hasAttackApproach;
        private Vector3 _attackTakeoffPosition;
        private Vector3 _attackContactRootPosition;
        private bool _hasAttackContactRoot;
        private float _attackContactTime;
        private float _attackJumpLead;
        private float _attackJumpQuality;
        private float _appliedAttackCorrection;
        private Vector3 _attackAlignmentOffset;
        private float _lastSampleSimulationTime = float.NaN;
        private float _lastSampleDeltaSeconds;
        private float _remainingAttackStepDistance = float.PositiveInfinity;
        private TechniqueAction _scheduledAction;
        private PlayerAbilityProfile _scheduledAbility;
        private Vector3 _attackMotionOrigin;
        private Vector3 _attackMotionForward;
        private Vector3 _supportStartPosition;
        private Vector3 _supportTargetPosition;
        private float _supportStartSimulationTime;
        private float _supportEndSimulationTime;
        private TechniqueAction _supportAction;
        private bool _hasSupportMovement;
        private float _supportBlockContactHeight;

        internal SimVector3 PreparedForward { get; set; }
        internal Vector3 MotionOrigin { get; set; }
        internal Vector3 MotionForward { get; set; }
        internal bool IsMovingThisStep { get; set; }
        internal ObservedAttackTakeoff ObservedAttackTakeoff { get; private set; }
        internal bool HasObservedAttackTakeoff { get; private set; }
        internal bool ContinueAttackPreparation { get; set; }

        internal void ClearObservedAttackTakeoff()
        {
            ObservedAttackTakeoff = default;
            HasObservedAttackTakeoff = false;
        }

        internal void RecordObservedAttackTakeoff(ObservedAttackTakeoff takeoff)
        {
            ObservedAttackTakeoff = takeoff;
            HasObservedAttackTakeoff = true;
        }

        public PlayerLocomotion(Transform root, TeamId team, float courtHalfLength, float moveSpeed)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _team = team;
            if (courtHalfLength <= 0f || float.IsNaN(courtHalfLength) || float.IsInfinity(courtHalfLength))
            {
                throw new ArgumentOutOfRangeException(nameof(courtHalfLength));
            }
            if (moveSpeed <= 0f || float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            _courtHalfLength = courtHalfLength;
            _moveSpeed = moveSpeed;
            _movementStartPosition = ConstrainGroundPosition(root.position);
            _movementTargetPosition = _movementStartPosition;
        }

        public Transform Root => _root;

        public float MaximumSpeed { get; private set; }

        public float MovementShortfall { get; private set; }

        public float ScheduledMovementDistance { get; private set; }

        public float MaximumAppliedContactCorrection { get; private set; }

        public Vector3 CurrentAttackAlignmentOffset => _attackAlignmentOffset;

        public Vector3 ScheduledMovementTarget => _movementTargetPosition;

        public bool HasScheduledMovement => _hasScheduledMovement;

        public Vector3 SupportTarget => _supportTargetPosition;

        public bool HasAttackApproach => _hasAttackApproach;

        public bool TryGetAttackTakeoff(float simulationTime, out Vector3 position, out float takeoffTime)
        {
            takeoffTime = Mathf.Max(_movementEndSimulationTime + 0.01f, _attackContactTime - _attackJumpLead);
            if (!_hasAttackApproach || simulationTime < takeoffTime)
            {
                position = default;
                return false;
            }

            var movementPosition = EvaluateScheduledMovement(takeoffTime, out _);
            position = EvaluatePlannedAttackPosition(takeoffTime, movementPosition);
            return true;
        }

        public void ConfigureScheduledMovement(
            Vector3 requestedTarget,
            float movementStartSimulationTime,
            float scheduledContactTime,
            TechniqueAction action,
            PlayerAbilityProfile ability,
            float? movementLeadOverride = null)
        {
            ClearAttackPlanState();
            _movementStartPosition = ConstrainGroundPosition(_root.position);
            _movementStartSimulationTime = movementStartSimulationTime;
            var movementLead = movementLeadOverride ?? (action == TechniqueAction.Attack ? 0.32f : 0.10f);
            _movementEndSimulationTime = Mathf.Max(
                _movementStartSimulationTime + 0.01f,
                scheduledContactTime - movementLead);
            var availableSeconds = _movementEndSimulationTime - _movementStartSimulationTime;
            MaximumSpeed = HorizontalMaximumSpeed(ability);
            _scheduledAction = action;
            _scheduledAbility = ability;
            _attackContactTime = scheduledContactTime;
            if (action == TechniqueAction.Attack)
            {
                ResetAttackCorrectionAccounting();
            }
            _attackMotionForward = _root.forward;
            _movementTargetPosition = Vector3.MoveTowards(
                _movementStartPosition,
                ConstrainGroundPosition(requestedTarget),
                MaximumSpeed * availableSeconds);
            _attackMotionOrigin = _movementTargetPosition;
            ScheduledMovementDistance = Vector3.Distance(_movementStartPosition, _movementTargetPosition);
            MovementShortfall = Vector3.Distance(_movementTargetPosition, requestedTarget);
            _hasScheduledMovement = ScheduledMovementDistance > 0.01f;
            UpdateAttackRouteMaximumSpeed();
        }

        public void ConfigureContinuationMovement(
            Vector3 requestedTakeoff,
            float movementStartSimulationTime,
            float scheduledContactTime,
            PlayerAbilityProfile ability)
        {
            ConfigureScheduledMovement(
                requestedTakeoff,
                movementStartSimulationTime,
                scheduledContactTime,
                TechniqueAction.Attack,
                ability,
                0.38f);
        }

        public void ConfigureAttackApproach(
            AttackApproachPlan approach,
            PlayerAbilityProfile ability,
            float contactTime,
            bool useContinuationTakeoff = false)
        {
            ResetAttackCorrectionAccounting();
            _hasAttackApproach = true;
            _attackContactTime = contactTime;
            _attackJumpLead = 0.38f;
            _attackJumpQuality = approach.JumpQuality;
            var approachStart = _movementTargetPosition;
            var requestedTakeoff = ConstrainGroundPosition(ToUnity(approach.Takeoff));
            if (useContinuationTakeoff)
            {
                _attackTakeoffPosition = approachStart;
                return;
            }

            var takeoffTime = Mathf.Max(_movementEndSimulationTime + 0.01f, contactTime - _attackJumpLead);
            var horizontalMaximumSpeed = HorizontalMaximumSpeed(ability);
            _attackTakeoffPosition = Vector3.MoveTowards(
                approachStart,
                requestedTakeoff,
                horizontalMaximumSpeed * (takeoffTime - _movementEndSimulationTime));
            ScheduledMovementDistance += Vector3.Distance(approachStart, _attackTakeoffPosition);
            MovementShortfall += Vector3.Distance(_attackTakeoffPosition, requestedTakeoff);
            UpdateAttackRouteMaximumSpeed();
        }

        public void ConfigureAttackContact(Vector3 requestedRootPosition, float jumpLead, PlayerAbilityProfile ability)
        {
            _attackJumpLead = jumpLead;
            var horizontalMaximumSpeed = HorizontalMaximumSpeed(ability);
            var takeoffHorizontal = new Vector3(_attackTakeoffPosition.x, 0f, _attackTakeoffPosition.z);
            var requestedHorizontal = new Vector3(requestedRootPosition.x, 0f, requestedRootPosition.z);
            var reachableHorizontal = Vector3.MoveTowards(
                takeoffHorizontal,
                requestedHorizontal,
                horizontalMaximumSpeed * jumpLead);
            _attackContactRootPosition = new Vector3(reachableHorizontal.x, requestedRootPosition.y, reachableHorizontal.z);
            _hasAttackContactRoot = true;
            MovementShortfall += Vector3.Distance(reachableHorizontal, requestedHorizontal);
            UpdateAttackRouteMaximumSpeed();
        }

        public void ConfigureSupportMovement(
            TechniqueAction action,
            Vector3 requestedTarget,
            float movementStartSimulationTime,
            float scheduledContactTime,
            PlayerAbilityProfile ability,
            float blockContactHeight = 0f)
        {
            _supportAction = action;
            _supportStartPosition = ConstrainGroundPosition(_root.position);
            _supportStartSimulationTime = movementStartSimulationTime;
            _supportEndSimulationTime = Mathf.Max(_supportStartSimulationTime + 0.01f, scheduledContactTime - 0.10f);
            MaximumSpeed = HorizontalMaximumSpeed(ability);
            _supportTargetPosition = Vector3.MoveTowards(
                _supportStartPosition,
                ConstrainGroundPosition(requestedTarget),
                MaximumSpeed * (_supportEndSimulationTime - _supportStartSimulationTime));
            _supportBlockContactHeight = blockContactHeight;
            _hasSupportMovement = true;
            ScheduledMovementDistance = Vector3.Distance(_supportStartPosition, _supportTargetPosition);
        }

        public void RetargetSupportMovement(Vector3 requestedTarget, float scheduledContactTime)
        {
            _supportTargetPosition = Vector3.MoveTowards(
                _supportTargetPosition,
                ConstrainGroundPosition(requestedTarget),
                0.549f);
            _supportEndSimulationTime = Mathf.Max(_supportStartSimulationTime + 0.01f, scheduledContactTime - 0.10f);
            ScheduledMovementDistance = Vector3.Distance(_supportStartPosition, _supportTargetPosition);
        }

        public void SetSupportBlockContactHeight(float blockContactHeight)
        {
            _supportBlockContactHeight = blockContactHeight;
        }

        public Vector3 SampleSupport(float simulationTime, float supportContactTime, PlayerAbilityProfile ability)
        {
            var position = EvaluateSupportGroundMovement(simulationTime);
            if (_supportAction == TechniqueAction.Block)
            {
                var takeoffTime = supportContactTime - 0.22f;
                var landingTime = supportContactTime + 0.28f;
                var progress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
                var contactProgress = 0.22f / 0.50f;
                var contactHeight = _supportBlockContactHeight > 0f
                    ? _supportBlockContactHeight
                    : (0.30f + (ability.Jump * 0.20f)) * 4f * contactProgress * (1f - contactProgress);
                position.y += contactHeight * (4f * progress * (1f - progress)) /
                              (4f * contactProgress * (1f - contactProgress));
            }
            return ConstrainToOwnCourt(position);
        }

        public PlayerLocomotionSample Sample(float simulationTime)
        {
            return Sample(simulationTime, -1f);
        }

        public PlayerLocomotionSample Sample(float simulationTime, float elapsedStepSeconds)
        {
            return Sample(simulationTime, elapsedStepSeconds, false);
        }

        public PlayerLocomotionSample Sample(
            float simulationTime,
            float elapsedStepSeconds,
            bool shareAttackAlignmentStepBudget)
        {
            var isFirstLiveSample = float.IsNaN(_lastSampleSimulationTime);
            if (!isFirstLiveSample)
            {
                _lastSampleDeltaSeconds = Mathf.Max(0f, simulationTime - _lastSampleSimulationTime);
            }
            _lastSampleSimulationTime = simulationTime;
            var movementPosition = EvaluateScheduledMovement(simulationTime, out var complete);
            var position = _hasAttackApproach
                ? EvaluatePlannedAttackPosition(simulationTime, movementPosition)
                : _scheduledAction == TechniqueAction.Attack
                    ? EvaluateUnplannedAttackPosition(simulationTime, movementPosition)
                    : movementPosition;
            if (_scheduledAction == TechniqueAction.Attack)
            {
                position += _attackAlignmentOffset;
            }
            // SmoothStep describes the desired route only. The live root travels that
            // route through one arc-length budget, so midpoint easing and vertical jump
            // motion cannot consume more than MaximumSpeed * dt in a single simulation step.
            var isFixedStep = elapsedStepSeconds >= 0f &&
                              _lastSampleDeltaSeconds <= elapsedStepSeconds + .0001f;
            var liveStepSeconds = isFirstLiveSample
                ? Mathf.Max(0f, simulationTime - _movementStartSimulationTime)
                : Mathf.Max(0f, elapsedStepSeconds);
            if (isFixedStep)
            {
                position = Vector3.MoveTowards(
                    _root.position,
                    position,
                    (MaximumSpeed * liveStepSeconds) + 0.000001f);
            }
            if (_scheduledAction == TechniqueAction.Attack)
            {
                if (shareAttackAlignmentStepBudget && isFixedStep)
                {
                    var stepBudget = MaximumSpeed * liveStepSeconds;
                    _remainingAttackStepDistance = Mathf.Max(
                        0f,
                        stepBudget - Vector3.Distance(_root.position, position));
                }
                else
                {
                    _remainingAttackStepDistance = float.PositiveInfinity;
                }
            }
            return new PlayerLocomotionSample(ConstrainToOwnCourt(position), complete);
        }

        public void ApplyLimitedContactAlignment(SimVector3 plannedCenter, SimVector3 actualCenter)
        {
            var requested = plannedCenter - actualCenter;
            var rootIsLegal = ConstrainToOwnCourt(_root.position) == _root.position;
            var requestedPosition = rootIsLegal
                ? ConstrainToOwnCourt(_root.position + ToUnity(requested))
                : ConstrainToOwnCourt(_root.position);
            var applied = ApplyAttackContactCorrection(requestedPosition - _root.position);
            SetRootPosition(_root.position + applied);
        }

        public Vector3 ApplyAttackContactCorrection(Vector3 requested)
        {
            var remaining = Mathf.Max(0f, PrototypePlayerAgent.NetClearance - _appliedAttackCorrection);
            var applied = Vector3.ClampMagnitude(requested, remaining);
            _appliedAttackCorrection += applied.magnitude;
            MaximumAppliedContactCorrection = Mathf.Max(MaximumAppliedContactCorrection, _appliedAttackCorrection);
            MovementShortfall += Mathf.Max(0f, requested.magnitude - applied.magnitude);
            return applied;
        }

        public Vector3 ApplyAttackContactAlignment(Vector3 requested)
        {
            return ApplyAttackContactAlignment(requested, _lastSampleDeltaSeconds);
        }

        public Vector3 ApplyAttackContactAlignment(Vector3 requested, float elapsedStepSeconds)
        {
            var desiredOffset = Vector3.ClampMagnitude(
                _attackAlignmentOffset + requested,
                PrototypePlayerAgent.NetClearance);
            var maximumStep = Mathf.Min(
                MaximumSpeed * Mathf.Max(0f, elapsedStepSeconds),
                _remainingAttackStepDistance);
            var nextOffset = Vector3.MoveTowards(
                _attackAlignmentOffset,
                desiredOffset,
                maximumStep);
            var requestedStep = nextOffset - _attackAlignmentOffset;
            var constrainedStep = ConstrainToOwnCourt(_root.position + requestedStep) - _root.position;
            _attackAlignmentOffset += constrainedStep;
            _remainingAttackStepDistance = Mathf.Max(
                0f,
                _remainingAttackStepDistance - constrainedStep.magnitude);
            _appliedAttackCorrection = _attackAlignmentOffset.magnitude;
            MaximumAppliedContactCorrection = Mathf.Max(
                MaximumAppliedContactCorrection,
                _attackAlignmentOffset.magnitude);
            MovementShortfall += Mathf.Max(0f, requested.magnitude - constrainedStep.magnitude);
            SetRootPosition(_root.position + constrainedStep);
            return constrainedStep;
        }

        internal void ApplyContactAlignment(
            SimVector3 plannedCenter,
            SimVector3 actualCenter,
            TechniqueAction action,
            bool controlledHandling,
            ActionPhase phase,
            float phaseProgress,
            float elapsedStepSeconds)
        {
            if (phase != ActionPhase.Power && phase != ActionPhase.Contact) return;
            var maximumCorrection = controlledHandling ? .70f : action switch
            {
                TechniqueAction.Attack => PrototypePlayerAgent.NetClearance,
                TechniqueAction.Set => .30f,
                _ => .16f
            };
            var correction = plannedCenter - actualCenter;
            if (correction.Magnitude > maximumCorrection)
                correction = correction.Normalized * (maximumCorrection - .0001f);
            var requested = ToUnity(correction);
            if (!controlledHandling && action == TechniqueAction.Attack)
            {
                if (phase == ActionPhase.Contact) ApplyAttackContactAlignment(requested, elapsedStepSeconds);
                return;
            }
            if (phase == ActionPhase.Power) requested *= Mathf.SmoothStep(0f, 1f, phaseProgress);
            SetRootPosition(_root.position + requested);
        }

        private void ResetAttackCorrectionAccounting()
        {
            _appliedAttackCorrection = 0f;
            _attackAlignmentOffset = Vector3.zero;
            _remainingAttackStepDistance = float.PositiveInfinity;
            MaximumAppliedContactCorrection = 0f;
        }

        private void ClearAttackPlanState()
        {
            _hasAttackApproach = false;
            _hasAttackContactRoot = false;
            _attackTakeoffPosition = default;
            _attackContactRootPosition = default;
            _attackJumpLead = 0f;
            _attackJumpQuality = 0f;
        }

        private float HorizontalMaximumSpeed(PlayerAbilityProfile ability)
        {
            return _moveSpeed * (0.65f + (ability.Mobility * 0.5f));
        }

        // The horizontal reach calculation deliberately uses mobility's average speed.
        // MaximumSpeed is instead the instantaneous 3D route bound consumed by live root steps.
        private void UpdateAttackRouteMaximumSpeed()
        {
            var maximum = HorizontalMaximumSpeed(_scheduledAbility);
            var scheduledPeak = SmoothStepPeakSpeed(
                Vector3.Distance(_movementStartPosition, _movementTargetPosition),
                _movementEndSimulationTime - _movementStartSimulationTime);
            maximum = Mathf.Max(maximum, scheduledPeak);

            if (_scheduledAction != TechniqueAction.Attack)
            {
                MaximumSpeed = maximum;
                return;
            }

            var takeoffTime = Mathf.Max(_movementEndSimulationTime + .01f, _attackContactTime - _attackJumpLead);
            if (_hasAttackApproach)
            {
                maximum = Mathf.Max(maximum, SmoothStepPeakSpeed(
                    Vector3.Distance(_movementTargetPosition, _attackTakeoffPosition),
                    takeoffTime - _movementEndSimulationTime) + AttackAlignmentSpeedAllowance);
            }

            if (_hasAttackContactRoot)
            {
                maximum = Mathf.Max(maximum, SmoothStepPeakSpeed(
                    Vector3.Distance(_attackTakeoffPosition, _attackContactRootPosition),
                    _attackContactTime - takeoffTime) + AttackAlignmentSpeedAllowance);
                maximum = Mathf.Max(maximum, SmoothStepPeakSpeed(
                    Mathf.Abs(_attackContactRootPosition.y), .45f) + AttackAlignmentSpeedAllowance);
            }
            else
            {
                var jumpHeight = 0.72f + (_scheduledAbility.Jump * .5f);
                var verticalPeak = (4f * jumpHeight) / .83f;
                var approachPeak = SmoothStepPeakSpeed(
                    .45f + (_scheduledAbility.Mobility * .35f), .55f);
                maximum = Mathf.Max(maximum,
                    scheduledPeak + approachPeak + verticalPeak + AttackAlignmentSpeedAllowance);
            }

            MaximumSpeed = maximum;
        }

        private static float SmoothStepPeakSpeed(float distance, float duration)
        {
            return duration > .000001f ? (1.5f * distance) / duration : 0f;
        }

        public void SetRootPosition(Vector3 position)
        {
            _root.position = ConstrainToOwnCourt(position);
        }

        public IEnumerator MoveTo(Vector3 destination, PlayerAbilityProfile ability)
        {
            destination = ConstrainGroundPosition(destination);
            var speed = 0f;
            const float acceleration = 24f;
            while ((_root.position - destination).sqrMagnitude > 0.01f)
            {
                var distance = Vector3.Distance(_root.position, destination);
                var brakingSpeed = Mathf.Sqrt(2f * acceleration * distance);
                MaximumSpeed = _moveSpeed * (0.65f + (ability.Mobility * 0.5f));
                var targetSpeed = Mathf.Min(MaximumSpeed, brakingSpeed);
                speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * Time.deltaTime);
                _root.position = ConstrainToOwnCourt(
                    Vector3.MoveTowards(_root.position, destination, speed * Time.deltaTime));
                yield return null;
            }

            _root.position = ConstrainToOwnCourt(destination);
        }

        public Vector3 ConstrainGroundPosition(Vector3 position)
        {
            position.y = 0f;
            return ConstrainToOwnCourt(position);
        }

        public Vector3 ConstrainToOwnCourt(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -CourtBuilder.HalfWidth + PrototypePlayerAgent.BoundaryClearance,
                CourtBuilder.HalfWidth - PrototypePlayerAgent.BoundaryClearance);
            position.z = _team == TeamId.Blue
                ? Mathf.Clamp(position.z, -_courtHalfLength + PrototypePlayerAgent.BoundaryClearance,
                    -PrototypePlayerAgent.NetClearance)
                : Mathf.Clamp(position.z, PrototypePlayerAgent.NetClearance,
                    _courtHalfLength - PrototypePlayerAgent.BoundaryClearance);
            return position;
        }

        private Vector3 EvaluateScheduledMovement(float simulationTime, out bool complete)
        {
            if (!_hasScheduledMovement || simulationTime >= _movementEndSimulationTime)
            {
                complete = true;
                return _movementTargetPosition;
            }
            if (simulationTime <= _movementStartSimulationTime)
            {
                complete = false;
                return _movementStartPosition;
            }

            var progress = Mathf.InverseLerp(_movementStartSimulationTime, _movementEndSimulationTime, simulationTime);
            progress = progress * progress * (3f - (2f * progress));
            complete = progress >= 1f;
            return Vector3.Lerp(_movementStartPosition, _movementTargetPosition, progress);
        }

        private Vector3 EvaluateSupportGroundMovement(float simulationTime)
        {
            if (!_hasSupportMovement || simulationTime >= _supportEndSimulationTime) return _supportTargetPosition;
            if (simulationTime <= _supportStartSimulationTime) return _supportStartPosition;
            var progress = Mathf.InverseLerp(_supportStartSimulationTime, _supportEndSimulationTime, simulationTime);
            progress = progress * progress * (3f - (2f * progress));
            return Vector3.Lerp(_supportStartPosition, _supportTargetPosition, progress);
        }

        private Vector3 EvaluatePlannedAttackPosition(float simulationTime, Vector3 movementPosition)
        {
            var takeoffTime = Mathf.Max(_movementEndSimulationTime + 0.01f, _attackContactTime - _attackJumpLead);
            var approachProgress = Mathf.InverseLerp(_movementEndSimulationTime, takeoffTime, simulationTime);
            approachProgress = approachProgress * approachProgress * (3f - (2f * approachProgress));
            var position = Vector3.Lerp(movementPosition, _attackTakeoffPosition, approachProgress);
            if (_hasAttackContactRoot)
            {
                if (simulationTime < takeoffTime) return position;
                if (simulationTime <= _attackContactTime)
                {
                    var ascent = Mathf.InverseLerp(takeoffTime, _attackContactTime, simulationTime);
                    ascent = ascent * ascent * (3f - (2f * ascent));
                    return Vector3.Lerp(_attackTakeoffPosition, _attackContactRootPosition, ascent);
                }
                var descent = Mathf.Clamp01((simulationTime - _attackContactTime) / 0.45f);
                descent = descent * descent * (3f - (2f * descent));
                var landed = _attackContactRootPosition;
                landed.y = 0f;
                return Vector3.Lerp(_attackContactRootPosition, landed, descent);
            }

            var landingTime = _attackContactTime + 0.45f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            position.y = (0.72f + (PlayerAbilityProfile.Default.Jump * 0.5f)) * _attackJumpQuality *
                         4f * jumpProgress * (1f - jumpProgress);
            return position;
        }

        private Vector3 EvaluateUnplannedAttackPosition(float simulationTime, Vector3 movementPosition)
        {
            var takeoffTime = _attackContactTime - 0.38f;
            var landingTime = _attackContactTime + 0.45f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var jumpHeight = (0.72f + (_scheduledAbility.Jump * 0.5f)) * 4f * jumpProgress * (1f - jumpProgress);
            var approachStart = _attackContactTime - 0.72f;
            var approachProgress = Mathf.Clamp01((simulationTime - approachStart) / 0.55f);
            approachProgress = approachProgress * approachProgress * (3f - (2f * approachProgress));
            var approachDistance = 0.45f + (_scheduledAbility.Mobility * 0.35f);
            var position = movementPosition + (_attackMotionForward * approachDistance * approachProgress);
            position.y = _attackMotionOrigin.y + jumpHeight;
            return position;
        }

        private static Vector3 ToUnity(SimVector3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}

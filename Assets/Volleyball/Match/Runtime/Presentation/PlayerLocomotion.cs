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
    /// Owns player root motion.  Inputs are immutable plans; timeline ownership remains with the facade.
    /// </summary>
    public sealed class PlayerLocomotion
    {
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
        private AttackApproachPlan _attackApproach;
        private Vector3 _attackTakeoffPosition;
        private Vector3 _attackContactRootPosition;
        private bool _hasAttackContactRoot;
        private float _attackContactTime;
        private float _attackJumpLead;
        private float _attackJumpQuality;

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

        public Vector3 ScheduledMovementTarget => _movementTargetPosition;

        public bool HasScheduledMovement => _hasScheduledMovement;

        public void ConfigureScheduledMovement(
            Vector3 requestedTarget,
            float movementStartSimulationTime,
            float scheduledContactTime,
            TechniqueAction action,
            PlayerAbilityProfile ability,
            float? movementLeadOverride = null)
        {
            _movementStartPosition = ConstrainGroundPosition(_root.position);
            _movementStartSimulationTime = movementStartSimulationTime;
            var movementLead = movementLeadOverride ?? (action == TechniqueAction.Attack ? 0.32f : 0.10f);
            _movementEndSimulationTime = Mathf.Max(
                _movementStartSimulationTime + 0.01f,
                scheduledContactTime - movementLead);
            var availableSeconds = _movementEndSimulationTime - _movementStartSimulationTime;
            MaximumSpeed = _moveSpeed * (0.65f + (ability.Mobility * 0.5f));
            _movementTargetPosition = Vector3.MoveTowards(
                _movementStartPosition,
                ConstrainGroundPosition(requestedTarget),
                MaximumSpeed * availableSeconds);
            ScheduledMovementDistance = Vector3.Distance(_movementStartPosition, _movementTargetPosition);
            MovementShortfall = Vector3.Distance(_movementTargetPosition, ConstrainGroundPosition(requestedTarget));
            _hasScheduledMovement = ScheduledMovementDistance > 0.01f;
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
            _hasAttackApproach = true;
            _attackApproach = approach;
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
            MaximumSpeed = _moveSpeed * (0.65f + (ability.Mobility * 0.5f));
            _attackTakeoffPosition = Vector3.MoveTowards(
                approachStart,
                requestedTakeoff,
                MaximumSpeed * (takeoffTime - _movementEndSimulationTime));
            ScheduledMovementDistance += Vector3.Distance(approachStart, _attackTakeoffPosition);
            MovementShortfall += Vector3.Distance(_attackTakeoffPosition, requestedTakeoff);
        }

        public void ConfigureAttackContact(Vector3 requestedRootPosition, float jumpLead, PlayerAbilityProfile ability)
        {
            _attackJumpLead = jumpLead;
            MaximumSpeed = _moveSpeed * (0.65f + (ability.Mobility * 0.5f));
            var takeoffHorizontal = new Vector3(_attackTakeoffPosition.x, 0f, _attackTakeoffPosition.z);
            var requestedHorizontal = new Vector3(requestedRootPosition.x, 0f, requestedRootPosition.z);
            var reachableHorizontal = Vector3.MoveTowards(
                takeoffHorizontal,
                requestedHorizontal,
                MaximumSpeed * jumpLead);
            _attackContactRootPosition = new Vector3(reachableHorizontal.x, requestedRootPosition.y, reachableHorizontal.z);
            _hasAttackContactRoot = true;
            MovementShortfall += Vector3.Distance(reachableHorizontal, requestedHorizontal);
        }

        public PlayerLocomotionSample Sample(float simulationTime)
        {
            var movementPosition = EvaluateScheduledMovement(simulationTime, out var complete);
            var position = _hasAttackApproach
                ? EvaluatePlannedAttackPosition(simulationTime, movementPosition)
                : movementPosition;
            return new PlayerLocomotionSample(ConstrainToOwnCourt(position), complete);
        }

        public void ApplyLimitedContactAlignment(SimVector3 plannedCenter, SimVector3 actualCenter)
        {
            var requested = plannedCenter - actualCenter;
            var applied = Vector3.ClampMagnitude(ToUnity(requested), PrototypePlayerAgent.NetClearance);
            _root.position += applied;
            MaximumAppliedContactCorrection = Mathf.Max(MaximumAppliedContactCorrection, applied.magnitude);
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

        private static Vector3 ToUnity(SimVector3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}

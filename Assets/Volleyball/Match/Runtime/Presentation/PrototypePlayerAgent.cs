using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation
{
    public readonly struct ObservedAttackTakeoff
    {
        public ObservedAttackTakeoff(SimVector3 point, float simulationTime)
        {
            if (!point.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }
            if (float.IsNaN(simulationTime) || float.IsInfinity(simulationTime))
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTime));
            }

            Point = point;
            SimulationTime = simulationTime;
        }

        public SimVector3 Point { get; }

        public float SimulationTime { get; }
    }

    public sealed class PrototypePlayerAgent : MonoBehaviour, IBallContactSource
    {
        public const float NetClearance = 0.18f;
        public const float BoundaryClearance = 0.25f;

        [SerializeField]
        private float _moveSpeed = 7f;

        public PlayerId Id { get; private set; }

        public StablePlayerId StableId { get; private set; }

        public StickFigureRig Rig => _presentation == null ? null : _presentation.Rig;

        public PlayerAbilityProfile Ability { get; private set; }

        public PlayerContactSurfaces ContactSurfaces { get; private set; }

        public SetTechniqueStyle CurrentSetStyle => _setDecision.ExecutedStyle;

        public SetTechniqueStyle RequestedSetStyle => _setDecision.RequestedStyle;

        public SimVector3 PreparedForward { get; private set; }

        public Vector3 ScheduledMovementTarget => _hasPhysicalBlockContact || _actionTimelineState.HasSupportAction
            ? _supportTargetPosition
            : _movementTargetPosition;

        public string ReplayScheduledAction => _hasPhysicalBlockContact
            ? TechniqueAction.Block.ToString()
            : _actionTimelineState.HasSupportAction
                ? _actionTimelineState.SupportAction.ToString()
                : _actionTimelineState.HasScheduledContact
                    ? _isControlledHandling ? "Handling" : _scheduledAction.ToString()
                    : "Ready";

        public float MovementShortfall { get; private set; }

        public float ScheduledMovementDistance { get; private set; }

        public int PhysicalBlockContactAssignments { get; private set; }

        public float BlockRetargetDistance { get; private set; }

        public float BlockRetargetTimeShift { get; private set; }

        public float PhysicalBlockContactTime { get; private set; }

        public float MaximumAppliedContactCorrection { get; private set; }

        public SimVector3 LastScheduledSurfaceCenter { get; private set; }

        public SimVector3 LastScheduledSurfaceNormal { get; private set; }

        public ExecutionEnvelopeV4 ScheduledExecutionEnvelopeV4 => _techniqueExecutor.ExecutionEnvelope;

        public ExecutionSampleV4 ScheduledExecutionSampleV4 => _techniqueExecutor.ExecutionSample;

        public ExecutionSampleClassificationV4 ScheduledExecutionClassificationV4 =>
            _techniqueExecutor.ExecutionClassification;

        public BallTrajectoryPredictionArtifactV4
            ScheduledTrajectoryPredictionArtifactV4 => _techniqueExecutor.TrajectoryArtifact;

        internal float MinimumActiveSurfacePlanError { get; private set; }

        public bool IsWithinOwnCourt => IsWithinOwnCourtBounds(transform.position);

        public bool EmergencyReceiveWindowEnabled => _actionTimelineState.HasEmergencyReceiveWindow;

        public event Action<PrototypePlayerAgent, TechniqueAction> SupportActionActivated;

        public bool TryGetObservedAttackTakeoff(out ObservedAttackTakeoff takeoff)
        {
            takeoff = _observedAttackTakeoff;
            return _hasObservedAttackTakeoff;
        }

        private readonly PlayerActionTimeline _actionTimelineState = new PlayerActionTimeline();
        private readonly PlayerTechniqueExecutor _techniqueExecutor = new PlayerTechniqueExecutor();
        private PlayerPresentation _presentation;
        private TechniqueAction _scheduledAction;
        private SkillExecutionError _executionError;
        private SimVector3 _targetVelocity;
        private int _contactGroupId;
        private Vector3 _motionOrigin;
        private Vector3 _motionForward;
        private SimVector3 _plannedContactCenter;
        private bool _hasPlannedContactCenter;
        private SetTechniqueDecision _setDecision;
        private Vector3 _movementStartPosition;
        private Vector3 _movementTargetPosition;
        private float _movementStartSimulationTime;
        private float _movementEndSimulationTime;
        private bool _hasScheduledMovement;
        private bool _isMovingThisStep;
        private Vector3 _supportStartPosition;
        private Vector3 _supportTargetPosition;
        private float _supportStartSimulationTime;
        private float _supportEndSimulationTime;
        private bool _supportActionActivated;
        private bool _hasPhysicalBlockContact;
        private SimVector3 _physicalBlockTargetVelocity;
        private int _physicalBlockContactGroupId;
        private float _physicalBlockContactRootHeight;
        private bool _hasAttackApproach;
        private AttackApproachPlan _attackApproach;
        private bool _hasAttackContactPlan;
        private AttackContactPlan _attackContactPlan;
        private Vector3 _attackTakeoffPosition;
        private Vector3 _attackContactRootPosition;
        private ObservedAttackTakeoff _observedAttackTakeoff;
        private bool _hasObservedAttackTakeoff;
        private bool _continueAttackPreparation;
        private bool _physicalBlockActivationLogged;
        private BlockArmContactVolumes _blockArmContactVolumes;
        private bool _isControlledHandling;
        private float _courtHalfLength = CourtBuilder.HalfLength;

        public void Initialize(PlayerId id, Color color, string jerseyNumber)
        {
            var prefix = id.Team == TeamId.Blue ? "home-" : "away-";
            Initialize(
                id,
                new StablePlayerId(prefix + id.Role.ToString().ToLowerInvariant()),
                color,
                jerseyNumber);
        }

        public void Initialize(
            PlayerId id,
            StablePlayerId stableId,
            Color color,
            string jerseyNumber)
        {
            Id = id;
            StableId = stableId;
            _presentation = new PlayerPresentation(transform, color, jerseyNumber);
            Ability = PlayerAbilityProfile.Default;
            ContactSurfaces = new PlayerContactSurfaces(Rig, transform);
            _blockArmContactVolumes = new BlockArmContactVolumes(Rig);
        }

        public void SetAbility(PlayerAbilityProfile ability)
        {
            Ability = ability;
        }

        public void SetCourtHalfLength(float courtHalfLength)
        {
            if (float.IsNaN(courtHalfLength) || float.IsInfinity(courtHalfLength) || courtHalfLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(courtHalfLength));
            }

            _courtHalfLength = courtHalfLength;
        }

        public void ScheduleContact(
            TechniqueAction action,
            float scheduledSimulationTime,
            ExecutionSampleClassificationV4 executionClassification,
            SkillExecutionError executionError,
            int contactGroupId,
            SimVector3? plannedContactCenter = null,
            bool emergencyOneHand = false,
            Vector3? movementTarget = null,
            float movementStartSimulationTime = 0f,
            AttackApproachPlan? attackApproach = null,
            AttackContactPlan? attackContactPlan = null,
            SetRoute? normalSetRoute = null,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact = null)
        {
            _techniqueExecutor.ScheduleV4(
                action,
                scheduledSimulationTime,
                executionClassification,
                executionError,
                contactGroupId,
                plannedContactCenter,
                emergencyOneHand,
                movementTarget,
                movementStartSimulationTime,
                attackApproach,
                attackContactPlan,
                normalSetRoute,
                trajectoryArtifact);
            var command = _techniqueExecutor.ExecutionCommand;
            ScheduleContactCore(
                command.Action,
                command.ScheduledSimulationTime,
                command.TargetVelocity,
                command.Error,
                command.ContactGroupId,
                command.PlannedContactCenter,
                command.EmergencyOneHand,
                command.MovementTarget,
                command.MovementStartSimulationTime,
                command.AttackApproach,
                command.AttackContactPlan,
                command.NormalSetRoute,
                applyLegacyAttackPowerScale: false);
        }

        // Compatibility path for legacy 3v3 callers. Formal V4 scheduling is
        // routed through PlayerTechniqueExecutor.ScheduleV4 above.
        public void ScheduleContact(
            TechniqueAction action,
            float scheduledSimulationTime,
            SimVector3 targetVelocity,
            SkillExecutionError executionError,
            int contactGroupId,
            SimVector3? plannedContactCenter = null,
            bool emergencyOneHand = false,
            Vector3? movementTarget = null,
            float movementStartSimulationTime = 0f,
            AttackApproachPlan? attackApproach = null,
            AttackContactPlan? attackContactPlan = null,
            SetRoute? normalSetRoute = null)
        {
            _techniqueExecutor.Clear();
            ScheduleContactCore(
                action,
                scheduledSimulationTime,
                targetVelocity,
                executionError,
                contactGroupId,
                plannedContactCenter,
                emergencyOneHand,
                movementTarget,
                movementStartSimulationTime,
                attackApproach,
                attackContactPlan,
                normalSetRoute,
                applyLegacyAttackPowerScale: true);
        }

        private void ScheduleContactCore(
            TechniqueAction action,
            float scheduledSimulationTime,
            SimVector3 targetVelocity,
            SkillExecutionError executionError,
            int contactGroupId,
            SimVector3? plannedContactCenter,
            bool emergencyOneHand,
            Vector3? movementTarget,
            float movementStartSimulationTime,
            AttackApproachPlan? attackApproach,
            AttackContactPlan? attackContactPlan,
            SetRoute? normalSetRoute,
            bool applyLegacyAttackPowerScale)
        {
            if (attackApproach.HasValue && action != TechniqueAction.Attack)
            {
                throw new ArgumentException("Only attack contacts may include an approach plan.", nameof(attackApproach));
            }

            if (attackContactPlan.HasValue && action != TechniqueAction.Attack)
            {
                throw new ArgumentException("Only attack contacts may include a contact plan.", nameof(attackContactPlan));
            }

            if (attackContactPlan.HasValue && !attackApproach.HasValue)
            {
                throw new ArgumentException("A contact plan requires an attack approach.", nameof(attackApproach));
            }

            if (attackContactPlan.HasValue && attackApproach.HasValue &&
                !attackContactPlan.Value.Takeoff.Equals(attackApproach.Value.Takeoff))
            {
                throw new ArgumentException("Attack approach and contact plan must use the same takeoff.", nameof(attackContactPlan));
            }

            if (attackContactPlan.HasValue)
            {
                attackContactPlan.Value.Validate();
            }

            DisableBlockContactWindow();
            _scheduledAction = action;
            _observedAttackTakeoff = default;
            _hasObservedAttackTakeoff = false;
            var powerScale = applyLegacyAttackPowerScale && action == TechniqueAction.Attack
                ? 0.90f + (Ability.AttackPowerCapacity * 0.10f)
                : 1f;
            _targetVelocity = (targetVelocity * powerScale) + executionError.TargetVelocityError;
            if (action == TechniqueAction.Set)
            {
                var worldTarget = new Vector3(
                    _targetVelocity.X,
                    _targetVelocity.Y,
                    _targetVelocity.Z);
                var localTarget = transform.InverseTransformDirection(worldTarget);
                _setDecision = normalSetRoute.HasValue && !emergencyOneHand
                    ? SetTechniqueSelector.SelectNormal(normalSetRoute.Value, Ability.SetTechnique)
                    : SetTechniqueSelector.SelectEmergency(
                        new SimVector3(localTarget.x, localTarget.y, localTarget.z),
                        Ability.SetTechnique,
                        emergencyOneHand);
            }
            _executionError = executionError;
            _contactGroupId = contactGroupId;
            _actionTimelineState.ScheduleContact(
                action,
                scheduledSimulationTime,
                executionError.ContactTimingError);
            _hasAttackApproach = attackApproach.HasValue;
            _attackApproach = attackApproach.GetValueOrDefault();
            _hasAttackContactPlan = attackContactPlan.HasValue;
            _attackContactPlan = attackContactPlan.GetValueOrDefault();
            var continuePreparedAttack = attackApproach.HasValue &&
                                         (_continueAttackPreparation ||
                                          (_actionTimelineState.HasSupportAction &&
                                           _actionTimelineState.SupportAction == TechniqueAction.Attack));
            if (continuePreparedAttack)
            {
                ConfigureContinuationMovement(
                    ToUnity(attackApproach.Value.Takeoff),
                    _actionTimelineState.ContactTimeline.ActualContactTime);
            }
            else
            {
                var requestedMovementTarget = attackApproach.HasValue
                    ? ToUnity(attackApproach.Value.ApproachStart)
                    : movementTarget.GetValueOrDefault(transform.position);
                ConfigureScheduledMovement(
                    requestedMovementTarget,
                    movementStartSimulationTime + executionError.ReactionDelay,
                    action == TechniqueAction.Attack
                        ? _actionTimelineState.ContactTimeline.ActualContactTime
                        : scheduledSimulationTime,
                    action,
                    attackApproach.HasValue ? 0.72f : (float?)null);
            }
            if (attackApproach.HasValue)
            {
                ConfigureAttackApproach(attackApproach.Value, continuePreparedAttack);
            }
            _continueAttackPreparation = false;
            _motionOrigin = _movementTargetPosition;
            _motionForward = transform.forward;
            var authoritativeContactCenter = attackContactPlan?.ContactCenter ?? plannedContactCenter;
            _hasPlannedContactCenter = authoritativeContactCenter.HasValue;
            _plannedContactCenter = authoritativeContactCenter.GetValueOrDefault();
            MinimumActiveSurfacePlanError = float.PositiveInfinity;
            _actionTimelineState.DisableSupport();
            _supportActionActivated = false;
        }

        public void ScheduleControlledHandlingContact(
            float scheduledSimulationTime,
            ExecutionSampleClassificationV4 executionClassification,
            SkillExecutionError executionError,
            int contactGroupId,
            AttackApproachPlan attackApproach,
            AttackContactPlan attackContactPlan,
            float movementStartSimulationTime)
        {
            if (attackContactPlan.Outcome != AttackContactOutcome.Handling)
            {
                throw new ArgumentException(
                    "Controlled handling requires a handling contact plan.",
                    nameof(attackContactPlan));
            }

            _techniqueExecutor.ScheduleV4(
                TechniqueAction.Attack,
                scheduledSimulationTime,
                executionClassification,
                executionError,
                contactGroupId,
                attackContactPlan.ContactCenter,
                movementTarget: ToUnity(attackApproach.ApproachStart),
                movementStartSimulationTime: movementStartSimulationTime,
                attackApproach: attackApproach,
                attackContactPlan: attackContactPlan,
                controlledHandling: true);
            var command = _techniqueExecutor.ExecutionCommand;
            ScheduleContactCore(
                command.Action,
                command.ScheduledSimulationTime,
                command.TargetVelocity,
                command.Error,
                command.ContactGroupId,
                command.PlannedContactCenter,
                command.EmergencyOneHand,
                command.MovementTarget,
                command.MovementStartSimulationTime,
                command.AttackApproach,
                command.AttackContactPlan,
                command.NormalSetRoute,
                applyLegacyAttackPowerScale: false);
            ConfigureControlledHandling(attackContactPlan);
        }

        // Compatibility path for legacy 3v3 controlled handling callers.
        public void ScheduleControlledHandlingContact(
            float scheduledSimulationTime,
            SimVector3 targetVelocity,
            SkillExecutionError executionError,
            int contactGroupId,
            AttackApproachPlan attackApproach,
            AttackContactPlan attackContactPlan,
            float movementStartSimulationTime)
        {
            if (attackContactPlan.Outcome != AttackContactOutcome.Handling)
            {
                throw new ArgumentException(
                    "Controlled handling requires a handling contact plan.",
                    nameof(attackContactPlan));
            }

            ScheduleContact(
                TechniqueAction.Attack,
                scheduledSimulationTime,
                targetVelocity,
                executionError,
                contactGroupId,
                attackContactPlan.ContactCenter,
                movementTarget: ToUnity(attackApproach.ApproachStart),
                movementStartSimulationTime: movementStartSimulationTime,
                attackApproach: attackApproach,
                attackContactPlan: attackContactPlan);
            _targetVelocity = targetVelocity + executionError.TargetVelocityError;
            ConfigureControlledHandling(attackContactPlan);
        }

        private void ConfigureControlledHandling(AttackContactPlan attackContactPlan)
        {
            _isControlledHandling = true;
            _attackContactRootPosition = ContactRootPosition(attackContactPlan, TechniqueAction.Set);
        }

        public void ContinueAttackPreparation(
            AttackApproachPlan approach,
            AttackContactPlan contactPlan,
            float actualContactTime)
        {
            if (!contactPlan.Takeoff.Equals(approach.Takeoff))
            {
                throw new ArgumentException(
                    "Attack approach and contact plan must use the same takeoff.",
                    nameof(contactPlan));
            }

            contactPlan.Validate();
            _continueAttackPreparation = true;
            ConfigureContinuationMovement(ToUnity(approach.Takeoff), actualContactTime);
        }

        public void CancelScheduledContact()
        {
            _hasPlannedContactCenter = false;
            _continueAttackPreparation = false;
            _actionTimelineState.CancelContact();
            _actionTimelineState.DisableSupport();
            _supportActionActivated = false;
            _hasAttackApproach = false;
            _hasAttackContactPlan = false;
            _isControlledHandling = false;
            DisableBlockContactWindow();
            DisableEmergencyReceiveWindow();
        }

        public void EnableEmergencyReceiveWindow(
            float startSimulationTime,
            float endSimulationTime,
            SimVector3 targetVelocity,
            int contactGroupId)
        {
            _actionTimelineState.EnableEmergencyReceive(
                startSimulationTime,
                endSimulationTime,
                targetVelocity,
                contactGroupId);
        }

        public void DisableEmergencyReceiveWindow()
        {
            _actionTimelineState.DisableEmergencyReceive();
        }

        public void ScheduleSupportAction(
            TechniqueAction action,
            float scheduledSimulationTime,
            Vector3 movementTarget,
            float movementStartSimulationTime)
        {
            if (action != TechniqueAction.Block && action != TechniqueAction.Receive)
            {
                throw new System.ArgumentException("Only block and receive support actions are supported.", nameof(action));
            }

            DisableBlockContactWindow();
            ConfigureSupportAction(action, scheduledSimulationTime, movementTarget, movementStartSimulationTime);
            _supportActionActivated = false;
        }

        public void ScheduleAttackPreparation(
            float scheduledSetContactTime,
            Vector3 approachStart,
            float movementStartSimulationTime)
        {
            CancelScheduledContact();
            ConfigureSupportAction(
                TechniqueAction.Attack,
                scheduledSetContactTime,
                approachStart,
                movementStartSimulationTime);
            _supportActionActivated = false;
        }

        public void ScheduleSetPreparation(
            float scheduledReceiveContactTime,
            Vector3 settingPosition,
            float movementStartSimulationTime)
        {
            CancelScheduledContact();
            ConfigureSupportAction(
                TechniqueAction.Set,
                scheduledReceiveContactTime,
                settingPosition,
                movementStartSimulationTime);
            _supportActionActivated = false;
        }

        public void ScheduleBlockContact(
            float scheduledSimulationTime,
            Vector3 movementTarget,
            float movementStartSimulationTime,
            SimVector3 targetVelocity,
            int contactGroupId)
        {
            if (!targetVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVelocity));
            }

            CancelScheduledContact();
            transform.forward = Id.Team == TeamId.Blue ? Vector3.forward : Vector3.back;
            ConfigureSupportAction(
                TechniqueAction.Block,
                scheduledSimulationTime,
                movementTarget,
                movementStartSimulationTime,
                isSupportAction: false);
            _physicalBlockContactRootHeight = movementTarget.y > 0f
                ? Mathf.Min(MaximumBlockContactRootHeight(), movementTarget.y)
                : MaximumBlockContactRootHeight();
            _physicalBlockTargetVelocity = targetVelocity;
            _physicalBlockContactGroupId = contactGroupId;
            _hasPhysicalBlockContact = true;
            PhysicalBlockContactTime = scheduledSimulationTime;
            _physicalBlockActivationLogged = false;
            PhysicalBlockContactAssignments++;
            BlockRetargetDistance = 0f;
            BlockRetargetTimeShift = 0f;
            ScheduledMovementDistance = Vector3.Distance(_supportStartPosition, _supportTargetPosition);
        }

        public bool RetargetBlockContact(
            float scheduledSimulationTime,
            Vector3 movementTarget,
            SimVector3 targetVelocity)
        {
            if (!_hasPhysicalBlockContact)
            {
                return false;
            }

            if (!targetVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVelocity));
            }

            var requestedShift = scheduledSimulationTime - _actionTimelineState.SupportTimeline.ScheduledContactTime;
            var appliedShift = Mathf.Clamp(requestedShift, -0.12f, 0.12f);
            var adjustedContactTime = _actionTimelineState.SupportTimeline.ScheduledContactTime + appliedShift;
            _actionTimelineState.ScheduleBlock(adjustedContactTime);
            PhysicalBlockContactTime = adjustedContactTime;
            BlockRetargetTimeShift = Mathf.Abs(appliedShift);

            var previousTarget = _supportTargetPosition;
            _supportTargetPosition = Vector3.MoveTowards(
                previousTarget,
                ConstrainGroundPosition(movementTarget),
                0.549f);
            _physicalBlockContactRootHeight = movementTarget.y > 0f
                ? Mathf.Min(MaximumBlockContactRootHeight(), movementTarget.y)
                : MaximumBlockContactRootHeight();
            BlockRetargetDistance = Vector3.Distance(previousTarget, _supportTargetPosition);
            _supportEndSimulationTime = Mathf.Max(
                _supportStartSimulationTime + 0.01f,
                adjustedContactTime - 0.10f);
            _physicalBlockTargetVelocity = targetVelocity;
            ScheduledMovementDistance = Vector3.Distance(_supportStartPosition, _supportTargetPosition);
            return true;
        }

        public void DisableBlockContactWindow()
        {
            _hasPhysicalBlockContact = false;
            if (!_actionTimelineState.HasSupportAction)
            {
                _actionTimelineState.DisableSupport();
            }
        }

        private void ConfigureSupportAction(
            TechniqueAction action,
            float scheduledSimulationTime,
            Vector3 movementTarget,
            float movementStartSimulationTime,
            bool isSupportAction = true)
        {
            if (isSupportAction)
            {
                _actionTimelineState.ScheduleSupport(action, scheduledSimulationTime);
            }
            else
            {
                _actionTimelineState.ScheduleBlock(scheduledSimulationTime);
            }
            _supportStartPosition = ConstrainGroundPosition(transform.position);
            _supportStartSimulationTime = movementStartSimulationTime;
            _supportEndSimulationTime = Mathf.Max(
                _supportStartSimulationTime + 0.01f,
                scheduledSimulationTime - 0.10f);
            var availableSeconds = _supportEndSimulationTime - _supportStartSimulationTime;
            var maximumSpeed = _moveSpeed * (0.65f + (Ability.Mobility * 0.5f));
            _supportTargetPosition = Vector3.MoveTowards(
                _supportStartPosition,
                ConstrainGroundPosition(movementTarget),
                maximumSpeed * availableSeconds);
        }

        public void PrepareForTraining(Vector3 worldPosition)
        {
            CancelScheduledContact();
            var constrained = ConstrainToOwnCourt(worldPosition);
            transform.position = constrained;
            _motionOrigin = constrained;
            _presentation.SetPose(StickFigurePose.Ready, 1f);
        }

        public void SetPreparedFacing(TeamCourtFrame frame, SetRoute route)
        {
            if (!Enum.IsDefined(typeof(SetRoute), route))
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            PreparedForward = PreparedForwardFor(frame);
            transform.forward = new Vector3(PreparedForward.X, PreparedForward.Y, PreparedForward.Z);
        }

        public static SimVector3 PreparedForwardFor(TeamCourtFrame frame)
        {
            return frame.ToWorld(new SimVector3(-1f, 0f, 0.25f).Normalized);
        }

        public void ApplyCrowdingOffset(Vector3 worldOffset)
        {
            worldOffset.y = 0f;
            transform.position = ConstrainToOwnCourt(transform.position + worldOffset);
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewContactFrames(TechniqueAction action)
        {
            return PreviewContactFramesAt(action, transform.position);
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewContactFramesAt(
            TechniqueAction action,
            Vector3 worldPosition)
        {
            var previewPosition = action == TechniqueAction.Attack
                ? EvaluateAttackContactPosition(worldPosition, transform.forward)
                : worldPosition;
            return PreviewContactFramesAtResolvedPosition(action, previewPosition);
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewAttackContactFramesAt(
            AttackApproachPlan approach)
        {
            return PreviewContactFramesAtResolvedPosition(
                TechniqueAction.Attack,
                EvaluatePlannedAttackContactPosition(approach));
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewAttackContactFramesAt(
            AttackContactPlan plan)
        {
            plan.Validate();
            return PreviewContactFramesAtResolvedPosition(
                TechniqueAction.Attack,
                AttackRootContactPosition(plan));
        }

        public IReadOnlyList<ContactCapsuleFrame> PreviewBlockArmFrames(
            float simulationTime,
            Vector3 rootPosition)
        {
            if (float.IsNaN(simulationTime) || float.IsInfinity(simulationTime) ||
                float.IsNaN(rootPosition.x) || float.IsInfinity(rootPosition.x) ||
                float.IsNaN(rootPosition.y) || float.IsInfinity(rootPosition.y) ||
                float.IsNaN(rootPosition.z) || float.IsInfinity(rootPosition.z))
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTime));
            }

            var savedPosition = transform.position;
            var savedRotation = transform.rotation;
            try
            {
                transform.position = ConstrainToOwnCourt(rootPosition);
                transform.forward = Id.Team == TeamId.Blue ? Vector3.forward : Vector3.back;
                return _presentation.WithPreviewPose(StickFigurePose.Block, () =>
                {
                    var snapshots = new BlockArmContactVolumes(Rig).Capture(false, 0);
                    var frames = new ContactCapsuleFrame[snapshots.Count];
                    for (var index = 0; index < snapshots.Count; index++)
                    {
                        frames[index] = snapshots[index].Current;
                    }

                    return frames;
                });
            }
            finally
            {
                transform.position = savedPosition;
                transform.rotation = savedRotation;
            }
        }

        public Vector3 ResolveBlockRootTarget(
            SimVector3 desiredContactCenter,
            Vector3 nominalRootTarget)
        {
            if (!desiredContactCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredContactCenter));
            }

            nominalRootTarget.y = 0f;
            var frames = PreviewBlockArmFrames(0f, nominalRootTarget);
            var centerHeight = 0f;
            for (var index = 0; index < frames.Count; index++)
            {
                centerHeight += (frames[index].Start.Y + frames[index].End.Y) * 0.5f;
            }

            centerHeight /= frames.Count;
            nominalRootTarget.y = Mathf.Clamp(
                desiredContactCenter.Y - centerHeight,
                0f,
                MaximumBlockContactRootHeight());
            return nominalRootTarget;
        }

        public Vector3 ResolveContactRootTarget(
            TechniqueAction action,
            SimVector3 desiredContactCenter,
            Vector3 nominalRootTarget)
        {
            if (action == TechniqueAction.Attack)
            {
                throw new ArgumentException(
                    "Attack root targets must be resolved from an attack contact plan.",
                    nameof(action));
            }

            var frames = PreviewContactFramesAt(action, nominalRootTarget);
            var previewCenter = SimVector3.Zero;
            if (action == TechniqueAction.Set)
            {
                previewCenter = frames[0].Origin +
                                (frames[0].Normal * SimulatedBall.DefaultRadius);
            }
            else
            {
                var origin = SimVector3.Zero;
                var normal = SimVector3.Zero;
                foreach (var frame in frames)
                {
                    origin += frame.Origin;
                    normal += frame.Normal;
                }

                previewCenter = (origin / frames.Count) +
                                ((normal / frames.Count).Normalized * SimulatedBall.DefaultRadius);
            }

            var correction = desiredContactCenter - previewCenter;
            return ConstrainGroundPosition(
                nominalRootTarget + new Vector3(correction.X, 0f, correction.Z));
        }

        private IReadOnlyList<ContactSurfaceFrame> PreviewContactFramesAtResolvedPosition(
            TechniqueAction action,
            Vector3 worldPosition)
        {
            var savedPosition = transform.position;
            try
            {
                transform.position = ConstrainToOwnCourt(worldPosition);
                return _presentation.WithPreviewPose(action, _setDecision.ExecutedStyle, () =>
                {
                    var previewSurfaces = new PlayerContactSurfaces(Rig, transform)
                        .Capture(action, true, 0);
                    var frames = new ContactSurfaceFrame[previewSurfaces.Count];
                    for (var index = 0; index < previewSurfaces.Count; index++)
                    {
                        frames[index] = previewSurfaces[index].Current;
                    }

                    return frames;
                });
            }
            finally
            {
                transform.position = savedPosition;
            }
        }

        public void CollectContacts(
            float simulationTime,
            float deltaSeconds,
            ICollection<BallContactCandidate> contacts)
        {
            if (contacts == null)
            {
                return;
            }

            transform.position = ConstrainToOwnCourt(transform.position);

            if (_hasPhysicalBlockContact && !_actionTimelineState.HasScheduledContact)
            {
                CollectPhysicalBlockContacts(simulationTime, deltaSeconds, contacts);
                return;
            }

            if (_actionTimelineState.HasSupportAction && !_actionTimelineState.HasScheduledContact)
            {
                ApplySupportAction(simulationTime, deltaSeconds);
            }

            if (!_actionTimelineState.HasScheduledContact &&
                TryAddEmergencyReceiveContacts(simulationTime, deltaSeconds, contacts))
            {
                return;
            }

            if (!_actionTimelineState.HasScheduledContact)
            {
                return;
            }

            _actionTimelineState.TrySampleContact(simulationTime, out var sample);
            ApplyScheduledRootMotion(sample, simulationTime);
            CaptureObservedAttackTakeoff(simulationTime);
            ApplyScheduledPose(sample, deltaSeconds);
            ApplyLimitedContactAlignment(sample);
            transform.position = ConstrainToOwnCourt(transform.position);
            var contactAction = _isControlledHandling
                ? TechniqueAction.Receive
                : _scheduledAction;
            var surfaceAction = _isControlledHandling
                ? TechniqueAction.Set
                : _scheduledAction;
            var surfaces = ContactSurfaces.Capture(
                surfaceAction,
                sample.SurfaceActive,
                _contactGroupId,
                setContactHand: CurrentSetContactHand());
            LastScheduledSurfaceCenter = SimVector3.Zero;
            LastScheduledSurfaceNormal = SimVector3.Zero;
            foreach (var surface in surfaces)
            {
                LastScheduledSurfaceCenter += surface.Current.Origin +
                                              (surface.Current.Normal * SimulatedBall.DefaultRadius);
                LastScheduledSurfaceNormal += surface.Current.Normal;
            }

            LastScheduledSurfaceCenter /= surfaces.Count;
            LastScheduledSurfaceNormal = (LastScheduledSurfaceNormal / surfaces.Count).Normalized;
            if (sample.SurfaceActive && _hasPlannedContactCenter)
            {
                MinimumActiveSurfacePlanError = Mathf.Min(
                    MinimumActiveSurfacePlanError,
                    (LastScheduledSurfaceCenter - _plannedContactCenter).Magnitude);
            }
            var strikeDirection = _targetVelocity.SqrMagnitude > 0.000001f
                ? _targetVelocity.Normalized
                : SimVector3.Up;
            var response = ResponseFor(contactAction);
            // AI assistance resolves the physical impulse toward this action's already-imperfect
            // execution target. Ability still changes that target, reaction time, reachable position,
            // contact pose and set-style availability before technique control is applied.
            var playerTechnique = _scheduledAction == TechniqueAction.Receive ||
                                  _scheduledAction == TechniqueAction.Set ||
                                  _scheduledAction == TechniqueAction.Attack
                ? 1f
                : Ability.TechniqueFor(_scheduledAction);
            if (_scheduledAction == TechniqueAction.Set)
            {
                playerTechnique *= _setDecision.ControlScale;
            }

            foreach (var surface in surfaces)
            {
                contacts.Add(new BallContactCandidate(
                    surface,
                    contactAction,
                    Id,
                    playerTechnique,
                    _targetVelocity,
                    strikeDirection,
                    response));
            }

            if (sample.Phase == ActionPhase.Complete)
            {
                CancelScheduledContact();
            }
        }

        private void CollectPhysicalBlockContacts(
            float simulationTime,
            float deltaSeconds,
            ICollection<BallContactCandidate> contacts)
        {
            _actionTimelineState.TrySampleSupport(simulationTime, out var sample);
            transform.position = ConstrainToOwnCourt(EvaluateSupportPosition(simulationTime));
            var pose = sample.Phase == ActionPhase.Recover || sample.Phase == ActionPhase.Complete
                ? StickFigurePose.Ready
                : StickFigurePose.Block;
            _presentation.SetPose(pose, Mathf.Clamp01(deltaSeconds * 12f));
            var armVolumes = _blockArmContactVolumes.Capture(
                sample.SurfaceActive,
                _physicalBlockContactGroupId);

            if (sample.SurfaceActive)
            {
                if (!_physicalBlockActivationLogged)
                {
                    _physicalBlockActivationLogged = true;
                    Debug.Log(
                        $"[Physical3v3] block-surface team={Id.Team} actor={Id.Role} " +
                        $"time={simulationTime:0.00} root=({transform.position.x:0.00}," +
                        $"{transform.position.y:0.00},{transform.position.z:0.00}) " +
                        $"leftPalm=({armVolumes[2].Current.End.X:0.00}," +
                        $"{armVolumes[2].Current.End.Y:0.00}," +
                        $"{armVolumes[2].Current.End.Z:0.00})");
                }

                var strikeDirection = _physicalBlockTargetVelocity.SqrMagnitude > 0.000001f
                    ? _physicalBlockTargetVelocity.Normalized
                    : -new SimVector3(transform.forward.x, transform.forward.y, transform.forward.z);
                var response = ResponseFor(TechniqueAction.Block);
                foreach (var armVolume in armVolumes)
                {
                    contacts.Add(new BallContactCandidate(
                        armVolume,
                        TechniqueAction.Block,
                        Id,
                        Ability.TechniqueFor(TechniqueAction.Block),
                        _physicalBlockTargetVelocity,
                        strikeDirection,
                        response));
                }
            }

            if (sample.Phase == ActionPhase.Complete)
            {
                transform.position = ConstrainToOwnCourt(_supportTargetPosition);
                DisableBlockContactWindow();
            }
        }

        private bool TryAddEmergencyReceiveContacts(
            float simulationTime,
            float deltaSeconds,
            ICollection<BallContactCandidate> contacts)
        {
            if (!_actionTimelineState.HasEmergencyReceiveWindow)
            {
                return false;
            }

            if (simulationTime < _actionTimelineState.EmergencyReceiveStartSimulationTime)
            {
                return false;
            }

            if (simulationTime > _actionTimelineState.EmergencyReceiveEndSimulationTime)
            {
                DisableEmergencyReceiveWindow();
                return false;
            }

            _presentation.SetPose(StickFigurePose.Receive, Mathf.Clamp01(deltaSeconds * 18f));
            var targetVelocity = _actionTimelineState.EmergencyReceiveTargetVelocity;
            var strikeDirection = targetVelocity.SqrMagnitude > 0.000001f
                ? targetVelocity.Normalized
                : SimVector3.Up;
            var response = ResponseFor(TechniqueAction.Receive);
            var playerTechnique = Ability.TechniqueFor(TechniqueAction.Receive);
            var surfaces = ContactSurfaces.Capture(
                TechniqueAction.Receive,
                true,
                _actionTimelineState.EmergencyReceiveContactGroupId);
            foreach (var surface in surfaces)
            {
                contacts.Add(new BallContactCandidate(
                    surface,
                    TechniqueAction.Receive,
                    Id,
                    playerTechnique,
                    targetVelocity,
                    strikeDirection,
                    response));
            }

            return true;
        }

        private void ApplySupportAction(float simulationTime, float deltaSeconds)
        {
            _actionTimelineState.TrySampleSupport(simulationTime, out var sample);
            transform.position = ConstrainToOwnCourt(EvaluateSupportPosition(simulationTime));
            if (!_supportActionActivated &&
                (sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact))
            {
                _supportActionActivated = true;
                SupportActionActivated?.Invoke(this, _actionTimelineState.SupportAction);
            }

            var pose = _actionTimelineState.SupportAction switch
            {
                TechniqueAction.Block => StickFigurePose.Block,
                TechniqueAction.Attack => StickFigurePose.Run,
                TechniqueAction.Set => StickFigurePose.Run,
                _ => StickFigurePose.Receive
            };
            if (sample.Phase == ActionPhase.Prepare &&
                _actionTimelineState.SupportAction == TechniqueAction.Receive)
            {
                pose = StickFigurePose.Run;
            }
            else if (sample.Phase == ActionPhase.Recover || sample.Phase == ActionPhase.Complete)
            {
                pose = StickFigurePose.Ready;
            }

            _presentation.SetPose(pose, Mathf.Clamp01(deltaSeconds * 12f));
            if (sample.Phase == ActionPhase.Complete)
            {
                _actionTimelineState.DisableSupport();
                _supportActionActivated = false;
                transform.position = ConstrainToOwnCourt(_supportTargetPosition);
            }
        }

        private void ApplyScheduledPose(ActionTimelineSample sample, float deltaSeconds)
        {
            if (_isMovingThisStep && sample.Phase == ActionPhase.Prepare)
            {
                _presentation.SetPose(StickFigurePose.Run, Mathf.Clamp01(deltaSeconds * 12f));
                return;
            }

            if (_isControlledHandling)
            {
                var handlingPose = sample.Phase == ActionPhase.Recover ||
                                   sample.Phase == ActionPhase.Complete
                    ? StickFigurePose.Ready
                    : StickFigurePose.Set;
                _presentation.SetPoseWithContactError(
                    handlingPose,
                    Mathf.Clamp01(deltaSeconds * 14f),
                    TechniqueAction.Set,
                    _executionError.ContactPositionError,
                    _executionError.ContactNormalErrorDegrees,
                    sample.SurfaceActive ? 1f : 0f);
                return;
            }

            if (_scheduledAction == TechniqueAction.Attack && ApplyAttackPose(sample, deltaSeconds))
            {
                return;
            }

            if (_scheduledAction == TechniqueAction.Set && ApplySetPose(sample, deltaSeconds))
            {
                return;
            }

            var pose = _scheduledAction switch
            {
                TechniqueAction.Receive => StickFigurePose.Receive,
                TechniqueAction.Set => StickFigurePose.Set,
                TechniqueAction.Attack => sample.Phase == ActionPhase.Prepare
                    ? StickFigurePose.Approach
                    : sample.Phase == ActionPhase.FollowThrough || sample.Phase == ActionPhase.Recover
                        ? StickFigurePose.Landing
                        : StickFigurePose.Spike,
                TechniqueAction.Block => StickFigurePose.Block,
                TechniqueAction.Serve => StickFigurePose.Serve,
                _ => StickFigurePose.Ready
            };
            if (sample.Phase == ActionPhase.Recover || sample.Phase == ActionPhase.Complete)
            {
                pose = StickFigurePose.Ready;
            }

            var errorWeight = sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact
                ? 1f
                : sample.Phase == ActionPhase.FollowThrough ? 1f - sample.PhaseProgress : 0f;
            var blend = Mathf.Clamp01(deltaSeconds * 18f * _executionError.SurfaceSpeedScale);
            _presentation.SetPoseWithContactError(
                pose,
                blend,
                _scheduledAction,
                _executionError.ContactPositionError,
                _executionError.ContactNormalErrorDegrees,
                errorWeight);
        }

        private bool ApplySetPose(ActionTimelineSample sample, float deltaSeconds)
        {
            var contactPose = _presentation.SetContactPose(_setDecision.ExecutedStyle);
            var errorWeight = sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact
                ? 1f
                : sample.Phase == ActionPhase.FollowThrough ? 1f - sample.PhaseProgress : 0f;
            switch (sample.Phase)
            {
                case ActionPhase.Prepare:
                    _presentation.SetPoseWithContactError(
                        StickFigurePose.SetDraw,
                        Mathf.Clamp01(deltaSeconds * 12f),
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        0f);
                    return true;
                case ActionPhase.Power:
                    _presentation.SetPoseTransition(
                        StickFigurePose.SetDraw,
                        contactPose,
                        sample.PhaseProgress * 0.8f,
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.Contact:
                    _presentation.SetPoseTransition(
                        StickFigurePose.SetDraw,
                        contactPose,
                        0.8f + (sample.PhaseProgress * 0.2f),
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.FollowThrough:
                    _presentation.SetPoseTransition(
                        contactPose,
                        StickFigurePose.Ready,
                        sample.PhaseProgress,
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                default:
                    return false;
            }
        }

        private bool ApplyAttackPose(ActionTimelineSample sample, float deltaSeconds)
        {
            var errorWeight = sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact
                ? 1f
                : sample.Phase == ActionPhase.FollowThrough ? 1f - sample.PhaseProgress : 0f;
            switch (sample.Phase)
            {
                case ActionPhase.Prepare:
                    _presentation.SetPoseWithContactError(
                        StickFigurePose.SpikeWindup,
                        Mathf.Clamp01(deltaSeconds * 10f),
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        0f);
                    return true;
                case ActionPhase.Power:
                    _presentation.SetPoseTransition(
                        StickFigurePose.SpikeWindup,
                        StickFigurePose.Spike,
                        sample.PhaseProgress,
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.Contact when sample.PhaseProgress <= 0.5f:
                    _presentation.SetPoseTransition(
                        StickFigurePose.SpikeWindup,
                        StickFigurePose.Spike,
                        1f,
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.Contact:
                    _presentation.SetPoseTransition(
                        StickFigurePose.Spike,
                        StickFigurePose.Landing,
                        (sample.PhaseProgress - 0.5f) * 0.5f,
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.FollowThrough:
                    _presentation.SetPoseTransition(
                        StickFigurePose.Spike,
                        StickFigurePose.Landing,
                        0.25f + (sample.PhaseProgress * 0.75f),
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyScheduledRootMotion(ActionTimelineSample sample, float simulationTime)
        {
            var movementPosition = EvaluateScheduledMovement(simulationTime, out var movementComplete);
            _isMovingThisStep = _hasScheduledMovement && !movementComplete;
            if (_scheduledAction != TechniqueAction.Attack)
            {
                if (_hasScheduledMovement)
                {
                    transform.position = ConstrainToOwnCourt(movementPosition);
                }

                return;
            }

            var position = EvaluateAttackPosition(simulationTime, movementPosition);
            if (sample.Phase == ActionPhase.Complete)
            {
                position.y = _motionOrigin.y;
            }

            transform.position = ConstrainToOwnCourt(position);
        }

        private void ApplyLimitedContactAlignment(ActionTimelineSample sample)
        {
            if (!_hasPlannedContactCenter ||
                sample.Phase != ActionPhase.Power && sample.Phase != ActionPhase.Contact)
            {
                return;
            }

            var currentFrames = new PlayerContactSurfaces(Rig, transform)
                .Capture(
                    _isControlledHandling ? TechniqueAction.Set : _scheduledAction,
                    true,
                    _contactGroupId,
                    setContactHand: CurrentSetContactHand());
            var currentCenter = SimVector3.Zero;
            foreach (var frame in currentFrames)
            {
                currentCenter += frame.Current.Origin +
                                 (frame.Current.Normal * SimulatedBall.DefaultRadius);
            }

            currentCenter /= currentFrames.Count;

            var correction = _plannedContactCenter - currentCenter;
            var maximumCorrection = _isControlledHandling ? 0.70f : _scheduledAction switch
            {
                TechniqueAction.Attack => 0.18f,
                TechniqueAction.Set => 0.30f,
                _ => 0.16f
            };
            if (correction.Magnitude > maximumCorrection)
            {
                // Keep a small numerical margin so the applied vector remains inside the
                // public bound after single-precision normalization and magnitude recovery.
                correction = correction.Normalized * (maximumCorrection - 0.0001f);
            }

            if (sample.Phase == ActionPhase.Power)
            {
                correction *= Mathf.SmoothStep(0f, 1f, sample.PhaseProgress);
            }
            MaximumAppliedContactCorrection = Mathf.Max(
                MaximumAppliedContactCorrection,
                correction.Magnitude);
            transform.position = ConstrainToOwnCourt(
                transform.position + new Vector3(correction.X, correction.Y, correction.Z));
        }

        private Vector3 EvaluateAttackPosition(float simulationTime, Vector3 movementPosition)
        {
            if (_hasAttackApproach)
            {
                return EvaluatePlannedAttackPosition(simulationTime, movementPosition);
            }

            var takeoffTime = _actionTimelineState.ContactTimeline.ActualContactTime - 0.38f;
            var landingTime = _actionTimelineState.ContactTimeline.ActualContactTime + 0.45f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var jumpHeight = (0.72f + (Ability.Jump * 0.5f)) * 4f * jumpProgress * (1f - jumpProgress);
            var approachStart = _actionTimelineState.ContactTimeline.ActualContactTime - 0.72f;
            var approachProgress = Mathf.Clamp01((simulationTime - approachStart) / 0.55f);
            approachProgress = approachProgress * approachProgress * (3f - (2f * approachProgress));
            var approachDistance = 0.45f + (Ability.Mobility * 0.35f);
            var position = movementPosition + (_motionForward * approachDistance * approachProgress);
            position.y = _motionOrigin.y + jumpHeight;
            return position;
        }

        private void CaptureObservedAttackTakeoff(float simulationTime)
        {
            if (_hasObservedAttackTakeoff ||
                _scheduledAction != TechniqueAction.Attack ||
                !_actionTimelineState.HasScheduledContact)
            {
                return;
            }

            var takeoffTime = AttackTakeoffTime();
            if (simulationTime < takeoffTime)
            {
                return;
            }

            // Snapshot the authoritative physical trajectory at its actual takeoff
            // time. The requested plan point is never copied into this observation.
            var movementPosition = EvaluateScheduledMovement(takeoffTime, out _);
            var takeoffPosition = EvaluateAttackPosition(takeoffTime, movementPosition);

            _observedAttackTakeoff = new ObservedAttackTakeoff(
                new SimVector3(
                    takeoffPosition.x,
                    takeoffPosition.y,
                    takeoffPosition.z),
                takeoffTime);
            _hasObservedAttackTakeoff = true;
        }

        private Vector3 EvaluatePlannedAttackPosition(float simulationTime, Vector3 movementPosition)
        {
            var approachStartTime = _movementEndSimulationTime;
            var takeoffTime = AttackTakeoffTime();
            var approachProgress = Mathf.InverseLerp(approachStartTime, takeoffTime, simulationTime);
            approachProgress = approachProgress * approachProgress * (3f - (2f * approachProgress));
            var position = Vector3.Lerp(movementPosition, _attackTakeoffPosition, approachProgress);

            if (_hasAttackContactPlan)
            {
                if (simulationTime < takeoffTime)
                {
                    return position;
                }

                if (simulationTime <= _actionTimelineState.ContactTimeline.ActualContactTime)
                {
                    var ascent = Mathf.InverseLerp(
                        takeoffTime,
                        _actionTimelineState.ContactTimeline.ActualContactTime,
                        simulationTime);
                    ascent = ascent * ascent * (3f - (2f * ascent));
                    return Vector3.Lerp(_attackTakeoffPosition, _attackContactRootPosition, ascent);
                }

                const float plannedLandingSeconds = 0.45f;
                var descent = Mathf.Clamp01(
                    (simulationTime - _actionTimelineState.ContactTimeline.ActualContactTime) /
                    plannedLandingSeconds);
                descent = descent * descent * (3f - (2f * descent));
                var landed = _attackContactRootPosition;
                landed.y = _motionOrigin.y;
                return Vector3.Lerp(_attackContactRootPosition, landed, descent);
            }

            var landingTime = _actionTimelineState.ContactTimeline.ActualContactTime + 0.45f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var jumpHeight = (0.72f + (Ability.Jump * 0.5f)) *
                             _attackApproach.JumpQuality *
                             4f * jumpProgress * (1f - jumpProgress);
            position.y = _motionOrigin.y + jumpHeight;
            return position;
        }

        private void ConfigureAttackApproach(AttackApproachPlan approach, bool useContinuationTakeoff)
        {
            var approachStart = _movementTargetPosition;
            var requestedTakeoff = ConstrainGroundPosition(ToUnity(approach.Takeoff));
            if (useContinuationTakeoff)
            {
                _attackTakeoffPosition = approachStart;
                if (_hasAttackContactPlan)
                {
                    ConfigureAttackContactRootPosition();
                }

                return;
            }

            var takeoffTime = Mathf.Max(
                _movementEndSimulationTime + 0.01f,
                _actionTimelineState.ContactTimeline.ActualContactTime - 0.38f);
            var availableSeconds = takeoffTime - _movementEndSimulationTime;
            var maximumSpeed = _moveSpeed * (0.65f + (Ability.Mobility * 0.5f));
            _attackTakeoffPosition = Vector3.MoveTowards(
                approachStart,
                requestedTakeoff,
                maximumSpeed * availableSeconds);
            if (_hasAttackContactPlan)
            {
                ConfigureAttackContactRootPosition();
            }
            ScheduledMovementDistance += Vector3.Distance(approachStart, _attackTakeoffPosition);
            MovementShortfall += Vector3.Distance(_attackTakeoffPosition, requestedTakeoff);
        }

        private float AttackJumpLead()
        {
            return _hasAttackContactPlan
                ? Mathf.Lerp(0.24f, 0.38f, _attackContactPlan.JumpTiming)
                : 0.38f;
        }

        private float AttackTakeoffTime()
        {
            var requestedTakeoffTime =
                _actionTimelineState.ContactTimeline.ActualContactTime - AttackJumpLead();
            return _hasAttackApproach
                ? Mathf.Max(_movementEndSimulationTime + 0.01f, requestedTakeoffTime)
                : requestedTakeoffTime;
        }

        private void ConfigureAttackContactRootPosition()
        {
            var requestedRootPosition = AttackRootContactPosition(_attackContactPlan);
            var maximumHorizontalDistance = _moveSpeed *
                                            (0.65f + (Ability.Mobility * 0.5f)) *
                                            AttackJumpLead();
            var takeoffHorizontal = new Vector3(
                _attackTakeoffPosition.x,
                0f,
                _attackTakeoffPosition.z);
            var requestedHorizontal = new Vector3(
                requestedRootPosition.x,
                0f,
                requestedRootPosition.z);
            var reachableHorizontal = Vector3.MoveTowards(
                takeoffHorizontal,
                requestedHorizontal,
                maximumHorizontalDistance);
            _attackContactRootPosition = new Vector3(
                reachableHorizontal.x,
                requestedRootPosition.y,
                reachableHorizontal.z);
            MovementShortfall += Vector3.Distance(reachableHorizontal, requestedHorizontal);
        }

        private void ConfigureScheduledMovement(
            Vector3 requestedTarget,
            float movementStartSimulationTime,
            float scheduledContactTime,
            TechniqueAction action,
            float? movementLeadOverride = null)
        {
            _movementStartPosition = ConstrainGroundPosition(transform.position);
            _movementStartSimulationTime = movementStartSimulationTime;
            var movementLead = movementLeadOverride ?? (action == TechniqueAction.Attack ? 0.32f : 0.10f);
            _movementEndSimulationTime = Mathf.Max(
                _movementStartSimulationTime + 0.01f,
                scheduledContactTime - movementLead);
            var availableSeconds = _movementEndSimulationTime - _movementStartSimulationTime;
            var maximumSpeed = _moveSpeed * (0.65f + (Ability.Mobility * 0.5f));
            var maximumDistance = maximumSpeed * availableSeconds;
            _movementTargetPosition = Vector3.MoveTowards(
                _movementStartPosition,
                ConstrainGroundPosition(requestedTarget),
                maximumDistance);
            ScheduledMovementDistance = Vector3.Distance(
                _movementStartPosition,
                _movementTargetPosition);
            MovementShortfall = Vector3.Distance(_movementTargetPosition, requestedTarget);
            _hasScheduledMovement = Vector3.Distance(
                _movementStartPosition,
                _movementTargetPosition) > 0.01f;
        }

        private void ConfigureContinuationMovement(
            Vector3 requestedTakeoff,
            float scheduledContactTime)
        {
            _movementStartPosition = ConstrainGroundPosition(transform.position);
            _movementStartSimulationTime = _actionTimelineState.HasSupportAction
                ? Mathf.Min(_actionTimelineState.SupportTimeline.ActualContactTime, scheduledContactTime)
                : scheduledContactTime;
            _movementEndSimulationTime = Mathf.Max(
                _movementStartSimulationTime + 0.01f,
                scheduledContactTime - 0.38f);
            var requestedTarget = ConstrainGroundPosition(requestedTakeoff);
            var availableSeconds = _movementEndSimulationTime - _movementStartSimulationTime;
            var maximumSpeed = _moveSpeed * (0.65f + (Ability.Mobility * 0.5f));
            _movementTargetPosition = Vector3.MoveTowards(
                _movementStartPosition,
                requestedTarget,
                maximumSpeed * availableSeconds);
            ScheduledMovementDistance = Vector3.Distance(
                _movementStartPosition,
                _movementTargetPosition);
            MovementShortfall = Vector3.Distance(_movementTargetPosition, requestedTarget);
            _hasScheduledMovement = ScheduledMovementDistance > 0.01f;
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

            var progress = Mathf.InverseLerp(
                _movementStartSimulationTime,
                _movementEndSimulationTime,
                simulationTime);
            progress = progress * progress * (3f - (2f * progress));
            complete = progress >= 1f;
            return Vector3.Lerp(_movementStartPosition, _movementTargetPosition, progress);
        }

        private Vector3 EvaluateSupportPosition(float simulationTime)
        {
            var position = EvaluateSupportGroundMovement(simulationTime);
            if (_actionTimelineState.SupportAction == TechniqueAction.Block)
            {
                position.y += EvaluateSupportBlockJump(simulationTime);
            }

            return position;
        }

        private Vector3 EvaluateSupportGroundMovement(float simulationTime)
        {
            if (simulationTime >= _supportEndSimulationTime)
            {
                return _supportTargetPosition;
            }

            if (simulationTime <= _supportStartSimulationTime)
            {
                return _supportStartPosition;
            }

            var progress = Mathf.InverseLerp(
                _supportStartSimulationTime,
                _supportEndSimulationTime,
                simulationTime);
            progress = progress * progress * (3f - (2f * progress));
            return Vector3.Lerp(_supportStartPosition, _supportTargetPosition, progress);
        }

        private float EvaluateSupportBlockJump(float simulationTime)
        {
            var contactTime = _actionTimelineState.SupportTimeline.ActualContactTime;
            var takeoffTime = contactTime - 0.22f;
            var landingTime = contactTime + 0.28f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var contactProgress = 0.22f / 0.50f;
            var shape = 4f * jumpProgress * (1f - jumpProgress);
            var contactShape = 4f * contactProgress * (1f - contactProgress);
            var contactRootHeight = _hasPhysicalBlockContact
                ? _physicalBlockContactRootHeight
                : MaximumBlockContactRootHeight();
            return contactRootHeight * shape / contactShape;
        }

        private float MaximumBlockContactRootHeight()
        {
            var jumpHeight = 0.30f + (Ability.Jump * 0.20f);
            var contactProgress = 0.22f / 0.50f;
            return jumpHeight * 4f * contactProgress * (1f - contactProgress);
        }

        private Vector3 ConstrainGroundPosition(Vector3 position)
        {
            position.y = 0f;
            return ConstrainToOwnCourt(position);
        }

        private Vector3 ConstrainToOwnCourt(Vector3 position)
        {
            position.x = Mathf.Clamp(
                position.x,
                -CourtBuilder.HalfWidth + BoundaryClearance,
                CourtBuilder.HalfWidth - BoundaryClearance);
            position.z = Id.Team == TeamId.Blue
                ? Mathf.Clamp(
                    position.z,
                    -_courtHalfLength + BoundaryClearance,
                    -NetClearance)
                : Mathf.Clamp(
                    position.z,
                    NetClearance,
                    _courtHalfLength - BoundaryClearance);
            return position;
        }

        private bool IsWithinOwnCourtBounds(Vector3 position)
        {
            const float tolerance = 0.0001f;
            if (position.x < -CourtBuilder.HalfWidth + BoundaryClearance - tolerance ||
                position.x > CourtBuilder.HalfWidth - BoundaryClearance + tolerance ||
                position.z < -_courtHalfLength + BoundaryClearance - tolerance ||
                position.z > _courtHalfLength - BoundaryClearance + tolerance)
            {
                return false;
            }

            return Id.Team == TeamId.Blue
                ? position.z <= -NetClearance + tolerance
                : position.z >= NetClearance - tolerance;
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private Vector3 EvaluateAttackContactPosition(Vector3 origin, Vector3 forward)
        {
            const float jumpProgress = 0.38f / (0.38f + 0.45f);
            var jumpHeight = (0.72f + (Ability.Jump * 0.5f)) * 4f * jumpProgress * (1f - jumpProgress);
            var approachDistance = 0.45f + (Ability.Mobility * 0.35f);
            var position = origin + (forward * approachDistance);
            position.y = origin.y + jumpHeight;
            return position;
        }

        private Vector3 EvaluatePlannedAttackContactPosition(AttackApproachPlan approach)
        {
            const float jumpProgress = 0.38f / (0.38f + 0.45f);
            var position = ToUnity(approach.Takeoff);
            position.y += (0.72f + (Ability.Jump * 0.5f)) *
                          approach.JumpQuality *
                          4f * jumpProgress * (1f - jumpProgress);
            return position;
        }

        private Vector3 AttackRootContactPosition(AttackContactPlan plan)
        {
            return ContactRootPosition(plan, TechniqueAction.Attack);
        }

        private Vector3 ContactRootPosition(AttackContactPlan plan, TechniqueAction surfaceAction)
        {
            var takeoff = ToUnity(plan.Takeoff);
            var frames = PreviewContactFramesAtResolvedPosition(surfaceAction, takeoff);
            var currentCenter = SimVector3.Zero;
            foreach (var frame in frames)
            {
                currentCenter += frame.Origin + (frame.Normal * SimulatedBall.DefaultRadius);
            }

            currentCenter /= frames.Count;
            var correction = plan.ContactCenter - currentCenter;
            return ConstrainToOwnCourt(takeoff + new Vector3(correction.X, correction.Y, correction.Z));
        }

        private SetContactHand CurrentSetContactHand()
        {
            if (_scheduledAction != TechniqueAction.Set)
            {
                return SetContactHand.Both;
            }

            return _setDecision.ExecutedStyle switch
            {
                SetTechniqueStyle.OneHandLeft => SetContactHand.Left,
                SetTechniqueStyle.OneHandRight => SetContactHand.Right,
                _ => SetContactHand.Both
            };
        }

        private static ContactResponseParameters ResponseFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new ContactResponseParameters(0.85f, 1f, 0.12f, 0.08f),
                TechniqueAction.Set => new ContactResponseParameters(0.75f, 1f, 0.08f, 0.08f),
                TechniqueAction.Attack => new ContactResponseParameters(0.55f, 0.42f, 0.18f, 0.08f),
                TechniqueAction.Block => new ContactResponseParameters(0.65f, 0.8f, 0.22f, 0.08f),
                TechniqueAction.Serve => new ContactResponseParameters(0.72f, 1f, 0.15f, 0.08f),
                _ => new ContactResponseParameters(0.75f, 1f, 0.1f, 0.08f)
            };
        }

        public IEnumerator MoveTo(Vector3 destination)
        {
            destination = ConstrainGroundPosition(destination);
            var speed = 0f;
            const float acceleration = 24f;
            while ((transform.position - destination).sqrMagnitude > 0.01f)
            {
                _presentation.SetPose(StickFigurePose.Run, Time.deltaTime * 8f);
                var distance = Vector3.Distance(transform.position, destination);
                var brakingSpeed = Mathf.Sqrt(2f * acceleration * distance);
                var targetSpeed = Mathf.Min(_moveSpeed * (0.65f + (Ability.Mobility * 0.5f)), brakingSpeed);
                speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * Time.deltaTime);
                transform.position = ConstrainToOwnCourt(
                    Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime));
                yield return null;
            }

            transform.position = ConstrainToOwnCourt(destination);
            _presentation.SetPose(StickFigurePose.Ready, 0.25f);
        }
    }
}

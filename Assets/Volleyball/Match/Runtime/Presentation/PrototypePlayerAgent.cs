using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    public sealed class PrototypePlayerAgent : MonoBehaviour, IBallContactSource
    {
        [SerializeField]
        private float _moveSpeed = 7f;

        public PlayerId Id { get; private set; }

        public StickFigureRig Rig { get; private set; }

        public PlayerAbilityProfile Ability { get; private set; }

        public PlayerContactSurfaces ContactSurfaces { get; private set; }

        public SetTechniqueStyle CurrentSetStyle => _setDecision.ExecutedStyle;

        public SetTechniqueStyle RequestedSetStyle => _setDecision.RequestedStyle;

        public Vector3 ScheduledMovementTarget => _movementTargetPosition;

        public float MovementShortfall { get; private set; }

        public float ScheduledMovementDistance { get; private set; }

        public event Action<PrototypePlayerAgent, TechniqueAction> SupportActionActivated;

        private ActionTimeline _actionTimeline;
        private TechniqueAction _scheduledAction;
        private SkillExecutionError _executionError;
        private SimVector3 _targetVelocity;
        private int _contactGroupId;
        private bool _hasScheduledContact;
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
        private ActionTimeline _supportTimeline;
        private TechniqueAction _supportAction;
        private Vector3 _supportStartPosition;
        private Vector3 _supportTargetPosition;
        private float _supportStartSimulationTime;
        private float _supportEndSimulationTime;
        private bool _hasSupportAction;
        private bool _supportActionActivated;
        private bool _hasEmergencyReceiveWindow;
        private float _emergencyReceiveStartSimulationTime;
        private float _emergencyReceiveEndSimulationTime;
        private SimVector3 _emergencyReceiveTargetVelocity;
        private int _emergencyReceiveContactGroupId;

        public void Initialize(PlayerId id, Color color, string jerseyNumber)
        {
            Id = id;
            Rig = StickFigureRig.Create(transform, color, jerseyNumber);
            Ability = PlayerAbilityProfile.Default;
            ContactSurfaces = new PlayerContactSurfaces(Rig, transform);
        }

        public void SetAbility(PlayerAbilityProfile ability)
        {
            Ability = ability;
        }

        public void ScheduleContact(
            TechniqueAction action,
            float scheduledSimulationTime,
            SimVector3 targetVelocity,
            SkillExecutionError executionError,
            int contactGroupId,
            SimVector3? plannedContactCenter = null,
            bool emergencyOneHand = false,
            Vector3? movementTarget = null,
            float movementStartSimulationTime = 0f)
        {
            _scheduledAction = action;
            var powerScale = action == TechniqueAction.Attack
                ? 0.90f + (Ability.AttackPower * 0.10f)
                : 1f;
            _targetVelocity = (targetVelocity * powerScale) + executionError.TargetVelocityError;
            if (action == TechniqueAction.Set)
            {
                var worldTarget = new Vector3(
                    _targetVelocity.X,
                    _targetVelocity.Y,
                    _targetVelocity.Z);
                var localTarget = transform.InverseTransformDirection(worldTarget);
                _setDecision = SetTechniqueSelector.Select(
                    new SimVector3(localTarget.x, localTarget.y, localTarget.z),
                    Ability.SetTechnique,
                    emergencyOneHand);
            }
            _executionError = executionError;
            _contactGroupId = contactGroupId;
            _actionTimeline = new ActionTimeline(action, scheduledSimulationTime, executionError.ContactTimingError);
            ConfigureScheduledMovement(
                movementTarget.GetValueOrDefault(transform.position),
                movementStartSimulationTime + executionError.ReactionDelay,
                scheduledSimulationTime,
                action);
            _motionOrigin = _movementTargetPosition;
            _motionForward = transform.forward;
            _hasPlannedContactCenter = plannedContactCenter.HasValue;
            _plannedContactCenter = plannedContactCenter.GetValueOrDefault();
            _hasScheduledContact = true;
            _hasSupportAction = false;
            _supportActionActivated = false;
        }

        public void CancelScheduledContact()
        {
            _hasScheduledContact = false;
            _hasPlannedContactCenter = false;
            _actionTimeline = null;
            _hasSupportAction = false;
            _supportTimeline = null;
            _supportActionActivated = false;
            DisableEmergencyReceiveWindow();
        }

        public void EnableEmergencyReceiveWindow(
            float startSimulationTime,
            float endSimulationTime,
            SimVector3 targetVelocity,
            int contactGroupId)
        {
            _hasEmergencyReceiveWindow = true;
            _emergencyReceiveStartSimulationTime = startSimulationTime;
            _emergencyReceiveEndSimulationTime = Mathf.Max(startSimulationTime, endSimulationTime);
            _emergencyReceiveTargetVelocity = targetVelocity;
            _emergencyReceiveContactGroupId = contactGroupId;
        }

        public void DisableEmergencyReceiveWindow()
        {
            _hasEmergencyReceiveWindow = false;
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

            _supportAction = action;
            _supportTimeline = new ActionTimeline(action, scheduledSimulationTime);
            _supportStartPosition = GroundPosition(transform.position);
            _supportStartSimulationTime = movementStartSimulationTime;
            _supportEndSimulationTime = Mathf.Max(
                _supportStartSimulationTime + 0.01f,
                scheduledSimulationTime - 0.10f);
            var availableSeconds = _supportEndSimulationTime - _supportStartSimulationTime;
            var maximumSpeed = _moveSpeed * (0.65f + (Ability.Mobility * 0.5f));
            _supportTargetPosition = Vector3.MoveTowards(
                _supportStartPosition,
                GroundPosition(movementTarget),
                maximumSpeed * availableSeconds);
            _hasSupportAction = true;
            _supportActionActivated = false;
        }

        public void PrepareForTraining(Vector3 worldPosition)
        {
            CancelScheduledContact();
            transform.position = worldPosition;
            _motionOrigin = worldPosition;
            Rig.SetPose(StickFigurePose.Ready, 1f);
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewContactFrames(TechniqueAction action)
        {
            return PreviewContactFramesAt(action, transform.position);
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewContactFramesAt(
            TechniqueAction action,
            Vector3 worldPosition)
        {
            var savedPosition = transform.position;
            var savedRotations = Rig.CaptureLocalRotations();
            try
            {
                transform.position = worldPosition;
                if (action == TechniqueAction.Attack)
                {
                    transform.position = EvaluateAttackContactPosition(worldPosition, transform.forward);
                }

                Rig.SetPose(ContactPoseFor(action), 1f);
                var previewSurfaces = new PlayerContactSurfaces(Rig, transform)
                    .Capture(action, true, 0);
                var frames = new ContactSurfaceFrame[previewSurfaces.Count];
                for (var index = 0; index < previewSurfaces.Count; index++)
                {
                    frames[index] = previewSurfaces[index].Current;
                }

                return frames;
            }
            finally
            {
                transform.position = savedPosition;
                Rig.RestoreLocalRotations(savedRotations);
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

            if (_hasSupportAction && !_hasScheduledContact)
            {
                ApplySupportAction(simulationTime, deltaSeconds);
            }

            if (!_hasScheduledContact &&
                TryAddEmergencyReceiveContacts(simulationTime, deltaSeconds, contacts))
            {
                return;
            }

            if (!_hasScheduledContact)
            {
                return;
            }

            var sample = _actionTimeline.Sample(simulationTime);
            ApplyScheduledRootMotion(sample, simulationTime);
            ApplyScheduledPose(sample, deltaSeconds);
            ApplyLimitedContactAlignment(sample);
            var surfaces = ContactSurfaces.Capture(
                _scheduledAction,
                sample.SurfaceActive,
                _contactGroupId,
                setContactHand: CurrentSetContactHand());
            var strikeDirection = _targetVelocity.SqrMagnitude > 0.000001f
                ? _targetVelocity.Normalized
                : SimVector3.Up;
            var response = ResponseFor(_scheduledAction);
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
                    _scheduledAction,
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

        private bool TryAddEmergencyReceiveContacts(
            float simulationTime,
            float deltaSeconds,
            ICollection<BallContactCandidate> contacts)
        {
            if (!_hasEmergencyReceiveWindow)
            {
                return false;
            }

            if (simulationTime < _emergencyReceiveStartSimulationTime)
            {
                return false;
            }

            if (simulationTime > _emergencyReceiveEndSimulationTime)
            {
                DisableEmergencyReceiveWindow();
                return false;
            }

            Rig.SetPose(StickFigurePose.Receive, Mathf.Clamp01(deltaSeconds * 18f));
            var strikeDirection = _emergencyReceiveTargetVelocity.SqrMagnitude > 0.000001f
                ? _emergencyReceiveTargetVelocity.Normalized
                : SimVector3.Up;
            var response = ResponseFor(TechniqueAction.Receive);
            var playerTechnique = Ability.TechniqueFor(TechniqueAction.Receive);
            var surfaces = ContactSurfaces.Capture(
                TechniqueAction.Receive,
                true,
                _emergencyReceiveContactGroupId);
            foreach (var surface in surfaces)
            {
                contacts.Add(new BallContactCandidate(
                    surface,
                    TechniqueAction.Receive,
                    Id,
                    playerTechnique,
                    _emergencyReceiveTargetVelocity,
                    strikeDirection,
                    response));
            }

            return true;
        }

        private void ApplySupportAction(float simulationTime, float deltaSeconds)
        {
            var sample = _supportTimeline.Sample(simulationTime);
            transform.position = EvaluateSupportPosition(simulationTime);
            if (!_supportActionActivated &&
                (sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact))
            {
                _supportActionActivated = true;
                SupportActionActivated?.Invoke(this, _supportAction);
            }

            var pose = _supportAction == TechniqueAction.Block
                ? StickFigurePose.Block
                : StickFigurePose.Receive;
            if (sample.Phase == ActionPhase.Prepare && _supportAction == TechniqueAction.Receive)
            {
                pose = StickFigurePose.Run;
            }
            else if (sample.Phase == ActionPhase.Recover || sample.Phase == ActionPhase.Complete)
            {
                pose = StickFigurePose.Ready;
            }

            Rig.SetPose(pose, Mathf.Clamp01(deltaSeconds * 12f));
            if (sample.Phase == ActionPhase.Complete)
            {
                _hasSupportAction = false;
                _supportTimeline = null;
                _supportActionActivated = false;
                transform.position = _supportTargetPosition;
            }
        }

        private void ApplyScheduledPose(ActionTimelineSample sample, float deltaSeconds)
        {
            if (_isMovingThisStep && sample.Phase == ActionPhase.Prepare)
            {
                Rig.SetPose(StickFigurePose.Run, Mathf.Clamp01(deltaSeconds * 12f));
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
            Rig.SetPoseWithContactError(
                pose,
                blend,
                _scheduledAction,
                _executionError.ContactPositionError,
                _executionError.ContactNormalErrorDegrees,
                errorWeight);
        }

        private bool ApplySetPose(ActionTimelineSample sample, float deltaSeconds)
        {
            var contactPose = SetContactPose(_setDecision.ExecutedStyle);
            var errorWeight = sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact
                ? 1f
                : sample.Phase == ActionPhase.FollowThrough ? 1f - sample.PhaseProgress : 0f;
            switch (sample.Phase)
            {
                case ActionPhase.Prepare:
                    Rig.SetPoseWithContactError(
                        StickFigurePose.SetDraw,
                        Mathf.Clamp01(deltaSeconds * 12f),
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        0f);
                    return true;
                case ActionPhase.Power:
                    Rig.SetPoseTransition(
                        StickFigurePose.SetDraw,
                        contactPose,
                        sample.PhaseProgress * 0.8f,
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.Contact:
                    Rig.SetPoseTransition(
                        StickFigurePose.SetDraw,
                        contactPose,
                        0.8f + (sample.PhaseProgress * 0.2f),
                        TechniqueAction.Set,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.FollowThrough:
                    Rig.SetPoseTransition(
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
                    Rig.SetPoseWithContactError(
                        StickFigurePose.SpikeWindup,
                        Mathf.Clamp01(deltaSeconds * 10f),
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        0f);
                    return true;
                case ActionPhase.Power:
                    Rig.SetPoseTransition(
                        StickFigurePose.SpikeWindup,
                        StickFigurePose.Spike,
                        sample.PhaseProgress * 0.75f,
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.Contact when sample.PhaseProgress <= 0.5f:
                    Rig.SetPoseTransition(
                        StickFigurePose.SpikeWindup,
                        StickFigurePose.Spike,
                        0.75f + (sample.PhaseProgress * 0.5f),
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.Contact:
                    Rig.SetPoseTransition(
                        StickFigurePose.Spike,
                        StickFigurePose.Landing,
                        (sample.PhaseProgress - 0.5f) * 0.5f,
                        TechniqueAction.Attack,
                        _executionError.ContactPositionError,
                        _executionError.ContactNormalErrorDegrees,
                        errorWeight);
                    return true;
                case ActionPhase.FollowThrough:
                    Rig.SetPoseTransition(
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
                    transform.position = movementPosition;
                }

                return;
            }

            var position = EvaluateAttackPosition(simulationTime, movementPosition);
            if (sample.Phase == ActionPhase.Complete)
            {
                position.y = _motionOrigin.y;
            }

            transform.position = position;
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
                    _scheduledAction,
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
            var maximumCorrection = _scheduledAction switch
            {
                TechniqueAction.Attack => 0.70f,
                TechniqueAction.Set => 0.30f,
                _ => 0.16f
            };
            if (correction.Magnitude > maximumCorrection)
            {
                correction = correction.Normalized * maximumCorrection;
            }

            if (sample.Phase == ActionPhase.Power)
            {
                correction *= Mathf.SmoothStep(0f, 1f, sample.PhaseProgress);
            }
            transform.position += new Vector3(correction.X, correction.Y, correction.Z);
        }

        private Vector3 EvaluateAttackPosition(float simulationTime, Vector3 movementPosition)
        {
            var takeoffTime = _actionTimeline.ActualContactTime - 0.38f;
            var landingTime = _actionTimeline.ActualContactTime + 0.45f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var jumpHeight = (0.72f + (Ability.Jump * 0.5f)) * 4f * jumpProgress * (1f - jumpProgress);
            var approachStart = _actionTimeline.ActualContactTime - 0.72f;
            var approachProgress = Mathf.Clamp01((simulationTime - approachStart) / 0.55f);
            approachProgress = approachProgress * approachProgress * (3f - (2f * approachProgress));
            var approachDistance = 0.45f + (Ability.Mobility * 0.35f);
            var position = movementPosition + (_motionForward * approachDistance * approachProgress);
            position.y = _motionOrigin.y + jumpHeight;
            return position;
        }

        private void ConfigureScheduledMovement(
            Vector3 requestedTarget,
            float movementStartSimulationTime,
            float scheduledContactTime,
            TechniqueAction action)
        {
            _movementStartPosition = GroundPosition(transform.position);
            _movementStartSimulationTime = movementStartSimulationTime;
            var movementLead = action == TechniqueAction.Attack ? 0.32f : 0.10f;
            _movementEndSimulationTime = Mathf.Max(
                _movementStartSimulationTime + 0.01f,
                scheduledContactTime - movementLead);
            var availableSeconds = _movementEndSimulationTime - _movementStartSimulationTime;
            var maximumSpeed = _moveSpeed * (0.65f + (Ability.Mobility * 0.5f));
            var maximumDistance = maximumSpeed * availableSeconds;
            _movementTargetPosition = Vector3.MoveTowards(
                _movementStartPosition,
                GroundPosition(requestedTarget),
                maximumDistance);
            ScheduledMovementDistance = Vector3.Distance(
                _movementStartPosition,
                _movementTargetPosition);
            MovementShortfall = Vector3.Distance(_movementTargetPosition, requestedTarget);
            _hasScheduledMovement = Vector3.Distance(
                _movementStartPosition,
                _movementTargetPosition) > 0.01f;
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
            if (_supportAction == TechniqueAction.Block)
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
            var takeoffTime = _supportTimeline.ActualContactTime - 0.22f;
            var landingTime = _supportTimeline.ActualContactTime + 0.28f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var jumpHeight = 0.46f + (Ability.Jump * 0.30f);
            return jumpHeight * 4f * jumpProgress * (1f - jumpProgress);
        }

        private static Vector3 GroundPosition(Vector3 position)
        {
            position.y = 0f;
            return position;
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

        private static StickFigurePose ContactPoseFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => StickFigurePose.Receive,
                TechniqueAction.Set => StickFigurePose.Set,
                TechniqueAction.Attack => StickFigurePose.Spike,
                TechniqueAction.Block => StickFigurePose.Block,
                TechniqueAction.Serve => StickFigurePose.Serve,
                _ => StickFigurePose.Ready
            };
        }

        private static StickFigurePose SetContactPose(SetTechniqueStyle style)
        {
            return style switch
            {
                SetTechniqueStyle.SideLeftTwoHand => StickFigurePose.SetSideLeft,
                SetTechniqueStyle.SideRightTwoHand => StickFigurePose.SetSideRight,
                SetTechniqueStyle.BackTwoHand => StickFigurePose.SetBack,
                SetTechniqueStyle.OneHandLeft => StickFigurePose.SetOneHandLeft,
                SetTechniqueStyle.OneHandRight => StickFigurePose.SetOneHandRight,
                _ => StickFigurePose.Set
            };
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
            var speed = 0f;
            const float acceleration = 24f;
            while ((transform.position - destination).sqrMagnitude > 0.01f)
            {
                Rig.SetPose(StickFigurePose.Run, Time.deltaTime * 8f);
                var distance = Vector3.Distance(transform.position, destination);
                var brakingSpeed = Mathf.Sqrt(2f * acceleration * distance);
                var targetSpeed = Mathf.Min(_moveSpeed * (0.65f + (Ability.Mobility * 0.5f)), brakingSpeed);
                speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * Time.deltaTime);
                transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = destination;
            Rig.SetPose(StickFigurePose.Ready, 0.25f);
        }
    }
}

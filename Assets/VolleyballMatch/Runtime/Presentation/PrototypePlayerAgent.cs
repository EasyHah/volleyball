using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Domain.Prototype;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.Presentation
{
    public sealed class PrototypePlayerAgent : MonoBehaviour, IBallContactSource
    {
        [SerializeField]
        private float _moveSpeed = 7f;

        public PlayerId Id { get; private set; }

        public StickFigureRig Rig { get; private set; }

        public PlayerAbilityProfile Ability { get; private set; }

        public PlayerContactSurfaces ContactSurfaces { get; private set; }

        private ActionTimeline _actionTimeline;
        private TechniqueAction _scheduledAction;
        private SkillExecutionError _executionError;
        private SimVector3 _targetVelocity;
        private int _contactGroupId;
        private bool _hasScheduledContact;
        private Vector3 _motionOrigin;
        private Vector3 _motionForward;

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
            int contactGroupId)
        {
            _scheduledAction = action;
            _targetVelocity = targetVelocity + executionError.TargetVelocityError;
            _executionError = executionError;
            _contactGroupId = contactGroupId;
            _actionTimeline = new ActionTimeline(action, scheduledSimulationTime, executionError.ContactTimingError);
            _motionOrigin = transform.position;
            _motionForward = transform.forward;
            _hasScheduledContact = true;
        }

        public void CancelScheduledContact()
        {
            _hasScheduledContact = false;
            _actionTimeline = null;
        }

        public void CollectContacts(
            float simulationTime,
            float deltaSeconds,
            ICollection<BallContactCandidate> contacts)
        {
            if (!_hasScheduledContact || contacts == null)
            {
                return;
            }

            var sample = _actionTimeline.Sample(simulationTime);
            ApplyScheduledRootMotion(sample, simulationTime);
            ApplyScheduledPose(sample, deltaSeconds);
            var surfaces = ContactSurfaces.Capture(_scheduledAction, sample.SurfaceActive, _contactGroupId);
            var strikeDirection = _targetVelocity.SqrMagnitude > 0.000001f
                ? _targetVelocity.Normalized
                : SimVector3.Up;
            var response = ResponseFor(_scheduledAction);
            foreach (var surface in surfaces)
            {
                contacts.Add(new BallContactCandidate(
                    surface,
                    _scheduledAction,
                    Ability.TechniqueFor(_scheduledAction),
                    _targetVelocity,
                    strikeDirection,
                    response));
            }

            if (sample.Phase == ActionPhase.Complete)
            {
                CancelScheduledContact();
            }
        }

        private void ApplyScheduledPose(ActionTimelineSample sample, float deltaSeconds)
        {
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
            var blend = Mathf.Clamp01(deltaSeconds * 12f * _executionError.SurfaceSpeedScale);
            Rig.SetPoseWithContactError(
                pose,
                blend,
                _scheduledAction,
                _executionError.ContactPositionError,
                _executionError.ContactNormalErrorDegrees,
                errorWeight);
        }

        private void ApplyScheduledRootMotion(ActionTimelineSample sample, float simulationTime)
        {
            if (_scheduledAction != TechniqueAction.Attack)
            {
                return;
            }

            var takeoffTime = _actionTimeline.ActualContactTime - 0.38f;
            var landingTime = _actionTimeline.ActualContactTime + 0.45f;
            var jumpProgress = Mathf.Clamp01((simulationTime - takeoffTime) / (landingTime - takeoffTime));
            var jumpHeight = (0.72f + (Ability.Jump * 0.5f)) * 4f * jumpProgress * (1f - jumpProgress);
            var approachStart = _actionTimeline.ActualContactTime - 0.72f;
            var approachProgress = Mathf.Clamp01((simulationTime - approachStart) / 0.55f);
            approachProgress = approachProgress * approachProgress * (3f - (2f * approachProgress));
            var approachDistance = 0.45f + (Ability.Mobility * 0.35f);
            var position = _motionOrigin + (_motionForward * approachDistance * approachProgress);
            position.y = _motionOrigin.y + jumpHeight;
            if (sample.Phase == ActionPhase.Complete)
            {
                position.y = _motionOrigin.y;
            }

            transform.position = position;
        }

        private static ContactResponseParameters ResponseFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new ContactResponseParameters(0.85f, 1f, 0.12f, 0.08f),
                TechniqueAction.Set => new ContactResponseParameters(0.75f, 1f, 0.08f, 0.08f),
                TechniqueAction.Attack => new ContactResponseParameters(0.72f, 1f, 0.18f, 0.08f),
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

using System;
using VolleyballMatch.Domain.Players;

namespace VolleyballMatch.Presentation
{
    public enum ActionPhase
    {
        Prepare,
        Power,
        Contact,
        FollowThrough,
        Recover,
        Complete
    }

    public readonly struct ActionTimelineSample
    {
        public ActionTimelineSample(
            ActionPhase phase,
            float phaseProgress,
            float relativeToContact,
            float contactWeight,
            bool surfaceActive)
        {
            Phase = phase;
            PhaseProgress = phaseProgress;
            RelativeToContact = relativeToContact;
            ContactWeight = contactWeight;
            SurfaceActive = surfaceActive;
        }

        public ActionPhase Phase { get; }

        public float PhaseProgress { get; }

        public float RelativeToContact { get; }

        public float ContactWeight { get; }

        public bool SurfaceActive { get; }
    }

    public sealed class ActionTimeline
    {
        private const float ActivateThreshold = 0.6f;
        private const float DeactivateThreshold = 0.4f;
        private readonly TimelineProfile _profile;
        private bool _surfaceActive;

        public ActionTimeline(TechniqueAction action, float scheduledContactTime, float contactTimingError = 0f)
        {
            if (!IsFinite(scheduledContactTime) || !IsFinite(contactTimingError))
            {
                throw new ArgumentOutOfRangeException("Timeline times must be finite.");
            }

            Action = action;
            ScheduledContactTime = scheduledContactTime;
            ActualContactTime = scheduledContactTime + contactTimingError;
            _profile = ProfileFor(action);
        }

        public TechniqueAction Action { get; }

        public float ScheduledContactTime { get; }

        public float ActualContactTime { get; }

        public ActionTimelineSample Sample(float simulationTime)
        {
            if (!IsFinite(simulationTime))
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTime));
            }

            var relative = simulationTime - ActualContactTime;
            var contactWeight = Clamp01(1f - (Math.Abs(relative) / _profile.ActivationHalfWidth));
            if (!_surfaceActive && contactWeight + 0.00001f >= ActivateThreshold)
            {
                _surfaceActive = true;
            }
            else if (_surfaceActive && contactWeight <= DeactivateThreshold)
            {
                _surfaceActive = false;
            }

            var phase = ResolvePhase(relative);
            var progress = ResolveProgress(phase, relative);
            return new ActionTimelineSample(phase, progress, relative, contactWeight, _surfaceActive);
        }

        private ActionPhase ResolvePhase(float relative)
        {
            if (relative < -_profile.PowerDuration)
            {
                return ActionPhase.Prepare;
            }

            if (relative < -_profile.ContactHalfWidth)
            {
                return ActionPhase.Power;
            }

            if (relative <= _profile.ContactHalfWidth)
            {
                return ActionPhase.Contact;
            }

            if (relative <= _profile.FollowThroughDuration)
            {
                return ActionPhase.FollowThrough;
            }

            if (relative <= _profile.RecoveryDuration)
            {
                return ActionPhase.Recover;
            }

            return ActionPhase.Complete;
        }

        private float ResolveProgress(ActionPhase phase, float relative)
        {
            return phase switch
            {
                ActionPhase.Prepare => Clamp01((relative + _profile.PreparationDuration) /
                                                (_profile.PreparationDuration - _profile.PowerDuration)),
                ActionPhase.Power => Clamp01((relative + _profile.PowerDuration) /
                                              (_profile.PowerDuration - _profile.ContactHalfWidth)),
                ActionPhase.Contact => Clamp01((relative + _profile.ContactHalfWidth) /
                                                (_profile.ContactHalfWidth * 2f)),
                ActionPhase.FollowThrough => Clamp01((relative - _profile.ContactHalfWidth) /
                                                      (_profile.FollowThroughDuration - _profile.ContactHalfWidth)),
                ActionPhase.Recover => Clamp01((relative - _profile.FollowThroughDuration) /
                                                (_profile.RecoveryDuration - _profile.FollowThroughDuration)),
                _ => 1f
            };
        }

        private static TimelineProfile ProfileFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new TimelineProfile(0.48f, 0.14f, 0.04f, 0.20f, 0.48f, 0.10f),
                TechniqueAction.Set => new TimelineProfile(0.55f, 0.16f, 0.04f, 0.22f, 0.50f, 0.10f),
                TechniqueAction.Attack => new TimelineProfile(0.75f, 0.18f, 0.04f, 0.30f, 0.65f, 0.10f),
                TechniqueAction.Block => new TimelineProfile(0.50f, 0.14f, 0.04f, 0.22f, 0.55f, 0.10f),
                TechniqueAction.Serve => new TimelineProfile(0.65f, 0.18f, 0.04f, 0.28f, 0.60f, 0.10f),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct TimelineProfile
        {
            public TimelineProfile(
                float preparationDuration,
                float powerDuration,
                float contactHalfWidth,
                float followThroughDuration,
                float recoveryDuration,
                float activationHalfWidth)
            {
                PreparationDuration = preparationDuration;
                PowerDuration = powerDuration;
                ContactHalfWidth = contactHalfWidth;
                FollowThroughDuration = followThroughDuration;
                RecoveryDuration = recoveryDuration;
                ActivationHalfWidth = activationHalfWidth;
            }

            public float PreparationDuration { get; }

            public float PowerDuration { get; }

            public float ContactHalfWidth { get; }

            public float FollowThroughDuration { get; }

            public float RecoveryDuration { get; }

            public float ActivationHalfWidth { get; }
        }
    }
}

using System;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    /// <summary>Owns the player's visual rig and all pose-only mutations.</summary>
    public sealed class PlayerPresentation
    {
        public PlayerPresentation(Transform playerRoot, Color teamColor, string jerseyNumber)
        {
            Rig = StickFigureRig.Create(playerRoot, teamColor, jerseyNumber);
        }

        public StickFigureRig Rig { get; }

        public void ApplyPose(TechniqueAction action, SetTechniqueStyle setStyle, float normalizedBlend)
        {
            Rig.SetPose(ContactPoseFor(action, setStyle), normalizedBlend);
        }

        public void SetPose(StickFigurePose pose, float normalizedBlend)
        {
            Rig.SetPose(pose, normalizedBlend);
        }

        public void SetPoseWithContactError(
            StickFigurePose pose,
            float normalizedBlend,
            TechniqueAction action,
            SimVector3 positionError,
            SimVector3 normalErrorDegrees,
            float errorWeight)
        {
            Rig.SetPoseWithContactError(
                pose,
                normalizedBlend,
                action,
                positionError,
                normalErrorDegrees,
                errorWeight);
        }

        public void SetPoseTransition(
            StickFigurePose from,
            StickFigurePose to,
            float normalizedProgress,
            TechniqueAction action,
            SimVector3 positionError,
            SimVector3 normalErrorDegrees,
            float errorWeight)
        {
            Rig.SetPoseTransition(
                from,
                to,
                normalizedProgress,
                action,
                positionError,
                normalErrorDegrees,
                errorWeight);
        }

        /// <summary>Maps immutable action samples to rig poses; callers never select poses.</summary>
        internal void ApplyScheduledPose(
            ActionTimelineSample sample,
            TechniqueAction action,
            bool controlledHandling,
            SetTechniqueStyle setStyle,
            SkillExecutionError error,
            bool moving,
            float deltaSeconds)
        {
            var errorWeight = sample.Phase == ActionPhase.Power || sample.Phase == ActionPhase.Contact
                ? 1f : sample.Phase == ActionPhase.FollowThrough ? 1f - sample.PhaseProgress : 0f;
            if (moving && sample.Phase == ActionPhase.Prepare)
            {
                SetPose(StickFigurePose.Run, Mathf.Clamp01(deltaSeconds * 12f));
                return;
            }
            if (controlledHandling)
            {
                SetPoseWithContactError(
                    sample.Phase is ActionPhase.Recover or ActionPhase.Complete ? StickFigurePose.Ready : StickFigurePose.Set,
                    Mathf.Clamp01(deltaSeconds * 14f), TechniqueAction.Set,
                    error.ContactPositionError, error.ContactNormalErrorDegrees, sample.SurfaceActive ? 1f : 0f);
                return;
            }
            if (action == TechniqueAction.Set && ApplySetPose(sample, setStyle, error, errorWeight, deltaSeconds)) return;
            if (action == TechniqueAction.Attack && ApplyAttackPose(sample, error, errorWeight, deltaSeconds)) return;
            var pose = action switch
            {
                TechniqueAction.Receive => StickFigurePose.Receive,
                TechniqueAction.Set => StickFigurePose.Set,
                TechniqueAction.Attack => sample.Phase == ActionPhase.Prepare ? StickFigurePose.Approach :
                    sample.Phase is ActionPhase.FollowThrough or ActionPhase.Recover ? StickFigurePose.Landing : StickFigurePose.Spike,
                TechniqueAction.Block => StickFigurePose.Block,
                TechniqueAction.Serve => StickFigurePose.Serve,
                _ => StickFigurePose.Ready
            };
            if (sample.Phase is ActionPhase.Recover or ActionPhase.Complete) pose = StickFigurePose.Ready;
            SetPoseWithContactError(pose, Mathf.Clamp01(deltaSeconds * 18f * error.SurfaceSpeedScale), action,
                error.ContactPositionError, error.ContactNormalErrorDegrees, errorWeight);
        }

        internal void ApplySupportPose(ActionTimelineSample sample, TechniqueAction action, float deltaSeconds)
        {
            var pose = action switch
            {
                TechniqueAction.Block => StickFigurePose.Block,
                TechniqueAction.Attack or TechniqueAction.Set => StickFigurePose.Run,
                _ => StickFigurePose.Receive
            };
            if (sample.Phase == ActionPhase.Prepare && action == TechniqueAction.Receive) pose = StickFigurePose.Run;
            if (sample.Phase is ActionPhase.Recover or ActionPhase.Complete) pose = StickFigurePose.Ready;
            SetPose(pose, Mathf.Clamp01(deltaSeconds * 12f));
        }

        internal void ApplyEmergencyReceivePose(float deltaSeconds) =>
            SetPose(StickFigurePose.Receive, Mathf.Clamp01(deltaSeconds * 18f));

        internal void ApplyMovePose(bool moving, float blend) =>
            SetPose(moving ? StickFigurePose.Run : StickFigurePose.Ready, blend);

        internal void ApplyReadyPose() => SetPose(StickFigurePose.Ready, 1f);

        internal T WithPreviewBlockPose<T>(Func<T> capture) => WithPreviewPose(StickFigurePose.Block, capture);

        private bool ApplySetPose(ActionTimelineSample sample, SetTechniqueStyle style, SkillExecutionError error, float weight, float dt)
        {
            var contact = SetContactPoseFor(style);
            switch (sample.Phase)
            {
                case ActionPhase.Prepare: SetPoseWithContactError(StickFigurePose.SetDraw, Mathf.Clamp01(dt * 12f), TechniqueAction.Set, error.ContactPositionError, error.ContactNormalErrorDegrees, 0f); return true;
                case ActionPhase.Power: SetPoseTransition(StickFigurePose.SetDraw, contact, sample.PhaseProgress * .8f, TechniqueAction.Set, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                case ActionPhase.Contact: SetPoseTransition(StickFigurePose.SetDraw, contact, .8f + sample.PhaseProgress * .2f, TechniqueAction.Set, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                case ActionPhase.FollowThrough: SetPoseTransition(contact, StickFigurePose.Ready, sample.PhaseProgress, TechniqueAction.Set, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                default: return false;
            }
        }

        private bool ApplyAttackPose(ActionTimelineSample sample, SkillExecutionError error, float weight, float dt)
        {
            switch (sample.Phase)
            {
                case ActionPhase.Prepare: SetPoseWithContactError(StickFigurePose.SpikeWindup, Mathf.Clamp01(dt * 10f), TechniqueAction.Attack, error.ContactPositionError, error.ContactNormalErrorDegrees, 0f); return true;
                case ActionPhase.Power: SetPoseTransition(StickFigurePose.SpikeWindup, StickFigurePose.Spike, sample.PhaseProgress, TechniqueAction.Attack, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                case ActionPhase.Contact when sample.PhaseProgress <= .5f: SetPoseTransition(StickFigurePose.SpikeWindup, StickFigurePose.Spike, 1f, TechniqueAction.Attack, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                case ActionPhase.Contact: SetPoseTransition(StickFigurePose.Spike, StickFigurePose.Landing, (sample.PhaseProgress - .5f) * .5f, TechniqueAction.Attack, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                case ActionPhase.FollowThrough: SetPoseTransition(StickFigurePose.Spike, StickFigurePose.Landing, .25f + sample.PhaseProgress * .75f, TechniqueAction.Attack, error.ContactPositionError, error.ContactNormalErrorDegrees, weight); return true;
                default: return false;
            }
        }

        public void WithPreviewPose(TechniqueAction action, SetTechniqueStyle setStyle, Action capture)
        {
            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            var rotations = Rig.CaptureLocalRotations();
            try
            {
                ApplyPose(action, setStyle, 1f);
                capture();
            }
            finally
            {
                Rig.RestoreLocalRotations(rotations);
            }
        }

        public void WithPreviewPose(StickFigurePose pose, Action capture)
        {
            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            var rotations = Rig.CaptureLocalRotations();
            try
            {
                SetPose(pose, 1f);
                capture();
            }
            finally
            {
                Rig.RestoreLocalRotations(rotations);
            }
        }

        public T WithPreviewPose<T>(TechniqueAction action, SetTechniqueStyle setStyle, Func<T> capture)
        {
            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            var rotations = Rig.CaptureLocalRotations();
            try
            {
                ApplyPose(action, setStyle, 1f);
                return capture();
            }
            finally
            {
                Rig.RestoreLocalRotations(rotations);
            }
        }

        public T WithPreviewPose<T>(StickFigurePose pose, Func<T> capture)
        {
            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            var rotations = Rig.CaptureLocalRotations();
            try
            {
                SetPose(pose, 1f);
                return capture();
            }
            finally
            {
                Rig.RestoreLocalRotations(rotations);
            }
        }

        public StickFigurePose SetContactPose(SetTechniqueStyle style)
        {
            return SetContactPoseFor(style);
        }

        private static StickFigurePose ContactPoseFor(TechniqueAction action, SetTechniqueStyle setStyle)
        {
            if (action == TechniqueAction.Set)
            {
                return SetContactPoseFor(setStyle);
            }

            return action switch
            {
                TechniqueAction.Receive => StickFigurePose.Receive,
                TechniqueAction.Attack => StickFigurePose.Spike,
                TechniqueAction.Block => StickFigurePose.Block,
                TechniqueAction.Serve => StickFigurePose.Serve,
                _ => StickFigurePose.Ready
            };
        }

        private static StickFigurePose SetContactPoseFor(SetTechniqueStyle style)
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
    }
}

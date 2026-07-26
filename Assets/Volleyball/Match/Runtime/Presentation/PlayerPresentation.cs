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

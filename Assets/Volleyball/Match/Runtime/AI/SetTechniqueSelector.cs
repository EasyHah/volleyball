using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public enum SetTechniqueStyle
    {
        FrontTwoHand,
        SideLeftTwoHand,
        SideRightTwoHand,
        BackTwoHand,
        OneHandLeft,
        OneHandRight
    }

    public readonly struct SetTechniqueDecision
    {
        public SetTechniqueDecision(
            SetTechniqueStyle requestedStyle,
            SetTechniqueStyle executedStyle,
            float controlScale)
        {
            RequestedStyle = requestedStyle;
            ExecutedStyle = executedStyle;
            ControlScale = controlScale;
        }

        public SetTechniqueStyle RequestedStyle { get; }

        public SetTechniqueStyle ExecutedStyle { get; }

        public float ControlScale { get; }

        public bool WasLimited => RequestedStyle != ExecutedStyle;
    }

    public static class SetTechniqueSelector
    {
        private const float SideSetMinimum = 0.55f;
        private const float BackSetMinimum = 0.78f;
        private const float OneHandMinimum = 0.90f;

        public static SetTechniqueDecision Select(
            SimVector3 localTargetVelocity,
            float setTechnique,
            bool emergencyOneHand = false)
        {
            if (!localTargetVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(localTargetVelocity));
            }

            if (float.IsNaN(setTechnique) || float.IsInfinity(setTechnique) ||
                setTechnique < 0f || setTechnique > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(setTechnique));
            }

            var requested = RequestedStyle(localTargetVelocity, emergencyOneHand);
            var executed = ResolveAvailableStyle(requested, localTargetVelocity, setTechnique);
            var difficultyScale = DifficultyScale(executed, setTechnique);
            if (requested != executed)
            {
                difficultyScale *= 0.65f;
            }

            return new SetTechniqueDecision(requested, executed, difficultyScale);
        }

        private static SetTechniqueStyle RequestedStyle(
            SimVector3 localTargetVelocity,
            bool emergencyOneHand)
        {
            if (emergencyOneHand)
            {
                return localTargetVelocity.X < 0f
                    ? SetTechniqueStyle.OneHandLeft
                    : SetTechniqueStyle.OneHandRight;
            }

            var horizontalMagnitude = (float)Math.Sqrt(
                (localTargetVelocity.X * localTargetVelocity.X) +
                (localTargetVelocity.Z * localTargetVelocity.Z));
            if (horizontalMagnitude <= 0.0001f)
            {
                return SetTechniqueStyle.FrontTwoHand;
            }

            if (localTargetVelocity.Z < -horizontalMagnitude * 0.15f)
            {
                return SetTechniqueStyle.BackTwoHand;
            }

            if (Math.Abs(localTargetVelocity.X) > Math.Abs(localTargetVelocity.Z) * 0.70f)
            {
                return localTargetVelocity.X < 0f
                    ? SetTechniqueStyle.SideLeftTwoHand
                    : SetTechniqueStyle.SideRightTwoHand;
            }

            return SetTechniqueStyle.FrontTwoHand;
        }

        private static SetTechniqueStyle ResolveAvailableStyle(
            SetTechniqueStyle requested,
            SimVector3 localTargetVelocity,
            float setTechnique)
        {
            switch (requested)
            {
                case SetTechniqueStyle.SideLeftTwoHand:
                case SetTechniqueStyle.SideRightTwoHand:
                    return setTechnique >= SideSetMinimum
                        ? requested
                        : SetTechniqueStyle.FrontTwoHand;
                case SetTechniqueStyle.BackTwoHand:
                    if (setTechnique >= BackSetMinimum)
                    {
                        return requested;
                    }

                    if (setTechnique >= SideSetMinimum && Math.Abs(localTargetVelocity.X) > 0.5f)
                    {
                        return localTargetVelocity.X < 0f
                            ? SetTechniqueStyle.SideLeftTwoHand
                            : SetTechniqueStyle.SideRightTwoHand;
                    }

                    return SetTechniqueStyle.FrontTwoHand;
                case SetTechniqueStyle.OneHandLeft:
                case SetTechniqueStyle.OneHandRight:
                    if (setTechnique >= OneHandMinimum)
                    {
                        return requested;
                    }

                    if (setTechnique >= SideSetMinimum)
                    {
                        return requested == SetTechniqueStyle.OneHandLeft
                            ? SetTechniqueStyle.SideLeftTwoHand
                            : SetTechniqueStyle.SideRightTwoHand;
                    }

                    return SetTechniqueStyle.FrontTwoHand;
                default:
                    return SetTechniqueStyle.FrontTwoHand;
            }
        }

        private static float DifficultyScale(SetTechniqueStyle style, float setTechnique)
        {
            return style switch
            {
                SetTechniqueStyle.SideLeftTwoHand => 0.82f + (setTechnique * 0.18f),
                SetTechniqueStyle.SideRightTwoHand => 0.82f + (setTechnique * 0.18f),
                SetTechniqueStyle.BackTwoHand => 0.72f + (setTechnique * 0.28f),
                SetTechniqueStyle.OneHandLeft => 0.62f + (setTechnique * 0.38f),
                SetTechniqueStyle.OneHandRight => 0.62f + (setTechnique * 0.38f),
                _ => 1f
            };
        }
    }
}

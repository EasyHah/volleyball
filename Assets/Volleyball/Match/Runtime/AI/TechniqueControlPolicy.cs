using System;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public readonly struct TechniqueControlInput
    {
        public TechniqueControlInput(
            TechniqueAction action,
            SimVector3 physicalOutgoing,
            SimVector3 targetVelocity,
            SimVector3 strikeDirection,
            float playerTechnique,
            float contactQuality)
        {
            if (!physicalOutgoing.IsFinite || !targetVelocity.IsFinite || !strikeDirection.IsFinite ||
                strikeDirection.SqrMagnitude <= 0.000001f)
            {
                throw new ArgumentException("Technique-control vectors must be finite and strike direction must be non-zero.");
            }

            Action = action;
            PhysicalOutgoing = physicalOutgoing;
            TargetVelocity = targetVelocity;
            StrikeDirection = strikeDirection.Normalized;
            PlayerTechnique = ValidateUnit(playerTechnique, nameof(playerTechnique));
            ContactQuality = ValidateUnit(contactQuality, nameof(contactQuality));
        }

        public TechniqueAction Action { get; }

        public SimVector3 PhysicalOutgoing { get; }

        public SimVector3 TargetVelocity { get; }

        public SimVector3 StrikeDirection { get; }

        public float PlayerTechnique { get; }

        public float ContactQuality { get; }

        private static float ValidateUnit(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be in the range [0, 1].");
            }

            return value;
        }
    }

    public readonly struct TechniqueControlResult
    {
        public TechniqueControlResult(
            SimVector3 physicalOutgoing,
            SimVector3 requestedTarget,
            SimVector3 constrainedTarget,
            SimVector3 finalOutgoing,
            float appliedControl)
        {
            PhysicalOutgoing = physicalOutgoing;
            RequestedTarget = requestedTarget;
            ConstrainedTarget = constrainedTarget;
            FinalOutgoing = finalOutgoing;
            AppliedControl = appliedControl;
        }

        public SimVector3 PhysicalOutgoing { get; }

        public SimVector3 RequestedTarget { get; }

        public SimVector3 ConstrainedTarget { get; }

        public SimVector3 FinalOutgoing { get; }

        public float AppliedControl { get; }
    }

    public static class TechniqueControlPolicy
    {
        public static float MaximumControlFor(TechniqueAction action)
        {
            return ProfileFor(action).MaximumControl;
        }

        public static TechniqueControlResult Apply(TechniqueControlInput input)
        {
            var profile = ProfileFor(input.Action);
            var contactControl = input.Action == TechniqueAction.Receive ||
                                 input.Action == TechniqueAction.Set
                ? (float)Math.Sqrt(input.ContactQuality)
                : input.ContactQuality;
            var appliedControl = profile.MaximumControl * input.PlayerTechnique * contactControl;
            if (appliedControl <= 0f)
            {
                return new TechniqueControlResult(
                    input.PhysicalOutgoing,
                    input.TargetVelocity,
                    input.PhysicalOutgoing,
                    input.PhysicalOutgoing,
                    0f);
            }

            var physicalSpeed = input.PhysicalOutgoing.Magnitude;
            var physicalDirection = physicalSpeed > 0.000001f
                ? input.PhysicalOutgoing / physicalSpeed
                : input.StrikeDirection;
            var requestedSpeed = input.TargetVelocity.Magnitude;
            var requestedDirection = requestedSpeed > 0.000001f
                ? input.TargetVelocity / requestedSpeed
                : physicalDirection;

            if (SimVector3.Dot(requestedDirection, input.StrikeDirection) <= 0f)
            {
                requestedDirection = input.StrikeDirection;
            }

            var maximumDirectionCorrection = profile.MaximumDirectionCorrectionDegrees;
            if (input.Action == TechniqueAction.Attack && input.ContactQuality > 0.75f)
            {
                var centeredControl = Math.Min(1f, (input.ContactQuality - 0.75f) / 0.25f);
                maximumDirectionCorrection += (180f - maximumDirectionCorrection) * centeredControl;
            }

            var constrainedDirection = RotateTowards(
                physicalDirection,
                requestedDirection,
                maximumDirectionCorrection);
            var minimumSpeed = Math.Max(0f, physicalSpeed - profile.MaximumSpeedChange);
            var maximumSpeed = Math.Min(profile.MaximumOutgoingSpeed, physicalSpeed + profile.MaximumSpeedChange);
            var constrainedSpeed = Math.Max(minimumSpeed, Math.Min(maximumSpeed, requestedSpeed));
            var constrainedTarget = constrainedDirection * constrainedSpeed;
            var finalOutgoing = SimVector3.Lerp(input.PhysicalOutgoing, constrainedTarget, appliedControl);
            if (finalOutgoing.Magnitude > profile.MaximumOutgoingSpeed)
            {
                finalOutgoing = finalOutgoing.Normalized * profile.MaximumOutgoingSpeed;
            }

            return new TechniqueControlResult(
                input.PhysicalOutgoing,
                input.TargetVelocity,
                constrainedTarget,
                finalOutgoing,
                appliedControl);
        }

        private static SimVector3 RotateTowards(SimVector3 from, SimVector3 to, float maximumDegrees)
        {
            var dot = Math.Max(-1f, Math.Min(1f, SimVector3.Dot(from, to)));
            var angle = (float)Math.Acos(dot);
            var maximumRadians = maximumDegrees * ((float)Math.PI / 180f);
            if (angle <= maximumRadians || angle <= 0.000001f)
            {
                return to;
            }

            var alpha = maximumRadians / angle;
            var sine = (float)Math.Sin(angle);
            if (Math.Abs(sine) <= 0.000001f)
            {
                return SimVector3.Lerp(from, to, alpha).Normalized;
            }

            return (((float)Math.Sin((1f - alpha) * angle) / sine) * from +
                    ((float)Math.Sin(alpha * angle) / sine) * to).Normalized;
        }

        private static TechniqueControlProfile ProfileFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new TechniqueControlProfile(1f, 180f, 30f, 30f),
                TechniqueAction.Set => new TechniqueControlProfile(1f, 180f, 30f, 30f),
                TechniqueAction.Attack => new TechniqueControlProfile(1f, 60f, 24f, 30f),
                TechniqueAction.Block => new TechniqueControlProfile(0.05f, 5f, 2f, 24f),
                TechniqueAction.Serve => new TechniqueControlProfile(0.35f, 18f, 7f, 24f),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private readonly struct TechniqueControlProfile
        {
            public TechniqueControlProfile(
                float maximumControl,
                float maximumDirectionCorrectionDegrees,
                float maximumSpeedChange,
                float maximumOutgoingSpeed)
            {
                MaximumControl = maximumControl;
                MaximumDirectionCorrectionDegrees = maximumDirectionCorrectionDegrees;
                MaximumSpeedChange = maximumSpeedChange;
                MaximumOutgoingSpeed = maximumOutgoingSpeed;
            }

            public float MaximumControl { get; }

            public float MaximumDirectionCorrectionDegrees { get; }

            public float MaximumSpeedChange { get; }

            public float MaximumOutgoingSpeed { get; }
        }
    }
}

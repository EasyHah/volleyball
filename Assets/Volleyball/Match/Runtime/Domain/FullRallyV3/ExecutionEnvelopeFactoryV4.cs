using System;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public static class ExecutionEnvelopeFactoryV4
    {
        public static ExecutionEnvelopeV4 Create(
            DerivedMatchAttributesV4 derivedAttributes,
            ExecutionIntentV4 intent,
            string samplingKey,
            ExecutionEnvelopePolicyV4 policy)
        {
            if (derivedAttributes == null)
            {
                throw new ArgumentNullException(nameof(derivedAttributes));
            }

            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            if (string.IsNullOrWhiteSpace(samplingKey))
            {
                throw new ArgumentException("Sampling key is required.", nameof(samplingKey));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            if (!policy.ContainsCandidateCategory(intent.CandidateCategory))
            {
                throw new ArgumentException(
                    "Intent candidate category is absent from the deterministic policy order.",
                    nameof(intent));
            }

            ResolveAuthority(
                derivedAttributes.Attributes,
                intent.CandidateCategory,
                out var directionControl,
                out var speedControl,
                out var powerCapacity,
                out var baseMaximumSpeed);

            // Direction and speed controls own only their respective bounded
            // errors. Power capacity owns only maximum speed and effort.
            var targetErrorExtent = 0.03f + ((1f - directionControl) * 0.19f);
            var velocityErrorExtent = 0.12f + ((1f - speedControl) * 1.38f);
            var maximumSpeed = baseMaximumSpeed * (0.6f + (0.4f * powerCapacity));
            var maximumEffort = 0.6f + (0.4f * powerCapacity);

            var targetExtent = new SimVector3(
                targetErrorExtent,
                targetErrorExtent,
                targetErrorExtent);
            var velocityExtent = new SimVector3(
                velocityErrorExtent,
                velocityErrorExtent,
                velocityErrorExtent);
            var maximumVelocity = new SimVector3(
                maximumSpeed,
                maximumSpeed,
                maximumSpeed);
            if (intent.RequestedEffort > maximumEffort)
            {
                throw new ArgumentException(
                    "Requested effort exceeds the candidate's V4 power capacity.",
                    nameof(intent));
            }

            if (Math.Abs(intent.BaselineVelocity.X) > maximumVelocity.X ||
                Math.Abs(intent.BaselineVelocity.Y) > maximumVelocity.Y ||
                Math.Abs(intent.BaselineVelocity.Z) > maximumVelocity.Z)
            {
                throw new ArgumentException(
                    "Baseline velocity exceeds the candidate's V4 power capacity.",
                    nameof(intent));
            }

            return new ExecutionEnvelopeV4(
                policy.EnvelopeVersion,
                derivedAttributes.ResultFingerprint,
                derivedAttributes.ToCanonicalBytes(),
                intent.Identity,
                intent.CandidateCategory,
                intent.BaselineTarget,
                intent.BaselineVelocity,
                maximumVelocity,
                new BoundedErrorDistributionV4(
                    policy.TargetDistributionKind,
                    -targetExtent,
                    targetExtent),
                new BoundedErrorDistributionV4(
                    policy.VelocityDistributionKind,
                    -velocityExtent,
                    velocityExtent),
                intent.RequestedEffort,
                maximumEffort,
                new SamplingContractV4(samplingKey, policy),
                new EnvelopeExpansionPolicyV4(
                    policy.MaximumExpansionCount,
                    policy.AllowedExpansionCount,
                    currentExpansionCount: 0,
                    policy.PerStepExpansionFactor),
                policy);
        }

        internal static ExecutionEnvelopeV4 ExpandOneStep(ExecutionEnvelopeV4 envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (!envelope.Expansion.IsNextExpansionExplicitlyAllowed)
            {
                throw new InvalidOperationException(
                    "The next envelope expansion is not explicitly allowed.");
            }

            return new ExecutionEnvelopeV4(
                envelope.Version,
                envelope.DerivedAttributesFingerprint,
                envelope.DerivedAttributesCanonicalBytes,
                envelope.SourceIntentIdentity,
                envelope.CandidateCategory,
                envelope.BaselineTarget,
                envelope.BaselineVelocity,
                envelope.MaximumVelocity,
                envelope.TargetError,
                envelope.VelocityError,
                envelope.RequestedEffort,
                envelope.MaximumEffort,
                envelope.Sampling,
                new EnvelopeExpansionPolicyV4(
                    envelope.Expansion.MaximumExpansionCount,
                    envelope.Expansion.AllowedExpansionCount,
                    envelope.Expansion.CurrentExpansionCount + 1,
                    envelope.Expansion.PerStepExpansionFactor),
                envelope.Policy);
        }

        private static void ResolveAuthority(
            MatchAttributesV4 attributes,
            ExecutionCandidateCategoryV4 category,
            out float directionControl,
            out float speedControl,
            out float powerCapacity,
            out float baseMaximumSpeed)
        {
            switch (category)
            {
                case ExecutionCandidateCategoryV4.Receive:
                    directionControl = attributes.Receive.FirstTouchControl;
                    speedControl = attributes.Receive.FirstTouchControl;
                    powerCapacity = attributes.Receive.Movement;
                    baseMaximumSpeed = 14f;
                    return;
                case ExecutionCandidateCategoryV4.Set:
                    directionControl = attributes.Set.PlacementControl;
                    speedControl = attributes.Set.TempoControl;
                    powerCapacity = attributes.Set.Movement;
                    baseMaximumSpeed = 16f;
                    return;
                case ExecutionCandidateCategoryV4.Attack:
                    directionControl = attributes.Attack.DirectionControl;
                    speedControl = attributes.Attack.SpeedControl;
                    powerCapacity = attributes.Attack.PowerCapacity;
                    baseMaximumSpeed = 32f;
                    return;
                case ExecutionCandidateCategoryV4.Block:
                    directionControl = attributes.Block.HandControl;
                    speedControl = attributes.Block.Timing;
                    powerCapacity = attributes.Block.LateralMobility;
                    baseMaximumSpeed = 12f;
                    return;
                case ExecutionCandidateCategoryV4.Serve:
                    directionControl = attributes.Serve.DirectionControl;
                    speedControl = attributes.Serve.SpeedControl;
                    powerCapacity = attributes.Serve.PowerCapacity;
                    baseMaximumSpeed = 30f;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }
    }
}

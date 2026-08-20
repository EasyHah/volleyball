using System;
using System.Collections.Generic;
using System.Text;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public static class ExecutionEnvelopeFactoryV4
    {
        // V4 callers project at the legacy Shared boundary; runtime logic only
        // receives the version-neutral snapshot overload below.
        public static ExecutionEnvelopeV4 Create(
            Volleyball.Shared.Contracts.DerivedMatchAttributesV4 derivedAttributes,
            ExecutionIntentV4 intent,
            string samplingKey,
            ExecutionEnvelopePolicyV4 policy)
        {
            return Create(MatchAbilitySnapshot.FromV4(derivedAttributes), intent,
                samplingKey, policy);
        }

        public static ExecutionEnvelopeV4 Create(
            Volleyball.Shared.Contracts.DerivedMatchAttributesV5 derivedAttributes,
            ExecutionIntentV4 intent,
            string samplingKey,
            ExecutionEnvelopePolicyV4 policy)
        {
            return Create(MatchAbilitySnapshot.FromV5(derivedAttributes), intent,
                samplingKey, policy);
        }

        public static ExecutionEnvelopeV4 Create(
            MatchAbilitySnapshot attributes,
            ExecutionIntentV4 intent,
            string samplingKey,
            ExecutionEnvelopePolicyV4 policy)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
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
                attributes,
                intent.CandidateCategory,
                out var directionControl,
                out var speedControl,
                out var powerCapacity,
                out var baseMaximumSpeed,
                out var abilityConsumptions);

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
                attributes.Fingerprint,
                Encoding.UTF8.GetBytes(attributes.Fingerprint),
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
                policy,
                abilityConsumptions);
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
                envelope.Policy,
                envelope.AbilityConsumptions);
        }

        private static void ResolveAuthority(
            MatchAbilitySnapshot attributes,
            ExecutionCandidateCategoryV4 category,
            out float directionControl,
            out float speedControl,
            out float powerCapacity,
            out float baseMaximumSpeed,
            out IReadOnlyList<ExecutionAbilityConsumptionV4>
                abilityConsumptions)
        {
            switch (category)
            {
                case ExecutionCandidateCategoryV4.Receive:
                    var firstTouchControl =
                        attributes.ReceiveControl;
                    var receiveMovement = attributes.ReceiveMovement;
                    directionControl = firstTouchControl;
                    speedControl = firstTouchControl;
                    powerCapacity = receiveMovement;
                    baseMaximumSpeed = 14f;
                    abilityConsumptions = Consumptions(
                        ("Receive.FirstTouchControl", firstTouchControl),
                        ("Receive.Movement", receiveMovement));
                    return;
                case ExecutionCandidateCategoryV4.Set:
                    var placementControl = attributes.SetPlacementControl;
                    var tempoControl = attributes.SetTempoControl;
                    var setMovement = attributes.SetMovement;
                    directionControl = placementControl;
                    speedControl = tempoControl;
                    powerCapacity = setMovement;
                    baseMaximumSpeed = 16f;
                    abilityConsumptions = Consumptions(
                        ("Set.PlacementControl", placementControl),
                        ("Set.TempoControl", tempoControl),
                        ("Set.Movement", setMovement));
                    return;
                case ExecutionCandidateCategoryV4.Attack:
                    var attackDirection = attributes.AttackDirectionControl;
                    var attackSpeed = attributes.AttackSpeedControl;
                    var attackPower = attributes.AttackPowerCapacity;
                    directionControl = attackDirection;
                    speedControl = attackSpeed;
                    powerCapacity = attackPower;
                    baseMaximumSpeed = 32f;
                    abilityConsumptions = Consumptions(
                        ("Attack.DirectionControl", attackDirection),
                        ("Attack.SpeedControl", attackSpeed),
                        ("Attack.PowerCapacity", attackPower));
                    return;
                case ExecutionCandidateCategoryV4.Block:
                    var handControl = attributes.BlockHandControl;
                    var blockTiming = attributes.BlockTiming;
                    var lateralMobility = attributes.BlockLateralMobility;
                    directionControl = handControl;
                    speedControl = blockTiming;
                    powerCapacity = lateralMobility;
                    baseMaximumSpeed = 12f;
                    abilityConsumptions = Consumptions(
                        ("Block.HandControl", handControl),
                        ("Block.Timing", blockTiming),
                        ("Block.LateralMobility", lateralMobility));
                    return;
                case ExecutionCandidateCategoryV4.Serve:
                    var serveDirection = attributes.ServeDirectionControl;
                    var serveSpeed = attributes.ServeSpeedControl;
                    var servePower = attributes.ServePowerCapacity;
                    directionControl = serveDirection;
                    speedControl = serveSpeed;
                    powerCapacity = servePower;
                    baseMaximumSpeed = 30f;
                    abilityConsumptions = Consumptions(
                        ("Serve.DirectionControl", serveDirection),
                        ("Serve.SpeedControl", serveSpeed),
                        ("Serve.PowerCapacity", servePower));
                    return;
                case ExecutionCandidateCategoryV4.SoftAction:
                    var softTouch = attributes.SetSoftTouch;
                    directionControl = softTouch;
                    speedControl = softTouch;
                    powerCapacity = softTouch;
                    baseMaximumSpeed = 12f;
                    abilityConsumptions = Consumptions(
                        ("Set.SoftTouch", softTouch));
                    return;
                case ExecutionCandidateCategoryV4.Defense:
                    var platformControl = attributes.DefensePlatformControl;
                    var coverageMobility = attributes.DefenseCoverageMobility;
                    directionControl = platformControl;
                    speedControl = platformControl;
                    powerCapacity = coverageMobility;
                    baseMaximumSpeed = 14f;
                    abilityConsumptions = Consumptions(
                        ("Defense.PlatformControl", platformControl),
                        ("Defense.CoverageMobility", coverageMobility));
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        private static IReadOnlyList<ExecutionAbilityConsumptionV4>
            Consumptions(
                params (string AttributeName, float Value)[] values)
        {
            var records =
                new ExecutionAbilityConsumptionV4[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                records[index] = new ExecutionAbilityConsumptionV4(
                    values[index].AttributeName,
                    values[index].Value);
            }

            return records;
        }
    }
}

using System;
using System.Collections.Generic;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public readonly struct SetTargetSelectionInput
    {
        public SetTargetSelectionInput(
            TeamId attackingTeam,
            PlayerRole attackerRole,
            float setterDepthFromNet,
            float targetHeight,
            float preferredX,
            IReadOnlyList<ContactCapsuleFrame> predictedArms,
            IReadOnlyList<float> lateralCandidates)
        {
            if (!Enum.IsDefined(typeof(TeamId), attackingTeam))
            {
                throw new ArgumentOutOfRangeException(nameof(attackingTeam));
            }

            if (!Enum.IsDefined(typeof(PlayerRole), attackerRole))
            {
                throw new ArgumentOutOfRangeException(nameof(attackerRole));
            }

            if (!IsFinite(setterDepthFromNet) || setterDepthFromNet < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(setterDepthFromNet));
            }

            if (!IsFinite(targetHeight) || targetHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetHeight));
            }

            if (!IsFinite(preferredX))
            {
                throw new ArgumentOutOfRangeException(nameof(preferredX));
            }

            AttackingTeam = attackingTeam;
            AttackerRole = attackerRole;
            SetterDepthFromNet = setterDepthFromNet;
            TargetHeight = targetHeight;
            PreferredX = preferredX;
            PredictedArms = predictedArms ?? throw new ArgumentNullException(nameof(predictedArms));
            LateralCandidates = lateralCandidates ?? throw new ArgumentNullException(nameof(lateralCandidates));
        }

        public TeamId AttackingTeam { get; }
        public PlayerRole AttackerRole { get; }
        public float SetterDepthFromNet { get; }
        public float TargetHeight { get; }
        public float PreferredX { get; }
        public IReadOnlyList<ContactCapsuleFrame> PredictedArms { get; }
        public IReadOnlyList<float> LateralCandidates { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct SetTargetSelection
    {
        public SetTargetSelection(SimVector3 target, float minimumArmClearance)
        {
            Target = target;
            MinimumArmClearance = minimumArmClearance;
        }

        public SimVector3 Target { get; }
        public float MinimumArmClearance { get; }
    }

    public static class SetTargetSelector
    {
        private const float BallRadius = 0.12f;
        private const float CourtHalfWidth = 4.5f;

        public static SetTargetSelection Select(SetTargetSelectionInput input)
        {
            var band = AttackBandPolicy.Resolve(input.AttackerRole, input.SetterDepthFromNet);
            var depths = new[]
            {
                band.NearDepth,
                (band.NearDepth + band.FarDepth) * 0.5f,
                band.FarDepth
            };
            var frame = new TeamCourtFrame(input.AttackingTeam);
            var found = false;
            var bestTarget = default(SimVector3);
            var bestClearance = float.MinValue;
            var bestLateralDeviation = float.MaxValue;
            var bestDepth = float.MaxValue;

            for (var lateralIndex = 0; lateralIndex < input.LateralCandidates.Count; lateralIndex++)
            {
                var x = input.LateralCandidates[lateralIndex];
                if (!IsFinite(x) || Math.Abs(x) > CourtHalfWidth)
                {
                    continue;
                }

                for (var depthIndex = 0; depthIndex < depths.Length; depthIndex++)
                {
                    var depth = depths[depthIndex];
                    var candidate = frame.ToWorld(new SimVector3(x, input.TargetHeight, -depth));
                    var clearance = MinimumClearance(candidate, input.PredictedArms);
                    if (clearance <= 0f)
                    {
                        continue;
                    }

                    var lateralDeviation = Math.Abs(x - input.PreferredX);
                    if (!found || IsBetter(
                            candidate,
                            clearance,
                            lateralDeviation,
                            depth,
                            bestTarget,
                            bestClearance,
                            bestLateralDeviation,
                            bestDepth))
                    {
                        found = true;
                        bestTarget = candidate;
                        bestClearance = clearance;
                        bestLateralDeviation = lateralDeviation;
                        bestDepth = depth;
                    }
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("No legal set target has positive predicted arm clearance.");
            }

            return new SetTargetSelection(bestTarget, bestClearance);
        }

        private static float MinimumClearance(
            SimVector3 candidate,
            IReadOnlyList<ContactCapsuleFrame> predictedArms)
        {
            if (predictedArms.Count == 0)
            {
                return float.MaxValue;
            }

            var minimum = float.MaxValue;
            for (var index = 0; index < predictedArms.Count; index++)
            {
                var arm = predictedArms[index];
                var closest = arm.ClosestPoint(candidate, out _);
                var clearance = (candidate - closest).Magnitude - BallRadius - arm.Radius;
                minimum = Math.Min(minimum, clearance);
            }

            return minimum;
        }

        private static bool IsBetter(
            SimVector3 candidate,
            float clearance,
            float lateralDeviation,
            float depth,
            SimVector3 current,
            float currentClearance,
            float currentLateralDeviation,
            float currentDepth)
        {
            const float tolerance = 0.000001f;
            if (clearance > currentClearance + tolerance)
            {
                return true;
            }

            if (Math.Abs(clearance - currentClearance) > tolerance)
            {
                return false;
            }

            if (lateralDeviation < currentLateralDeviation - tolerance)
            {
                return true;
            }

            if (Math.Abs(lateralDeviation - currentLateralDeviation) > tolerance)
            {
                return false;
            }

            if (depth < currentDepth - tolerance)
            {
                return true;
            }

            if (Math.Abs(depth - currentDepth) > tolerance)
            {
                return false;
            }

            return candidate.X < current.X - tolerance ||
                   (Math.Abs(candidate.X - current.X) <= tolerance && candidate.Z < current.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

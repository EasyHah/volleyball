using System;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public static class SkillExecutionResolver
    {
        public const float MaximumReactionDelaySeconds = 0.22f;

        public static SkillExecutionError Resolve(
            PlayerAbilityProfile profile,
            TechniqueAction action,
            int playerStableId,
            int rallyNumber,
            int actionIndex,
            int seed,
            float difficulty)
        {
            if (float.IsNaN(difficulty) || float.IsInfinity(difficulty) || difficulty < 0f || difficulty > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty must be in the range [0, 1].");
            }

            var random = new Random(CombineSeed(seed, playerStableId, rallyNumber, actionIndex, (int)action));
            var technique = profile.TechniqueFor(action);
            var executionScale = (1f - technique) * difficulty;
            var reactionDelay = NextPositive(random) * MaximumReactionDelaySeconds *
                                (1f - profile.Reaction) * difficulty;
            var positionRange = action switch
            {
                TechniqueAction.Set => new SimVector3(0.14f, 0.18f, 0.14f),
                TechniqueAction.Attack => new SimVector3(0.22f, 0.18f, 0.22f),
                _ => new SimVector3(0.18f, 0.12f, 0.18f)
            };
            var normalRangeDegrees = action == TechniqueAction.Attack ? 14f : 10f;
            var speedRange = action == TechniqueAction.Attack ? 0.22f : 0.16f;

            return new SkillExecutionError(
                reactionDelay,
                RandomVector(random, positionRange) * executionScale,
                RandomVector(random, new SimVector3(normalRangeDegrees, normalRangeDegrees, normalRangeDegrees)) * executionScale,
                NextSigned(random) * 0.08f * executionScale,
                1f + (NextSigned(random) * speedRange * executionScale),
                RandomVector(random, new SimVector3(1.5f, 1.5f, 1.5f)) * executionScale,
                TechniqueControlPolicy.MaximumControlFor(action) * technique);
        }

        private static SimVector3 RandomVector(Random random, SimVector3 range)
        {
            return new SimVector3(
                NextSigned(random) * range.X,
                NextSigned(random) * range.Y,
                NextSigned(random) * range.Z);
        }

        private static float NextSigned(Random random)
        {
            return (float)((random.NextDouble() * 2d) - 1d);
        }

        private static float NextPositive(Random random)
        {
            return (float)random.NextDouble();
        }

        private static int CombineSeed(params int[] values)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var value in values)
                {
                    hash = (hash ^ value) * 16777619;
                }

                return hash;
            }
        }
    }
}

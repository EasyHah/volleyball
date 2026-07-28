using System;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public static class TryoutNumericRulesV1
    {
        public static TryoutOutputExplanation Explain(
            string reasonId,
            TryoutOutputDefinition definition,
            int baseValue,
            TryoutResolvedOutput resolvedOutput)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
            {
                throw new ArgumentException("A tryout explanation reason is required.", nameof(reasonId));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (resolvedOutput == null)
            {
                throw new ArgumentNullException(nameof(resolvedOutput));
            }

            if (!string.Equals(
                definition.OutputId,
                resolvedOutput.OutputId,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The resolved output must match its versioned definition.",
                    nameof(resolvedOutput));
            }

            var maximum = IsAbility(definition.Kind) ? 10000 : 100;
            if (baseValue < 0 || baseValue > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseValue),
                    baseValue,
                    "The base value is outside the output kind's legal range.");
            }

            var requestedDelta = RequestedDelta(
                definition.Kind,
                resolvedOutput.Perturbation);
            var finalValue = Clamp(baseValue + requestedDelta, 0, maximum);
            return new TryoutOutputExplanation(
                reasonId,
                definition.OutputId,
                baseValue,
                finalValue - baseValue,
                finalValue);
        }

        public static PotentialGrade DerivePotential(
            int spike,
            int serve,
            int reception,
            int defense,
            int block,
            int movement,
            int jump,
            int stamina)
        {
            var values = new[]
            {
                spike,
                serve,
                reception,
                defense,
                block,
                movement,
                jump,
                stamina
            };
            long total = 0;
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index] < 0 || values[index] > 10000)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(spike),
                        "Potential inputs must be ability basis points in [0, 10000].");
                }

                total += values[index];
            }

            var average = total / values.Length;
            if (average < 4500) return PotentialGrade.D;
            if (average < 5000) return PotentialGrade.C;
            if (average < 5500) return PotentialGrade.B;
            if (average < 6000) return PotentialGrade.A;
            return PotentialGrade.S;
        }

        private static int RequestedDelta(TryoutOutputKind kind, int perturbation)
        {
            switch (kind)
            {
                case TryoutOutputKind.Fatigue:
                    return perturbation / 20;
                case TryoutOutputKind.Mindset:
                case TryoutOutputKind.CoachTrust:
                    return perturbation / 10;
                case TryoutOutputKind.Spike:
                case TryoutOutputKind.Serve:
                case TryoutOutputKind.Reception:
                case TryoutOutputKind.Defense:
                case TryoutOutputKind.Block:
                case TryoutOutputKind.Movement:
                case TryoutOutputKind.Jump:
                case TryoutOutputKind.Stamina:
                    return perturbation;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unsupported tryout output kind.");
            }
        }

        private static bool IsAbility(TryoutOutputKind kind)
        {
            return kind >= TryoutOutputKind.Spike && kind <= TryoutOutputKind.Stamina;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}

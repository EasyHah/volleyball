using System;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class DeterministicWorkBudgetV3
    {
        public DeterministicWorkBudgetV3(
            int beamWidth,
            int candidatesPerResponsibility,
            int physicalSamplesPerCandidate,
            int maxCandidateEvaluations,
            int maxInvalidationExpansionDepth)
        {
            BeamWidth = Positive(beamWidth, nameof(beamWidth));
            CandidatesPerResponsibility = Positive(candidatesPerResponsibility, nameof(candidatesPerResponsibility));
            PhysicalSamplesPerCandidate = Positive(physicalSamplesPerCandidate, nameof(physicalSamplesPerCandidate));
            MaxCandidateEvaluations = Positive(maxCandidateEvaluations, nameof(maxCandidateEvaluations));
            MaxInvalidationExpansionDepth = NonNegative(maxInvalidationExpansionDepth, nameof(maxInvalidationExpansionDepth));
        }

        public int BeamWidth { get; }

        public int CandidatesPerResponsibility { get; }

        public int PhysicalSamplesPerCandidate { get; }

        public int MaxCandidateEvaluations { get; }

        public int MaxInvalidationExpansionDepth { get; }

        public bool UsesWallClockForDecision => false;

        public static DeterministicWorkBudgetV3 DefaultPhase0()
        {
            return new DeterministicWorkBudgetV3(
                beamWidth: 3,
                candidatesPerResponsibility: 4,
                physicalSamplesPerCandidate: 5,
                maxCandidateEvaluations: 60,
                maxInvalidationExpansionDepth: 2);
        }

        private static int Positive(int value, string paramName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value must be positive.");
            }

            return value;
        }

        private static int NonNegative(int value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value cannot be negative.");
            }

            return value;
        }
    }
}

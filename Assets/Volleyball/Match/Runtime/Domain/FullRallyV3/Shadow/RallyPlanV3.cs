using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class RallyPlanV3
    {
        public RallyPlanV3(
            RallyWorldSnapshotV3 worldSnapshot,
            TeamRallyPlanV3 homePlan,
            TeamRallyPlanV3 awayPlan,
            string artifactIdentity,
            long revision,
            long sourceSequence,
            PlanCoverageDecision coverageDecision)
        {
            WorldSnapshot = worldSnapshot ?? throw new ArgumentNullException(nameof(worldSnapshot));
            HomePlan = RequirePlan(homePlan, TeamSide.Home, nameof(homePlan));
            AwayPlan = RequirePlan(awayPlan, TeamSide.Away, nameof(awayPlan));
            ArtifactIdentity = PlayerWorldSnapshotV3.RequireText(artifactIdentity, nameof(artifactIdentity));
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "Revision must be non-negative.");
            }

            if (sourceSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSequence), "Source sequence must be non-negative.");
            }

            CoverageDecision = coverageDecision ?? throw new ArgumentNullException(nameof(coverageDecision));
            Revision = revision;
            SourceSequence = sourceSequence;
        }

        public RallyWorldSnapshotV3 WorldSnapshot { get; }
        public TeamRallyPlanV3 HomePlan { get; }
        public TeamRallyPlanV3 AwayPlan { get; }
        public string ArtifactIdentity { get; }
        public long Revision { get; }
        public long SourceSequence { get; }
        public PlanCoverageDecision CoverageDecision { get; }

        private static TeamRallyPlanV3 RequirePlan(TeamRallyPlanV3 plan, TeamSide side, string paramName)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (plan.Side != side)
            {
                throw new ArgumentException("Plan side does not match its slot.", paramName);
            }

            return plan;
        }
    }
}

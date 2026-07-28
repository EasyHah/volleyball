using System;
using System.Collections.Generic;

namespace Volleyball.AI
{
    /// <summary>
    /// Owns legacy tactical candidate generation behind a pure AI boundary.
    /// Presentation supplies immutable inputs and consumes immutable decisions.
    /// </summary>
    public sealed class RallyDecisionCoordinatorV3
    {
        private readonly TeamRallyDecisionPlanner _planner;

        public RallyDecisionCoordinatorV3(int seed)
        {
            _planner = new TeamRallyDecisionPlanner(seed);
        }

        public TeamRallyDecision Plan(TeamRallyDecisionInput input)
        {
            return _planner.Plan(input ??
                throw new ArgumentNullException(nameof(input)));
        }

        public IReadOnlyList<RallyDecisionCandidate> OrderedCandidates(
            TeamRallyDecisionInput input)
        {
            return _planner.OrderedCandidates(input ??
                throw new ArgumentNullException(nameof(input)));
        }

        public bool HasFeasibleCandidate(TeamRallyDecisionInput input)
        {
            var candidates = OrderedCandidates(input);
            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].IsFeasible)
                    return true;
            return false;
        }

        public ReceiveOrganizationResponsibilityPlanner
            CreateReceiveOrganizationPlanner()
        {
            return new ReceiveOrganizationResponsibilityPlanner(_planner);
        }
    }
}

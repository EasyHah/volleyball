using System;
using System.Collections.Generic;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    /// <summary>
    /// Owns legacy tactical candidate generation behind a pure AI boundary.
    /// Presentation supplies immutable inputs and consumes immutable decisions.
    /// </summary>
    public sealed class RallyDecisionCoordinatorV3
    {
        private readonly TeamRallyDecisionPlanner _planner;
        private int _decisionIndex;

        public RallyDecisionCoordinatorV3()
        {
            _planner = new TeamRallyDecisionPlanner();
        }

        public TeamRallyDecision Plan(TeamRallyDecisionInput input)
        {
            return _planner.Plan(input ??
                throw new ArgumentNullException(nameof(input)));
        }

        public int DecisionIndex => _decisionIndex;

        public TeamRallyDecisionInput CreateInput(
            TeamId team,
            TeamRallyTactic tactic,
            IReadOnlyList<RallyPlayerSnapshot> players,
            SimVector3 predictedBallCenter,
            float availableSeconds,
            float baseMovementSpeed,
            int countedTouches,
            PlayerId? lastCountedActor,
            int tacticRevision,
            RallyDecisionStage stage,
            RallyTacticalWeights tacticalWeights)
        {
            return new TeamRallyDecisionInput(
                team,
                tactic,
                players ?? throw new ArgumentNullException(nameof(players)),
                predictedBallCenter,
                availableSeconds,
                baseMovementSpeed,
                countedTouches,
                lastCountedActor,
                tacticRevision,
                _decisionIndex++,
                stage,
                tacticalWeights);
        }

        public bool HasFeasibleCandidate(TeamRallyDecisionInput input)
        {
            var candidates = _planner.OrderedCandidates(input ??
                throw new ArgumentNullException(nameof(input)));
            for (var index = 0; index < candidates.Count; index++)
                if (candidates[index].IsFeasible)
                    return true;
            return false;
        }

        public SetTargetSelection SelectSetTarget(SetTargetSelectionInput input)
        {
            return SetTargetSelector.Select(input);
        }

        public bool TrySelectCoveragePlayer(
            IReadOnlyList<RallyPlayerSnapshot> players,
            ISet<PlayerId> excludedPlayers,
            SimVector3 coverageTarget,
            out PlayerId selectedPlayer)
        {
            if (players == null)
                throw new ArgumentNullException(nameof(players));
            if (excludedPlayers == null)
                throw new ArgumentNullException(nameof(excludedPlayers));
            if (!coverageTarget.IsFinite)
                throw new ArgumentOutOfRangeException(nameof(coverageTarget));

            selectedPlayer = default;
            var found = false;
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                if (excludedPlayers.Contains(player.Id))
                    continue;

                var distance = (player.WorldPosition - coverageTarget).Magnitude;
                if (distance < bestDistance)
                {
                    selectedPlayer = player.Id;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found;
        }

        public ReceiveOrganizationResponsibilityPlanner
            CreateReceiveOrganizationPlanner()
        {
            return new ReceiveOrganizationResponsibilityPlanner(_planner);
        }
    }
}

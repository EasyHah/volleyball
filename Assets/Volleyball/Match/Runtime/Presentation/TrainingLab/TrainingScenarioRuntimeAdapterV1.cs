using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Domain.Prototype;
using Volleyball.Match.Domain.FullRallyV3;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingScenarioRuntimeAdapterV1
    {
        private readonly TrainingScenarioV1 _scenario;

        public TrainingScenarioRuntimeAdapterV1(TrainingScenarioV1 scenario)
        {
            _scenario = scenario ??
                        throw new ArgumentNullException(nameof(scenario));
        }

        public TrainingScenarioV1 Scenario => _scenario;

        public void ApplyPlayerSnapshots(
            IReadOnlyCollection<PrototypePlayerAgent> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            var byStableId = players.ToDictionary(
                player => player?.StableId ??
                          throw new ArgumentException(
                              "Runtime players cannot contain null.",
                              nameof(players)));
            if (byStableId.Count != 12)
            {
                throw new ArgumentException(
                    "Training runtime requires exactly twelve unique players.",
                    nameof(players));
            }

            for (var index = 0; index < _scenario.Players.Count; index++)
            {
                var snapshot = _scenario.Players[index];
                if (!byStableId.TryGetValue(snapshot.PlayerId, out var player))
                {
                    throw new InvalidOperationException(
                        "A training snapshot player is absent from the formal runtime.");
                }

                player.PrepareForTrainingSnapshot(
                    snapshot.Position,
                    snapshot.Forward,
                    snapshot.Pose);
            }
        }

        public PlayerId? ResolveRuntimeLastActor(
            IReadOnlyCollection<PrototypePlayerAgent> players)
        {
            if (!_scenario.StartState.LastLegalActor.HasValue)
            {
                return null;
            }

            var stableActor = _scenario.StartState.LastLegalActor.Value;
            var actor = players.SingleOrDefault(player =>
                player != null && player.StableId.Equals(stableActor));
            if (actor == null)
            {
                throw new InvalidOperationException(
                    "The semantic last actor is absent from the formal runtime.");
            }

            return actor.Id;
        }

        public PrototypePlayerAgent ResolvePlayer(
            IReadOnlyCollection<PrototypePlayerAgent> players,
            StablePlayerId playerId)
        {
            return players.Single(player => player.StableId.Equals(playerId));
        }
    }
}

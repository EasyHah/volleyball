using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class BlockUnitPlannerTests
    {
        [Test]
        public void Select_UsesThreeDistinctReachablePlayersWhenTheyCoverAdjacentLanes()
        {
            var unit = BlockUnitPlanner.Select(
                ThreeReachableFrontPlayers(),
                new SimVector3(0f, 3.1f, 0f),
                0.8f);

            Assert.That(unit.Blockers.Count, Is.EqualTo(3));
            Assert.That(unit.Blockers.Select(player => player.Id).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void Select_ExcludesUnreachablePlayers()
        {
            var unit = BlockUnitPlanner.Select(
                OneReachableAndTwoDistantPlayers(),
                new SimVector3(0f, 3.1f, 0f),
                0.35f);

            Assert.That(unit.Blockers.Count, Is.EqualTo(1));
        }

        [Test]
        public void Select_ExcludesBackRowPlayersWhenFormalSixVsSixIsRequested()
        {
            var unit = BlockUnitPlanner.Select(
                FrontAndBackRowPlayers(),
                new SimVector3(0f, 3.1f, 0f),
                0.8f,
                requireFrontRow: true);

            Assert.That(unit.Blockers.All(player => player.IsFrontRow), Is.True);
        }

        private static BlockCandidateSnapshot[] ThreeReachableFrontPlayers()
        {
            return new[]
            {
                Candidate(PlayerRole.OutsideHitter, -1f, true, 4f),
                Candidate(PlayerRole.MiddleBlocker, 0f, true, 4f),
                Candidate(PlayerRole.Opposite, 1f, true, 4f)
            };
        }

        private static BlockCandidateSnapshot[] OneReachableAndTwoDistantPlayers()
        {
            return new[]
            {
                Candidate(PlayerRole.MiddleBlocker, 0f, true, 4f),
                Candidate(PlayerRole.OutsideHitter, -4f, true, 1f),
                Candidate(PlayerRole.Opposite, 4f, true, 1f)
            };
        }

        private static BlockCandidateSnapshot[] FrontAndBackRowPlayers()
        {
            return new[]
            {
                Candidate(PlayerRole.MiddleBlocker, 0f, true, 4f),
                Candidate(PlayerRole.OutsideHitter, -0.8f, false, 4f),
                Candidate(PlayerRole.Opposite, 0.8f, true, 4f)
            };
        }

        private static BlockCandidateSnapshot Candidate(
            PlayerRole role,
            float x,
            bool isFrontRow,
            float movementSpeed)
        {
            return new BlockCandidateSnapshot(
                new PlayerId(TeamId.Orange, role),
                new SimVector3(x, 0f, 0.25f),
                movementSpeed,
                jump: 0.8f,
                isFrontRow);
        }
    }
}

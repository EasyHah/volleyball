using System;
using Volleyball.Domain.Prototype;

namespace Volleyball.AI
{
    public sealed class DeterministicRallyPlanner
    {
        private readonly int seed;

        public DeterministicRallyPlanner(int seed)
        {
            this.seed = seed;
        }

        public RallyPlan CreatePlan(TeamId servingTeam, int rallyNumber)
        {
            if (servingTeam != TeamId.Blue && servingTeam != TeamId.Orange)
            {
                throw new ArgumentOutOfRangeException(nameof(servingTeam));
            }

            if (rallyNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rallyNumber));
            }

            var random = new Random(unchecked(seed + rallyNumber * 7919));
            var receivingTeam = servingTeam == TeamId.Blue ? TeamId.Orange : TeamId.Blue;
            var lane = random.Next(0, 3) - 1;
            var defense = random.Next(0, 2) == 0
                ? RallyActionKind.Block
                : RallyActionKind.Dig;
            var winner = random.Next(0, 2) == 0 ? receivingTeam : servingTeam;
            var defenseContactHeight = defense == RallyActionKind.Block ? 2.7f : 1.3f;
            var receivingSign = receivingTeam == TeamId.Blue ? -1f : 1f;
            var servingSign = servingTeam == TeamId.Blue ? -1f : 1f;

            var receivePoint = new CourtPoint(lane * 2.2f, receivingSign * 5f);
            var receivingSetPoint = new CourtPoint(-1.4f, receivingSign * 1.8f);
            var receivingAttackPoint = new CourtPoint(-2.4f, receivingSign * 3.2f);
            var defensePoint = new CourtPoint(lane * -1.8f, servingSign * 5.6f);
            var servingSetPoint = new CourtPoint(1.4f, servingSign * 1.8f);
            var servingAttackPoint = new CourtPoint(2.4f, servingSign * 3.2f);
            var finalBallTarget = winner == servingTeam
                ? new CourtPoint(lane * 2.4f, receivingSign * 6.3f)
                : new CourtPoint(lane * 2.4f, receivingSign * 8.2f);

            var contacts = new[]
            {
                new ContactPlan(
                    new PlayerId(servingTeam, PlayerRole.Attacker),
                    RallyActionKind.Serve,
                    new CourtPoint(-2.4f, servingSign * 5.8f),
                    receivePoint,
                    1.8f,
                    1.1f,
                    1.1f,
                    3.2f),
                new ContactPlan(
                    new PlayerId(receivingTeam, PlayerRole.Defender),
                    RallyActionKind.Receive,
                    receivePoint,
                    receivingSetPoint,
                    1.3f,
                    1.9f,
                    0.7f,
                    1.4f),
                new ContactPlan(
                    new PlayerId(receivingTeam, PlayerRole.Setter),
                    RallyActionKind.Set,
                    receivingSetPoint,
                    receivingAttackPoint,
                    2.2f,
                    2.7f,
                    0.8f,
                    2.5f),
                new ContactPlan(
                    new PlayerId(receivingTeam, PlayerRole.Attacker),
                    RallyActionKind.Spike,
                    receivingAttackPoint,
                    defensePoint,
                    2.7f,
                    defenseContactHeight,
                    0.8f,
                    2f),
                new ContactPlan(
                    new PlayerId(servingTeam, PlayerRole.Attacker),
                    defense,
                    defensePoint,
                    servingSetPoint,
                    defenseContactHeight,
                    2.7f,
                    0.7f,
                    1.5f),
                new ContactPlan(
                    new PlayerId(servingTeam, PlayerRole.Setter),
                    RallyActionKind.Set,
                    servingSetPoint,
                    servingAttackPoint,
                    2.2f,
                    2.7f,
                    0.8f,
                    2.5f),
                new ContactPlan(
                    new PlayerId(servingTeam, PlayerRole.Attacker),
                    RallyActionKind.Spike,
                    servingAttackPoint,
                    finalBallTarget,
                    2.7f,
                    0.15f,
                    0.8f,
                    1.9f)
            };

            return new RallyPlan(contacts, winner);
        }
    }
}

using System;
using NUnit.Framework;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class PrototypeMatchTests
    {
        [Test]
        public void NewMatch_StartsAtZeroWithBlueServing()
        {
            var match = new PrototypeMatch(TeamId.Blue);

            Assert.That(match.BlueScore, Is.Zero);
            Assert.That(match.OrangeScore, Is.Zero);
            Assert.That(match.ServingTeam, Is.EqualTo(TeamId.Blue));
            Assert.That(match.RallyNumber, Is.Zero);
        }

        [Test]
        public void AwardPoint_OnlyResolvesTheActiveRallyOnce()
        {
            var match = new PrototypeMatch(TeamId.Blue);

            Assert.That(match.TryAwardPoint(TeamId.Orange), Is.True);
            Assert.That(match.TryAwardPoint(TeamId.Orange), Is.False);
            Assert.That(match.OrangeScore, Is.EqualTo(1));
            Assert.That(match.ServingTeam, Is.EqualTo(TeamId.Orange));
            Assert.That(match.RallyNumber, Is.EqualTo(1));
        }

        [Test]
        public void AwardPoint_InvalidWinnerThrowsWithoutMutatingState()
        {
            var match = new PrototypeMatch(TeamId.Blue);

            Assert.Throws<ArgumentOutOfRangeException>(() => match.TryAwardPoint((TeamId)123));
            Assert.That(match.BlueScore, Is.Zero);
            Assert.That(match.OrangeScore, Is.Zero);
            Assert.That(match.ServingTeam, Is.EqualTo(TeamId.Blue));
            Assert.That(match.RallyNumber, Is.Zero);
            Assert.That(match.TryAwardPoint(TeamId.Blue), Is.True);
        }

        [Test]
        public void BeginNextRally_AllowsExactlyOneMorePoint()
        {
            var match = new PrototypeMatch(TeamId.Orange);

            Assert.That(match.TryAwardPoint(TeamId.Blue), Is.True);
            match.BeginNextRally();
            Assert.That(match.TryAwardPoint(TeamId.Orange), Is.True);
            Assert.That(match.TryAwardPoint(TeamId.Blue), Is.False);

            Assert.That(match.BlueScore, Is.EqualTo(1));
            Assert.That(match.OrangeScore, Is.EqualTo(1));
            Assert.That(match.ServingTeam, Is.EqualTo(TeamId.Orange));
            Assert.That(match.RallyNumber, Is.EqualTo(2));
        }
    }
}

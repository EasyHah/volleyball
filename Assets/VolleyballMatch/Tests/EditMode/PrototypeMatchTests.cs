using NUnit.Framework;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.EditModeTests
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
    }
}

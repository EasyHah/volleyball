using NUnit.Framework;
using Volleyball.Domain;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class MatchRallyRefereeTests
    {
        private const float HalfWidth = 4.5f;
        private const float HalfLength = 7.5f;
        private const float NetHeight = 2.43f;

        [Test]
        public void GroundLanding_AfterHomeTouchInAwayCourt_AwardsHome()
        {
            var outcome = MatchRallyReferee.ResolveGroundLanding(
                TeamSide.Home,
                new SimVector3(0f, 0f, 3f),
                HalfWidth,
                HalfLength);

            Assert.That(outcome.Winner, Is.EqualTo(TeamSide.Home));
            Assert.That(outcome.IsFault, Is.False);
        }

        [Test]
        public void GroundLanding_AfterHomeTouchInHomeCourt_AwardsAway()
        {
            var outcome = MatchRallyReferee.ResolveGroundLanding(
                TeamSide.Home,
                new SimVector3(0f, 0f, -3f),
                HalfWidth,
                HalfLength);

            Assert.That(outcome.Winner, Is.EqualTo(TeamSide.Away));
            Assert.That(outcome.IsFault, Is.True);
        }

        [Test]
        public void GroundLanding_AfterHomeTouchOutsideAwayCourt_AwardsAway()
        {
            var outcome = MatchRallyReferee.ResolveGroundLanding(
                TeamSide.Home,
                new SimVector3(HalfWidth + 0.01f, 0f, 3f),
                HalfWidth,
                HalfLength);

            Assert.That(outcome.Winner, Is.EqualTo(TeamSide.Away));
            Assert.That(outcome.IsFault, Is.True);
        }

        [Test]
        public void NetCrossing_OutsideAntenna_AwardsOpponent()
        {
            var outcome = MatchRallyReferee.ResolveNetCrossing(
                TeamSide.Home,
                new SimVector3(HalfWidth + 0.01f, NetHeight + 0.1f, 0f),
                HalfWidth,
                NetHeight);

            Assert.That(outcome.HasValue, Is.True);
            Assert.That(outcome.Value.Winner, Is.EqualTo(TeamSide.Away));
            Assert.That(outcome.Value.IsFault, Is.True);
        }

        [Test]
        public void NetCrossing_InsideAntennaAboveNet_LeavesRallyUnresolved()
        {
            var outcome = MatchRallyReferee.ResolveNetCrossing(
                TeamSide.Home,
                new SimVector3(0f, NetHeight + 0.1f, 0f),
                HalfWidth,
                NetHeight);

            Assert.That(outcome.HasValue, Is.False);
        }

        [Test]
        public void NetCrossing_InsideAntennaBelowNet_LeavesRallyUnresolvedForNetResponse()
        {
            var outcome = MatchRallyReferee.ResolveNetCrossing(
                TeamSide.Home,
                new SimVector3(0f, NetHeight - 0.1f, 0f),
                HalfWidth,
                NetHeight);

            Assert.That(outcome.HasValue, Is.False);
        }
    }
}

using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FullRallyV3RulesEngineTests
    {
        private static readonly PlayerId HomeServer = new PlayerId("home-server");
        private static readonly PlayerId HomeSetter = new PlayerId("home-setter");
        private static readonly PlayerId HomeAttacker = new PlayerId("home-attacker");
        private static readonly PlayerId AwayBlocker = new PlayerId("away-blocker");
        private static readonly PlayerId AwaySetter = new PlayerId("away-setter");

        [Test]
        public void CanAttempt_DoesNotMutateState()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            var before = engine.State;

            var transition = engine.CanAttempt(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));

            Assert.That(transition.Accepted, Is.True);
            Assert.That(engine.State, Is.SameAs(before));
            AssertState(engine.State, null, null, 0, null, null, null, false);
        }

        [Test]
        public void ServeContact_StartsCountedSequence()
        {
            var transition = RallyRulesEngineV3.Open(TeamSide.Home)
                .Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));

            Assert.That(transition.Accepted, Is.True);
            AssertState(transition.After, TeamSide.Home, TeamSide.Home, 1, HomeServer,
                RallyContactClassificationV3.ServeContact, 1, false);
            Assert.That(transition.After.RemainingHits, Is.EqualTo(2));
        }

        [Test]
        public void TeamContact_CountsRegardlessOfTheCallerIntendedTechnique()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));

            var transition = engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 2));

            Assert.That(transition.Accepted, Is.True);
            Assert.That(transition.After.CountedHits, Is.EqualTo(2));
            Assert.That(transition.After.LastCountedActor, Is.EqualTo((PlayerId?)HomeSetter));
        }

        [Test]
        public void ConsecutiveCountedContact_IsRejectedWithoutStateMutation()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));
            var before = engine.State;

            var transition = engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.TeamContact, 2));

            Assert.That(transition.Accepted, Is.False);
            Assert.That(transition.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.ConsecutiveCountedContact));
            Assert.That(transition.Before, Is.SameAs(before));
            Assert.That(transition.After, Is.SameAs(before));
            Assert.That(engine.State, Is.SameAs(before));
        }

        [Test]
        public void FourthCountedContact_IsRejectedWithoutStateMutation()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));
            engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 2));
            engine.Apply(Contact(HomeAttacker, TeamSide.Home, RallyContactClassificationV3.TeamContact, 3));
            var before = engine.State;

            var transition = engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 4));

            Assert.That(transition.Accepted, Is.False);
            Assert.That(transition.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.FourthCountedContact));
            Assert.That(transition.After, Is.SameAs(before));
            Assert.That(engine.State, Is.SameAs(before));
        }

        [Test]
        public void BlockThenSameBlockerTeamContact_StartsFreshThreeHitSequence()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            var block = engine.Apply(Contact(AwayBlocker, TeamSide.Away, RallyContactClassificationV3.BlockContact, 10));
            var first = engine.Apply(Contact(AwayBlocker, TeamSide.Away, RallyContactClassificationV3.TeamContact, 11));

            AssertState(block.After, TeamSide.Away, null, 0, null,
                RallyContactClassificationV3.BlockContact, 10, false);
            Assert.That(block.After.RemainingHits, Is.EqualTo(3));
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.After.CurrentCountedSequenceTeam, Is.EqualTo(TeamSide.Away));
            Assert.That(first.After.CountedHits, Is.EqualTo(1));
            Assert.That(first.After.RemainingHits, Is.EqualTo(2));
        }

        [TestCase(TeamSide.Home)]
        [TestCase(TeamSide.Away)]
        public void BlockReboundToEitherSide_StartsANewSequence(TeamSide reboundTeam)
        {
            var reboundActor = reboundTeam == TeamSide.Home ? HomeSetter : AwaySetter;
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));
            engine.Apply(Contact(AwayBlocker, TeamSide.Away, RallyContactClassificationV3.BlockContact, 2));

            var rebound = engine.Apply(Contact(reboundActor, reboundTeam, RallyContactClassificationV3.TeamContact, 3));

            Assert.That(rebound.Accepted, Is.True);
            AssertState(rebound.After, reboundTeam, reboundTeam, 1, reboundActor,
                RallyContactClassificationV3.TeamContact, 3, false);
        }

        [Test]
        public void DuplicateContactGroup_IsRejectedWithoutStateMutation()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 1));
            var before = engine.State;

            var transition = engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 1));

            Assert.That(transition.Accepted, Is.False);
            Assert.That(transition.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.DuplicateContactGroup));
            Assert.That(transition.After, Is.SameAs(before));
            Assert.That(engine.State, Is.SameAs(before));
        }

        [Test]
        public void DuplicateContactGroup_AcrossClassifications_IsRejectedWithoutStateMutation()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(AwayBlocker, TeamSide.Away, RallyContactClassificationV3.BlockContact, 1));
            var before = engine.State;

            var transition = engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 1));

            Assert.That(transition.Accepted, Is.False);
            Assert.That(transition.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.DuplicateContactGroup));
            Assert.That(transition.Before, Is.SameAs(before));
            Assert.That(transition.After, Is.SameAs(before));
            Assert.That(engine.State, Is.SameAs(before));
        }

        [Test]
        public void SimultaneousSameTeamContact_CountsOnce()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);

            var transition = engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.SimultaneousTeamContact, 1));
            var beforeDuplicate = engine.State;
            var duplicate = engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.SimultaneousTeamContact, 1));

            Assert.That(transition.Accepted, Is.True);
            Assert.That(transition.After.CountedHits, Is.EqualTo(1));
            Assert.That(transition.After.LastContactGroup, Is.EqualTo((long?)1));
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.DuplicateContactGroup));
            Assert.That(duplicate.After, Is.SameAs(beforeDuplicate));
            Assert.That(engine.State, Is.SameAs(beforeDuplicate));
            Assert.That(engine.State.CountedHits, Is.EqualTo(1));
        }

        [Test]
        public void EnvironmentContact_ClosesTheRallyAndLaterContactIsRejected()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            var environment = ActualContactEventV3.Environment(1);
            var terminal = engine.Apply(environment);
            var before = engine.State;

            var later = engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.ServeContact, 2));

            Assert.That(environment.Actor.HasValue, Is.False);
            Assert.That(environment.Team.HasValue, Is.False);
            Assert.That(terminal.Accepted, Is.True);
            Assert.That(terminal.After.IsTerminal, Is.True);
            Assert.That(later.Accepted, Is.False);
            Assert.That(later.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.RallyClosed));
            Assert.That(later.After, Is.SameAs(before));
            Assert.That(engine.State, Is.SameAs(before));
        }

        private static ActualContactEventV3 Contact(
            PlayerId actor,
            TeamSide team,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            return new ActualContactEventV3(actor, team, classification, contactGroup);
        }

        private static void AssertState(
            TouchSequenceStateV3 state,
            TeamSide? lastPhysicalTeam,
            TeamSide? countedSequenceTeam,
            int countedHits,
            PlayerId? lastCountedActor,
            RallyContactClassificationV3? lastClassification,
            long? lastContactGroup,
            bool isTerminal)
        {
            Assert.That(state.LastLegalPhysicalContactTeam, Is.EqualTo(lastPhysicalTeam));
            Assert.That(state.CurrentCountedSequenceTeam, Is.EqualTo(countedSequenceTeam));
            Assert.That(state.CountedHits, Is.EqualTo(countedHits));
            Assert.That(state.LastCountedActor, Is.EqualTo(lastCountedActor));
            Assert.That(state.LastContactClassification, Is.EqualTo(lastClassification));
            Assert.That(state.LastContactGroup, Is.EqualTo(lastContactGroup));
            Assert.That(state.IsTerminal, Is.EqualTo(isTerminal));
        }
    }
}

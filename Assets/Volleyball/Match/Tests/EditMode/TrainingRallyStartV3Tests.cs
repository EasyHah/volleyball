using System;
using NUnit.Framework;
using Volleyball.Domain.Prototype;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using RuntimeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingRallyStartV3Tests
    {
        private static readonly StablePlayerId[] HomeRotation =
        {
            new StablePlayerId("home-1"),
            new StablePlayerId("home-2"),
            new StablePlayerId("home-3"),
            new StablePlayerId("home-4"),
            new StablePlayerId("home-5"),
            new StablePlayerId("home-6")
        };

        private static readonly StablePlayerId[] AwayRotation =
        {
            new StablePlayerId("away-1"),
            new StablePlayerId("away-2"),
            new StablePlayerId("away-3"),
            new StablePlayerId("away-4"),
            new StablePlayerId("away-5"),
            new StablePlayerId("away-6")
        };

        private static readonly RuntimePlayerId RuntimeHomeActor =
            new RuntimePlayerId(RuntimeTeamId.Blue, PlayerRole.OutsideHitter, 1);

        [Test]
        public void ServeFlight_CreatesZeroCountedStateWithoutSyntheticContact()
        {
            var start = Create(RallyStartRecipeV3.ServeFlight, TeamSide.Home, null);

            Assert.That(start.TouchSequence.CountedHits, Is.Zero);
            Assert.That(start.TouchSequence.CurrentCountedSequenceTeam, Is.Null);
            Assert.That(start.TouchSequence.LastCountedActor, Is.Null);
            Assert.That(start.TouchSequence.LastContactClassification, Is.Null);
            Assert.That(start.TouchSequence.LastContactGroup, Is.Null);
            Assert.That(start.IsPostBlock, Is.False);

            var compatibility = start.CreateCompatibilityState(null);
            Assert.That(compatibility.PossessionTeam, Is.EqualTo(RuntimeTeamId.Blue));
            Assert.That(compatibility.CountedTeamTouches, Is.Zero);
            Assert.That(compatibility.LastPhysicalTouch, Is.Null);
        }

        [TestCase(RallyStartRecipeV3.AfterReceive, 1)]
        [TestCase(RallyStartRecipeV3.AfterSet, 2)]
        [TestCase(RallyStartRecipeV3.AfterAttack, 3)]
        public void CountedRecipes_DeriveCountAndPreserveLastActor(
            RallyStartRecipeV3 recipe,
            int expectedCount)
        {
            var start = Create(recipe, TeamSide.Home, HomeRotation[1]);

            Assert.That(start.TouchSequence.LastLegalPhysicalContactTeam, Is.EqualTo(TeamSide.Home));
            Assert.That(start.TouchSequence.CurrentCountedSequenceTeam, Is.EqualTo(TeamSide.Home));
            Assert.That(start.TouchSequence.CountedHits, Is.EqualTo(expectedCount));
            Assert.That(start.TouchSequence.LastCountedActor, Is.EqualTo(HomeRotation[1]));
            Assert.That(
                start.TouchSequence.LastContactClassification,
                Is.EqualTo(RallyContactClassificationV3.TeamContact));
            Assert.That(start.TouchSequence.LastContactGroup, Is.Null);

            var compatibility = start.CreateCompatibilityState(RuntimeHomeActor);
            Assert.That(compatibility.PossessionTeam, Is.EqualTo(RuntimeTeamId.Blue));
            Assert.That(compatibility.CountedTeamTouches, Is.EqualTo(expectedCount));
            Assert.That(compatibility.LastCountedActor, Is.EqualTo(RuntimeHomeActor));
            Assert.That(compatibility.LastPhysicalTouch, Is.EqualTo(RuntimeHomeActor));
            Assert.That(compatibility.ContactWindow, Is.Null);
        }

        [Test]
        public void AfterAttack_NextSameTeamCountedContactIsFourthAndRejected()
        {
            var start = Create(
                RallyStartRecipeV3.AfterAttack,
                TeamSide.Home,
                HomeRotation[1]);
            var engine = RallyRulesEngineV3.Open(start);

            var transition = engine.Apply(new ActualContactEventV3(
                HomeRotation[2],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                1));

            Assert.That(transition.Accepted, Is.False);
            Assert.That(
                transition.RejectionReason,
                Is.EqualTo(RuleRejectionReasonV3.FourthCountedContact));
            Assert.That(engine.State, Is.SameAs(start.TouchSequence));
        }

        [Test]
        public void AfterAcceptedBlock_CreatesBlockOwnedZeroCountedPostBlockState()
        {
            var start = Create(
                RallyStartRecipeV3.AfterAcceptedBlock,
                TeamSide.Home,
                HomeRotation[1]);

            Assert.That(start.IsPostBlock, Is.True);
            Assert.That(start.TouchSequence.LastLegalPhysicalContactTeam, Is.EqualTo(TeamSide.Home));
            Assert.That(start.TouchSequence.CurrentCountedSequenceTeam, Is.Null);
            Assert.That(start.TouchSequence.CountedHits, Is.Zero);
            Assert.That(start.TouchSequence.LastCountedActor, Is.Null);
            Assert.That(
                start.TouchSequence.LastContactClassification,
                Is.EqualTo(RallyContactClassificationV3.BlockContact));
            Assert.That(start.TouchSequence.LastContactGroup, Is.Null);

            var compatibility = start.CreateCompatibilityState(RuntimeHomeActor);
            Assert.That(compatibility.CountedTeamTouches, Is.Zero);
            Assert.That(compatibility.LastCountedActor, Is.Null);
            Assert.That(compatibility.LastPhysicalTouch, Is.EqualTo(RuntimeHomeActor));
        }

        [Test]
        public void Create_RejectsActorFromAnotherSideOrOutsideEligibility()
        {
            var eligibility = CreateEligibility();

            Assert.That(
                () => RallyStartStateV3Factory.Create(
                    new RallyStartRequestV3(
                        RallyStartRecipeV3.AfterSet,
                        TeamSide.Home,
                        AwayRotation[1]),
                    eligibility),
                Throws.ArgumentException);
            Assert.That(
                () => RallyStartStateV3Factory.Create(
                    new RallyStartRequestV3(
                        RallyStartRecipeV3.AfterSet,
                        TeamSide.Home,
                        new StablePlayerId("home-bench")),
                    eligibility),
                Throws.ArgumentException);
        }

        [Test]
        public void Create_RejectsUnknownRecipeAndInconsistentActor()
        {
            var eligibility = CreateEligibility();

            Assert.That(
                () => RallyStartStateV3Factory.Create(
                    new RallyStartRequestV3(
                        (RallyStartRecipeV3)999,
                        TeamSide.Home,
                        null),
                    eligibility),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => RallyStartStateV3Factory.Create(
                    new RallyStartRequestV3(
                        RallyStartRecipeV3.ServeFlight,
                        TeamSide.Home,
                        HomeRotation[0]),
                    eligibility),
                Throws.ArgumentException);
            Assert.That(
                () => RallyStartStateV3Factory.Create(
                    new RallyStartRequestV3(
                        RallyStartRecipeV3.AfterReceive,
                        TeamSide.Home,
                        null),
                    eligibility),
                Throws.ArgumentException);
        }

        [Test]
        public void Create_RejectsBackRowActorForAcceptedBlock()
        {
            Assert.That(
                () => Create(
                    RallyStartRecipeV3.AfterAcceptedBlock,
                    TeamSide.Home,
                    HomeRotation[4]),
                Throws.ArgumentException);
        }

        [Test]
        public void SeededRuntimeAdapter_BeginsAtDerivedStateWithoutConsumingAContactGroup()
        {
            var eligibility = CreateEligibility();
            var start = RallyStartStateV3Factory.Create(
                new RallyStartRequestV3(
                    RallyStartRecipeV3.AfterSet,
                    TeamSide.Home,
                    HomeRotation[1]),
                eligibility);
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                eligibility,
                TeamSide.Away,
                V3RulesMode.Authority);

            adapter.BeginRally(start);
            var transition = adapter.CommitContact(
                HomeRotation[2],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                0);

            Assert.That(transition.Accepted, Is.True);
            Assert.That(transition.Before.CountedHits, Is.EqualTo(2));
            Assert.That(transition.Before.LastContactGroup, Is.Null);
            Assert.That(transition.After.CountedHits, Is.EqualTo(3));
        }

        private static RallyStartStateV3 Create(
            RallyStartRecipeV3 recipe,
            TeamSide team,
            StablePlayerId? actor)
        {
            return RallyStartStateV3Factory.Create(
                new RallyStartRequestV3(recipe, team, actor),
                CreateEligibility());
        }

        private static OnCourtEligibilitySnapshot CreateEligibility()
        {
            var positions = new[]
            {
                PlayerPosition.Setter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.MiddleBlocker,
                PlayerPosition.Opposite,
                PlayerPosition.Defender,
                PlayerPosition.OutsideHitter
            };
            var context = MatchV4TestFixture.CreateContextForRotations(
                Guid.Parse("f90d31ea-b8b9-426b-9e93-72e9c40cd27b"),
                24191,
                HomeRotation,
                positions,
                AwayRotation,
                positions);
            return OnCourtLineupRulesV3.Create(
                context,
                HomeRotation,
                AwayRotation,
                HomeRotation[0],
                AwayRotation[0],
                Array.Empty<LiberoReplacementV3>());
        }
    }
}

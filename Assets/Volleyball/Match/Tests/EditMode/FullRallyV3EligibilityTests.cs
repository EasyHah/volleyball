using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FullRallyV3EligibilityTests
    {
        [Test]
        public void Create_MapsSuppliedRotationOrderAndEligibilityForFormalSix()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            Assert.That(snapshot.Players, Has.Count.EqualTo(12));
            Assert.That(snapshot.Players.Take(6).Select(player => player.PlayerId), Is.EqualTo(HomeRotationOrder));
            Assert.That(snapshot.Players.Skip(6).Select(player => player.PlayerId), Is.EqualTo(AwayRotationOrder));
            Assert.That(snapshot.For(HomeRotationOrder[0]).RotationPosition, Is.EqualTo(1));
            Assert.That(snapshot.For(HomeRotationOrder[0]).IsFrontRow, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[0]).IsCurrentServer, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[0]).CanBlock, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[1]).RotationPosition, Is.EqualTo(2));
            Assert.That(snapshot.For(HomeRotationOrder[1]).IsFrontRow, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[1]).IsCurrentServer, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[1]).CanBlock, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[2]).RotationPosition, Is.EqualTo(3));
            Assert.That(snapshot.For(HomeRotationOrder[2]).IsFrontRow, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[2]).CanAttackAboveNetFromFrontZone, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[3]).RotationPosition, Is.EqualTo(4));
            Assert.That(snapshot.For(HomeRotationOrder[3]).IsFrontRow, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[3]).CanBlock, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[4]).RotationPosition, Is.EqualTo(5));
            Assert.That(snapshot.For(HomeRotationOrder[4]).IsFrontRow, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[5]).RotationPosition, Is.EqualTo(6));
            Assert.That(snapshot.For(HomeRotationOrder[5]).CanBlock, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[5]).CanAttackAboveNetFromFrontZone, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[5]).RegisteredPosition, Is.EqualTo(PlayerPosition.Libero));
        }

        [Test]
        public void Create_RejectsServerOutsideItsSuppliedRotation()
        {
            var context = CreateV4Context();

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, HomeRotationOrder, AwayRotationOrder, new PlayerId("home-bench"), AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void Create_RejectsDuplicateIdsWithinASuppliedRotation()
        {
            var context = CreateV4Context();
            var duplicateHomeRotation = new[]
            {
                HomeRotationOrder[0], HomeRotationOrder[0], HomeRotationOrder[2], HomeRotationOrder[3], HomeRotationOrder[4], HomeRotationOrder[5]
            };

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, duplicateHomeRotation, AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void Create_RequiresExactlySixIdsPerSuppliedRotation()
        {
            var context = CreateV4Context();

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, new[] { HomeRotationOrder[0], HomeRotationOrder[1], HomeRotationOrder[2], HomeRotationOrder[3], HomeRotationOrder[4] },
                AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0], Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void Create_RejectsLiberoAndReplacedPlayerCoexistingOnCourt()
        {
            var context = CreateV4Context();
            var replacement = new LiberoReplacementV3(HomeLiberoId, HomeReplacedPlayerId);

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, HomeRotationOrder, AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0], new[] { replacement }));
        }

        [Test]
        public void Create_RejectsRotationIdsFromTheOpposingOrUnknownRoster()
        {
            var context = CreateV4Context();
            var opposingIdRotation = new[]
            {
                AwayRotationOrder[0], HomeRotationOrder[1], HomeRotationOrder[2],
                HomeRotationOrder[3], HomeRotationOrder[4], HomeRotationOrder[5]
            };
            var unknownIdRotation = new[]
            {
                new PlayerId("unknown-player"), HomeRotationOrder[1], HomeRotationOrder[2],
                HomeRotationOrder[3], HomeRotationOrder[4], HomeRotationOrder[5]
            };

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, opposingIdRotation, AwayRotationOrder, opposingIdRotation[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, unknownIdRotation, AwayRotationOrder, unknownIdRotation[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void OnCourtEligibilitySnapshot_HasNoParameterlessConstructor()
        {
            Assert.That(typeof(OnCourtEligibilitySnapshot).GetConstructor(Type.EmptyTypes), Is.Null);
        }

        [Test]
        public void CanAttempt_BackRowTakeoffBehindAttackLineIsLegalForHomeAndAway()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            var home = AttackEligibilityRulesV3.CanAttempt(
                snapshot.For(HomeRotationOrder[0]),
                new SimVector3(0f, 0f, -3.01f),
                new SimVector3(0f, 2.44f, -1f),
                3f,
                2.43f);
            var away = AttackEligibilityRulesV3.CanAttempt(
                snapshot.For(AwayRotationOrder[0]),
                new SimVector3(0f, 0f, 3.01f),
                new SimVector3(0f, 2.44f, 1f),
                3f,
                2.43f);

            Assert.That(home.IsEligible, Is.True);
            Assert.That(away.IsEligible, Is.True);
        }

        [Test]
        public void CanAttempt_BackRowAboveNetAttackFromFrontZoneIsIllegalForHomeAndAway()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            var home = AttackEligibilityRulesV3.CanAttempt(
                snapshot.For(HomeRotationOrder[0]),
                new SimVector3(0f, 0f, -2.99f),
                new SimVector3(0f, 2.44f, -1f),
                3f,
                2.43f);
            var away = AttackEligibilityRulesV3.CanAttempt(
                snapshot.For(AwayRotationOrder[0]),
                new SimVector3(0f, 0f, 2.99f),
                new SimVector3(0f, 2.44f, 1f),
                3f,
                2.43f);

            Assert.That(home.IsEligible, Is.False);
            Assert.That(away.IsEligible, Is.False);
        }

        [Test]
        public void CanAttempt_FrontZoneContactAtOrBelowNetIsLegal()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            var decision = AttackEligibilityRulesV3.CanAttempt(
                snapshot.For(HomeRotationOrder[0]),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 2.43f, -1f),
                3f,
                2.43f);

            Assert.That(decision.IsEligible, Is.True);
        }

        [Test]
        public void CanAttempt_TreatsTheAttackLineAsFrontZone()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            var decision = AttackEligibilityRulesV3.CanAttempt(
                snapshot.For(HomeRotationOrder[0]),
                new SimVector3(0f, 0f, -3f),
                new SimVector3(0f, 2.44f, -1f),
                3f,
                2.43f);

            Assert.That(decision.IsEligible, Is.False);
        }

        [TestCase(2.43f, true)]
        [TestCase(2.4301f, false)]
        public void CanAttempt_AttackGeometryFactUsesExactNetHeightThreshold(
            float contactHeight,
            bool expectedEligible)
        {
            var snapshot = CreateSnapshot(CreateV4Context());
            var player = snapshot.For(HomeRotationOrder[0]);
            var geometry = new AttackGeometryFactV3(
                player.PlayerId,
                player.Side,
                new SimVector3(0f, 0f, -1f),
                new SimVector3(0f, contactHeight, -0.2f),
                3f,
                2.43f);

            var decision = AttackEligibilityRulesV3.CanAttempt(player, geometry);

            Assert.That(decision.IsEligible, Is.EqualTo(expectedEligible));
            Assert.That(
                decision.Reason,
                Is.EqualTo(
                    expectedEligible
                        ? "eligible attack attempt"
                        : "ineligible above-net front-zone attack"));
        }

        [Test]
        public void CanAttempt_AttackGeometryFactMustMatchEligiblePlayer()
        {
            var snapshot = CreateSnapshot(CreateV4Context());
            var player = snapshot.For(HomeRotationOrder[0]);
            var geometry = new AttackGeometryFactV3(
                HomeRotationOrder[4],
                player.Side,
                new SimVector3(0f, 0f, -3.1f),
                new SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);

            Assert.That(
                () => AttackEligibilityRulesV3.CanAttempt(player, geometry),
                Throws.ArgumentException);
        }

        [Test]
        public void CanAttempt_RejectsMissingPlayerAndInvalidGeometry()
        {
            var snapshot = CreateSnapshot(CreateV4Context());
            var player = snapshot.For(HomeRotationOrder[0]);

            Assert.Throws<ArgumentNullException>(() => AttackEligibilityRulesV3.CanAttempt(
                null, SimVector3.Zero, SimVector3.Zero, 3f, 2.43f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AttackEligibilityRulesV3.CanAttempt(
                player, new SimVector3(float.NaN, 0f, 0f), SimVector3.Zero, 3f, 2.43f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AttackEligibilityRulesV3.CanAttempt(
                player, SimVector3.Zero, SimVector3.Zero, 0f, 2.43f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AttackEligibilityRulesV3.CanAttempt(
                player, SimVector3.Zero, SimVector3.Zero, 3f, float.PositiveInfinity));
        }

        [Test]
        public void CanAttempt_AllLiberosAreIneligibleToBlock()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            var home = BlockEligibilityRulesV3.CanAttempt(snapshot.For(HomeLiberoId));
            var away = BlockEligibilityRulesV3.CanAttempt(snapshot.For(AwayRotationOrder[2]));

            Assert.That(home.IsEligible, Is.False);
            Assert.That(away.IsEligible, Is.False);
        }

        [Test]
        public void CanAttempt_FrontRowNonLiberoIsEligibleToBlock()
        {
            var snapshot = CreateSnapshot(CreateV4Context());

            var decision = BlockEligibilityRulesV3.CanAttempt(snapshot.For(HomeRotationOrder[1]));

            Assert.That(decision.IsEligible, Is.True);
        }

        private static readonly PlayerId HomeLiberoId = new PlayerId("home-libero");
        private static readonly PlayerId HomeReplacedPlayerId = new PlayerId("home-5");

        private static readonly PlayerId[] HomeRotationOrder =
        {
            new PlayerId("home-4"), new PlayerId("home-1"), new PlayerId("home-3"),
            new PlayerId("home-2"), HomeReplacedPlayerId, HomeLiberoId
        };

        private static readonly PlayerId[] AwayRotationOrder =
        {
            new PlayerId("away-3"), new PlayerId("away-1"), new PlayerId("away-libero"),
            new PlayerId("away-4"), new PlayerId("away-5"), new PlayerId("away-2")
        };

        private static OnCourtEligibilitySnapshot CreateSnapshot(MatchContextV4 context)
        {
            return OnCourtLineupRulesV3.Create(
                context, HomeRotationOrder, AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>());
        }

        private static MatchContextV4 CreateV4Context()
        {
            return MatchV4TestFixture.CreateContextForRotations(
                Guid.Parse("d2719a73-5270-4d89-84a5-2040cdd86210"),
                17,
                HomeRotationOrder,
                new[]
                {
                    PlayerPosition.Opposite,
                    PlayerPosition.Setter,
                    PlayerPosition.MiddleBlocker,
                    PlayerPosition.OutsideHitter,
                    PlayerPosition.Defender,
                    PlayerPosition.Libero
                },
                AwayRotationOrder,
                new[]
                {
                    PlayerPosition.MiddleBlocker,
                    PlayerPosition.Setter,
                    PlayerPosition.Libero,
                    PlayerPosition.Opposite,
                    PlayerPosition.Defender,
                    PlayerPosition.OutsideHitter
                });
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Domain;
using Volleyball.Shared.Contracts;
using Volleyball.Presentation;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class MatchSetTests
    {
        [Test]
        public void ResolveRally_ReceivingTeamWins_TakesServiceAndRotatesOnce()
        {
            var set = CreateSet(TeamSide.Home);

            set.ResolveRally(TeamSide.Away, null, null);

            Assert.That(set.AwayScore, Is.EqualTo(1));
            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Away));
            Assert.That(set.RotationOffsetFor(TeamSide.Away), Is.EqualTo(1));
            Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.Zero);
        }

        [Test]
        public void ResolveRally_ServingTeamWins_KeepsServiceWithoutRotation()
        {
            var set = CreateSet(TeamSide.Home);

            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.HomeScore, Is.EqualTo(1));
            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Home));
            Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.Zero);
        }

        [Test]
        public void ReceivingSide_IsAlwaysTheOpponentOfTheServingSide()
        {
            var set = CreateSet(TeamSide.Home);

            Assert.That(set.ReceivingSide, Is.EqualTo(TeamSide.Away));

            set.ResolveRally(TeamSide.Away, null, null);

            Assert.That(set.ReceivingSide, Is.EqualTo(TeamSide.Home));
        }

        [Test]
        public void ResolveRally_AtFourteenAll_RequiresTwoPointLeadToComplete()
        {
            var set = CreateSet(TeamSide.Home);
            Resolve(set, TeamSide.Home, 14);
            Resolve(set, TeamSide.Away, 14);

            set.ResolveRally(TeamSide.Home, null, null);
            Assert.That(set.IsComplete, Is.False);

            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.IsComplete, Is.True);
            Assert.That(set.HomeScore, Is.EqualTo(16));
            Assert.That(set.AwayScore, Is.EqualTo(14));
        }

        [Test]
        public void ResolveRally_AtFiftyEndsSetWithoutATwoPointLead()
        {
            var set = new MatchSet(
                CreateContext(),
                TeamSide.Home,
                new MatchSetRules(15, 2, 50));
            for (var point = 0; point < 49; point++)
            {
                set.ResolveRally(TeamSide.Home, null, null);
                set.ResolveRally(TeamSide.Away, null, null);
            }

            Assert.That(set.IsComplete, Is.False);
            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.IsComplete, Is.True);
            Assert.That(set.HomeScore, Is.EqualTo(50));
            Assert.That(set.AwayScore, Is.EqualTo(49));
            Assert.That(set.WinnerSide, Is.EqualTo(TeamSide.Home));
        }

        [Test]
        public void CalibrationConfiguration_CannotRaiseTheAbsoluteFiftyPointCap()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PhysicalMatchConfiguration.CreateCalibration(
                    PhysicalMatchConfiguration.FormalIndoorSixVsSix,
                    51,
                    1));
        }

        [Test]
        public void MatchSetRules_RejectsMaximumAboveTheAbsoluteFiftyPointCap()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchSetRules(15, 2, 51));
        }

        [Test]
        public void CreateResult_CompletedSet_ContainsAllTwelvePlayersAndValidatedV4Statistics()
        {
            var set = CreateSet(TeamSide.Home);
            var homeSetter = new StablePlayerId("home-setter");
            var awayDefender = new StablePlayerId("away-libero");

            set.RecordContact(homeSetter, 3.5f);
            set.ResolveRally(TeamSide.Home, homeSetter, awayDefender);
            Resolve(set, TeamSide.Home, 14);
            var result = set.CreateResult(acceptedContacts: 1, v3RuleTransitionCount: 1);

            Assert.That(result.PlayerStats, Has.Count.EqualTo(12));
            Assert.That(Stat(result, homeSetter).Points, Is.EqualTo(1));
            Assert.That(Stat(result, homeSetter).Contacts, Is.EqualTo(1));
            Assert.That(Stat(result, homeSetter).Workload, Is.EqualTo(4.5f));
            Assert.That(Stat(result, awayDefender).Errors, Is.EqualTo(1));
            Assert.DoesNotThrow(() => result.ValidateAgainst(set.Context));
        }

        [Test]
        public void CreateResult_IncompleteSet_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CreateSet(TeamSide.Home).CreateResult(0, 0));
        }

        [Test]
        public void FormalIndoorSixPlayerSet_UsesTwentyFivePointsAndAllSixRotationPositions()
        {
            var set = new MatchSet(CreateSixPlayerContext(), TeamSide.Home);
            var initialServers = new[]
            {
                set.ServerFor(TeamSide.Away),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.FrontRight),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.FrontCenter),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.FrontLeft),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.BackLeft),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.BackCenter)
            };

            Assert.That(set.SetTargetScore, Is.EqualTo(25));
            Assert.That(set.RosterSize, Is.EqualTo(6));
            Assert.That(set.IsFrontRow(initialServers[1]), Is.True);
            Assert.That(set.IsFrontRow(initialServers[2]), Is.True);
            Assert.That(set.IsFrontRow(initialServers[3]), Is.True);
            Assert.That(set.IsFrontRow(initialServers[0]), Is.False);
            Assert.That(set.IsFrontRow(initialServers[4]), Is.False);
            Assert.That(set.IsFrontRow(initialServers[5]), Is.False);

            set.ResolveRally(TeamSide.Away, null, null);

            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Away));
            Assert.That(set.ServerFor(TeamSide.Away), Is.EqualTo(initialServers[1]));
            Assert.That(set.RotationPositionFor(initialServers[0]), Is.EqualTo(6));
            Assert.That(set.RotationOffsetFor(TeamSide.Away), Is.EqualTo(1));
        }

        [Test]
        public void FormalIndoorSet_InitialRotationOffsetsApplyBeforeTheFirstServe()
        {
            var context = CreateSixPlayerContext();
            var set = new MatchSet(
                context,
                TeamSide.Away,
                MatchSetRules.FormalIndoor,
                homeInitialRotationOffset: 2,
                awayInitialRotationOffset: 5);

            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Away));
            Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.EqualTo(2));
            Assert.That(set.RotationOffsetFor(TeamSide.Away), Is.EqualTo(5));
            Assert.That(
                set.PlayerAtRotationPosition(TeamSide.Home, 1),
                Is.EqualTo(context.Home.Players[2].PlayerId));
            Assert.That(
                set.PlayerAtRotationPosition(TeamSide.Away, 1),
                Is.EqualTo(context.Away.Players[5].PlayerId));
        }

        [Test]
        public void FormalIndoorSet_RejectsAnInitialRotationOffsetOutsideTheRoster()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchSet(
                CreateSixPlayerContext(),
                TeamSide.Home,
                MatchSetRules.FormalIndoor,
                homeInitialRotationOffset: 6));
        }

        [Test]
        public void FormalIndoorSet_AtTwentyFourAllRequiresTwoPointLead()
        {
            var set = new MatchSet(CreateSixPlayerContext(), TeamSide.Home);
            Resolve(set, TeamSide.Home, 24);
            Resolve(set, TeamSide.Away, 24);

            set.ResolveRally(TeamSide.Home, null, null);
            Assert.That(set.IsComplete, Is.False);
            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.IsComplete, Is.True);
            Assert.That(set.HomeScore, Is.EqualTo(26));
            Assert.That(set.AwayScore, Is.EqualTo(24));
            Assert.That(set.CreateResult(0, 0).PlayerStats, Has.Count.EqualTo(12));
        }

        [Test]
        public void V4Context_ProducesAV4ResultWithTheSameContextIdentityAndAuthoritySummary()
        {
            var context = CreateContext();
            var set = new MatchSet(context, TeamSide.Home);
            Resolve(set, TeamSide.Home, 25);

            var result = set.CreateResult(acceptedContacts: 72, v3RuleTransitionCount: 70);

            Assert.That(set.Context, Is.SameAs(context));
            Assert.That(result.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(result.RalliesPlayed, Is.EqualTo(25));
            Assert.That(result.AcceptedContacts, Is.EqualTo(72));
            Assert.That(result.V3RuleTransitionCount, Is.EqualTo(70));
            Assert.DoesNotThrow(() => result.ValidateAgainst(context));
        }

        [Test]
        public void V4ActiveRosterConstructor_EnumeratesEachInputExactlyOnce()
        {
            var context = CreateContext();
            var homePlayers = new OneShotEnumerable<StablePlayerId>(new[]
            {
                context.Home.Players[0].PlayerId,
                context.Home.Players[1].PlayerId,
                context.Home.Players[2].PlayerId
            });
            var awayPlayers = new OneShotEnumerable<StablePlayerId>(new[]
            {
                context.Away.Players[0].PlayerId,
                context.Away.Players[1].PlayerId,
                context.Away.Players[2].PlayerId
            });

            var set = new MatchSet(
                context,
                homePlayers,
                awayPlayers,
                TeamSide.Home,
                MatchSetRules.ThreeVsThree);

            Assert.That(homePlayers.EnumerationCount, Is.EqualTo(1));
            Assert.That(awayPlayers.EnumerationCount, Is.EqualTo(1));
            Assert.That(set.RosterSize, Is.EqualTo(3));
            Assert.That(
                set.PlayerAtRotationPosition(TeamSide.Home, 1),
                Is.EqualTo(context.Home.Players[0].PlayerId));
            Assert.That(
                set.PlayerAtRotationPosition(TeamSide.Away, 3),
                Is.EqualTo(context.Away.Players[2].PlayerId));
        }

        [Test]
        public void PhysicalDirector_ExposesOnlyTheV4InitializeSignature()
        {
            var initialize = typeof(PhysicalMatchRallyDirector).GetMethod("InitializeV4");

            Assert.That(initialize, Is.Not.Null);
            Assert.That(initialize.GetParameters()[2].ParameterType, Is.EqualTo(typeof(MatchContextV4)));
            Assert.That(typeof(PhysicalMatchRallyDirector).GetMethod("Initialize"), Is.Null);
            Assert.That(typeof(PhysicalMatchRallyDirector).GetMethod("InitializeV2"), Is.Null);
        }

        private static void Resolve(MatchSet set, TeamSide winner, int count)
        {
            for (var index = 0; index < count; index++)
            {
                set.ResolveRally(winner, null, null);
            }
        }

        private static PlayerMatchStatsV4 Stat(MatchResultV4 result, StablePlayerId playerId)
        {
            foreach (var stat in result.PlayerStats)
            {
                if (stat.PlayerId.Equals(playerId))
                {
                    return stat;
                }
            }

            Assert.Fail("Missing stats for " + playerId.Value);
            return null;
        }

        private static MatchSet CreateSet(TeamSide servingSide)
        {
            return new MatchSet(CreateContext(), servingSide, MatchSetRules.ThreeVsThree);
        }

        private static MatchContextV4 CreateContext()
        {
            return MatchV4TestFixture.CreateContext(
                Guid.Parse("11111111-1111-1111-1111-111111111111"));
        }

        private static MatchContextV4 CreateSixPlayerContext()
        {
            return MatchV4TestFixture.CreateContext();
        }

        private sealed class OneShotEnumerable<T> : IEnumerable<T>
        {
            private readonly IReadOnlyList<T> _values;

            public OneShotEnumerable(IReadOnlyList<T> values)
            {
                _values = values;
            }

            public int EnumerationCount { get; private set; }

            public IEnumerator<T> GetEnumerator()
            {
                EnumerationCount++;
                if (EnumerationCount > 1)
                {
                    throw new InvalidOperationException(
                        "The active roster input was enumerated more than once.");
                }

                return _values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}

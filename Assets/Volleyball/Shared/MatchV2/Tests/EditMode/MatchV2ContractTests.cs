using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Shared.MatchV2.EditModeTests
{
    public sealed class MatchV2ContractTests
    {
        [Test]
        public void RunnerPort_HasOnlyTheExactAsynchronousMethod()
        {
            var methods = typeof(IMatchRunnerV2).GetMethods();
            Assert.That(methods, Has.Length.EqualTo(1));
            var method = methods.Single();
            Assert.That(method.Name, Is.EqualTo("ExecuteAsync"));
            Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<MatchResultV2>)));
            Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(MatchContextV2), typeof(CancellationToken) }));
        }

        [Test]
        public void Context_CreatesFixtureWithEightDistinctAxesAndDefensiveCopies()
        {
            var teams = MatchV2TestFactory.CreateTeams();
            var context = MatchV2TestFactory.CreateContext(teams);
            teams[0] = teams[1];

            Assert.That(context.Versions.ContractVersion, Is.EqualTo(2));
            Assert.That(context.MatchSeed, Is.EqualTo(25649701u));
            Assert.That(context.Teams[0].TeamId, Is.EqualTo(new TeamId("team.university.first")));
            Assert.That(context.Teams[0].Players[1].PlayerId,
                Is.EqualTo(new PlayerId("player.career.protagonist")));
            Assert.That(context.Teams[0].Players[1].Abilities.Values,
                Is.EqualTo(new[] { 7123, 6234, 7345, 6456, 7567, 6678, 7789, 6890 }));
            Assert.That(context.ContextHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [TestCase(MatchExecutionModeV2.Fixture, null, null, 1, 1)]
        [TestCase(MatchExecutionModeV2.Direct, "fixture.bad", null, null, null)]
        [TestCase(MatchExecutionModeV2.QuickSimulation, null, null, null, null)]
        public void Context_RejectsIllegalModeVersionPairings(
            MatchExecutionModeV2 mode,
            string fixtureId,
            int? fixtureVersion,
            int? simulationVersion,
            int? randomVersion)
        {
            Assert.That(
                () => MatchV2TestFactory.CreateContext(
                    MatchV2TestFactory.CreateTeams(),
                    mode,
                    fixtureId,
                    fixtureVersion,
                    simulationVersion,
                    randomVersion),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void Context_RejectsDuplicateGlobalPlayerAndInvalidTopology()
        {
            var teams = MatchV2TestFactory.CreateTeams();
            var away = teams[1].Players.ToArray();
            away[0] = new MatchPlayerSnapshotV2(
                teams[0].Players[0].PlayerId, 9, PlayerPositionV2.Opposite, 1, 9000,
                MatchV2TestFactory.Abilities(5000));
            teams[1] = new MatchTeamSnapshotV2(
                teams[1].TeamId, TeamSideV2.Away, away);

            Assert.That(
                () => MatchV2TestFactory.CreateContext(teams),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("unique"));
        }

        [TestCase(-1)]
        [TestCase(10001)]
        public void BasisPointValues_RejectOutOfRange(int value)
        {
            Assert.That(
                () => new MatchAbilitySnapshotV2(value, 1, 2, 3, 4, 5, 6, 7),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void LegacyV1SupportGate_RemainsClosedForV2()
        {
            Assert.That(ContractVersions.SupportsMatch(2), Is.False);
            Assert.That(MatchV2Versions.SupportsContract(2), Is.True);
            Assert.That(MatchV2Versions.SupportsContract(1), Is.False);
        }

        [Test]
        public void StableIdBoundaries_RejectDefaultStructValuesImmediately()
        {
            Assert.That(() => new MatchPlayerSnapshotV2(
                    default, 1, PlayerPositionV2.Opposite, 1, 9000, MatchV2TestFactory.Abilities(5000)),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => new MatchTeamSnapshotV2(
                    default, TeamSideV2.Home, MatchV2TestFactory.CreateTeams()[0].Players),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => MatchV2TestFactory.ZeroFacts(default(PlayerId)),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void Team_RejectsPlayerOrderAndPositionTopologyChanges()
        {
            var players = MatchV2TestFactory.CreateTeams()[0].Players.ToArray();
            (players[0], players[1]) = (players[1], players[0]);
            Assert.That(() => new MatchTeamSnapshotV2(
                    new TeamId("team.invalid.order"), TeamSideV2.Home, players),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("rotation"));

            players = MatchV2TestFactory.CreateTeams()[0].Players.ToArray();
            players[5] = new MatchPlayerSnapshotV2(
                new PlayerId("player.extra.outside"), 6, PlayerPositionV2.OutsideHitter,
                6, 9000, MatchV2TestFactory.Abilities(5000));
            Assert.That(() => new MatchTeamSnapshotV2(
                    new TeamId("team.invalid.positions"), TeamSideV2.Home, players),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("topology"));
        }

        [TestCase(0)]
        [TestCase(100)]
        public void Player_RejectsJerseyOutsideOneToNinetyNine(int jersey)
        {
            Assert.That(() => new MatchPlayerSnapshotV2(
                    new PlayerId("player.invalid.jersey"), jersey, PlayerPositionV2.Opposite,
                    1, 9000, MatchV2TestFactory.Abilities(5000)),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void Team_RejectsDuplicateJerseyNumbers()
        {
            var players = MatchV2TestFactory.CreateTeams()[0].Players.ToArray();
            players[1] = new MatchPlayerSnapshotV2(
                players[1].PlayerId, players[0].JerseyNumber, players[1].Position,
                players[1].RotationSlot, players[1].FitnessBasisPoints, players[1].Abilities);
            Assert.That(() => new MatchTeamSnapshotV2(
                    new TeamId("team.invalid.jerseys"), TeamSideV2.Home, players),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("jersey"));
        }

        [Test]
        public void TeamAndContext_DefensivelyCopyInputCollections()
        {
            var players = MatchV2TestFactory.CreateTeams()[0].Players.ToArray();
            var team = new MatchTeamSnapshotV2(new TeamId("team.copy.home"), TeamSideV2.Home, players);
            var originalFirst = team.Players[0];
            players[0] = players[1];
            Assert.That(team.Players[0], Is.SameAs(originalFirst));

            var teams = new[] { team, MatchV2TestFactory.CreateTeams()[1] };
            var context = MatchV2TestFactory.CreateContext(teams);
            teams[0] = teams[1];
            Assert.That(context.Teams[0], Is.SameAs(team));
        }

        [Test]
        public void Context_AcceptsEveryCompleteModeVersionPairing()
        {
            Assert.DoesNotThrow(() => MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams()));
            Assert.DoesNotThrow(() => MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), MatchExecutionModeV2.Direct,
                null, null, null, null));
            Assert.DoesNotThrow(() => MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), MatchExecutionModeV2.QuickSimulation,
                null, null, 1, 1));
        }

        [Test]
        public void Context_RejectsEveryIncompleteModeVersionPairing()
        {
            var teams = MatchV2TestFactory.CreateTeams();
            Assert.That(() => MatchV2TestFactory.CreateContext(
                teams, MatchExecutionModeV2.Fixture, "fixture.career.u1w1.6v6", 1, 1, 1),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => MatchV2TestFactory.CreateContext(
                teams, MatchExecutionModeV2.Direct, null, null, 1, null),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => MatchV2TestFactory.CreateContext(
                teams, MatchExecutionModeV2.Direct, null, null, null, 1),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => MatchV2TestFactory.CreateContext(
                teams, MatchExecutionModeV2.QuickSimulation, null, null, 1, null),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => MatchV2TestFactory.CreateContext(
                teams, MatchExecutionModeV2.QuickSimulation, null, null, null, 1),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => MatchV2TestFactory.CreateContext(
                teams, MatchExecutionModeV2.QuickSimulation, "fixture.career.u1w1.6v6", 1, 1, 1),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void PublicBoundaries_RejectNullCollectionsAndMembers()
        {
            Assert.That(() => new MatchPlayerSnapshotV2(
                    new PlayerId("player.null.abilities"), 1, PlayerPositionV2.Opposite,
                    1, 9000, null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new MatchTeamSnapshotV2(
                    new TeamId("team.null.players"), TeamSideV2.Home, null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => MatchV2TestFactory.CreateContext(null),
                Throws.TypeOf<ArgumentNullException>());

            var players = MatchV2TestFactory.CreateTeams()[0].Players.ToArray();
            players[0] = null;
            Assert.That(() => new MatchTeamSnapshotV2(
                    new TeamId("team.null.member"), TeamSideV2.Home, players),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void Context_AcceptsBothUint32SeedBoundaries()
        {
            var zero = MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), matchSeed: 0u);
            var maximum = MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), matchSeed: uint.MaxValue);
            Assert.That(zero.MatchSeed, Is.Zero);
            Assert.That(maximum.MatchSeed, Is.EqualTo(uint.MaxValue));
            Assert.That(zero.ContextHash, Is.Not.EqualTo(maximum.ContextHash));
        }
    }
}

using System;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FormalMatchScenarioPresetV4Tests
    {
        [Test]
        public void CompleteInputs_CreateStableScenarioHashAndRuntimeTactics()
        {
            var first = CreateScenario();
            var second = CreateScenario();

            Assert.That(first.ContentHash, Is.EqualTo(second.ContentHash));
            Assert.That(first.Context.ContextHash,
                Is.EqualTo(second.Context.ContextHash));
            Assert.That(first.CreateTactics().Blue.SetRoute,
                Is.EqualTo(SetRoute.RightPin));
            Assert.That(first.CreateTactics().Orange.SpikeRoute,
                Is.EqualTo(SpikeRoute.Line));
        }

        [Test]
        public void CompleteInputs_ChangingAnInitialRotationChangesScenarioHash()
        {
            var first = CreateScenario();
            var changed = CreateScenario(homeRotationOffset: 2);

            Assert.That(changed.ContentHash, Is.Not.EqualTo(first.ContentHash));
        }

        [Test]
        public void CompleteInputs_ChangingAPlayerSnapshotChangesScenarioHash()
        {
            var first = CreateScenario();
            var context = MatchV4TestFixture.CreateContext(
                Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var changedContext = MatchContextV4.Create(
                context.SessionId,
                context.Seed + 1,
                context.Home,
                context.Away,
                context.PhysicsConfigurationHash,
                context.TrajectoryPredictionProviderConfiguration,
                context.RulesVersion);
            var changed = CreateScenario(context: changedContext);

            Assert.That(changed.ContentHash, Is.Not.EqualTo(first.ContentHash));
        }

        [Test]
        public void CompleteInputs_RejectsUnsupportedConfigurationAndInvalidOffsets()
        {
            Assert.Throws<ArgumentException>(() => new FormalMatchScenarioDefinitionV4(
                "bad-configuration",
                FormalMatchScenarioDefinitionV4.FormatVersion,
                MatchV4TestFixture.CreateContext(),
                TeamSide.Home,
                0,
                0,
                "test-only-configuration",
                CreateTactics(),
                CreateTactics(),
                new FormalMatchAiInputV4()));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateScenario(
                homeRotationOffset: 6));
        }

        [Test]
        public void CompleteInputs_RejectsTamperedContentHash()
        {
            var scenario = CreateScenario();

            Assert.Throws<ArgumentException>(() => new FormalMatchScenarioDefinitionV4(
                scenario.ScenarioId,
                scenario.FormatVersionValue,
                scenario.Context,
                scenario.FirstServingSide,
                scenario.HomeInitialRotationOffset,
                scenario.AwayInitialRotationOffset,
                scenario.ConfigurationIdentity,
                scenario.HomeTactics,
                scenario.AwayTactics,
                scenario.Ai,
                new string('0', 64)));
        }

        private static FormalMatchScenarioDefinitionV4 CreateScenario(
            int homeRotationOffset = 1,
            MatchContextV4 context = null)
        {
            return new FormalMatchScenarioDefinitionV4(
                "formal-scenario-test",
                FormalMatchScenarioDefinitionV4.FormatVersion,
                context ?? MatchV4TestFixture.CreateContext(
                    Guid.Parse("11111111-1111-1111-1111-111111111111")),
                TeamSide.Away,
                homeRotationOffset,
                4,
                FormalMatchScenarioDefinitionV4.FormalIndoorConfigurationIdentity,
                CreateTactics(),
                CreateTactics(SpikeRoute.Line),
                new FormalMatchAiInputV4());
        }

        private static FormalMatchTacticInputV4 CreateTactics(
            SpikeRoute spikeRoute = SpikeRoute.CrossCourt)
        {
            return new FormalMatchTacticInputV4
            {
                SetRoute = Volleyball.AI.SetRoute.RightPin,
                SpikeRoute = spikeRoute,
                SetterX = .45f,
                SetterZ = -3.35f,
                AttackerX = 3.15f,
                AttackerZ = -1.2f,
                DefenderX = -2.4f,
                DefenderZ = -5.25f,
                Blocker = Volleyball.Domain.Prototype.PlayerRole.Attacker,
                BlockX = 3.15f,
                BlockZ = .65f,
                CoverReceiver = Volleyball.Domain.Prototype.PlayerRole.Setter,
                CoverX = -1.1f,
                CoverZ = 4.15f,
                SetRhythm = Volleyball.AI.SetRhythm.FastPin,
                AttackFlightSeconds = .45f
            };
        }
    }
}

using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class PhysicalRallyTacticPlannerTests
    {
        [Test]
        public void Create_ReturnsTheFixedBaseline()
        {
            var planner = new PhysicalRallyTacticPlanner();
            var tactics = planner.Create();

            Assert.That(tactics.Blue.SetRoute, Is.EqualTo(SetRoute.LeftPin));
            Assert.That(tactics.Blue.SpikeRoute, Is.EqualTo(SpikeRoute.CrossCourt));
            Assert.That(tactics.Orange.SetRoute, Is.EqualTo(SetRoute.LeftPin));
            Assert.That(tactics.Orange.SpikeRoute, Is.EqualTo(SpikeRoute.CrossCourt));
        }

        [Test]
        public void Create_MapsRoutesToRhythmMetadataInsteadOfFixedSetDurations()
        {
            var tactics = new PhysicalRallyTacticPlanner().Create();
            AssertRhythm(tactics.Blue);
            AssertRhythm(tactics.Orange);
        }

        [Test]
        public void Create_KeepsEveryMovementTargetInsidePlayableCourt()
        {
            var tactics = new PhysicalRallyTacticPlanner().Create();
            AssertInside(tactics.Blue);
            AssertInside(tactics.Orange);
        }

        [Test]
        public void Create_BlueBlockCoverageTracksOrangeAttackLane()
        {
            var tactics = new PhysicalRallyTacticPlanner().Create();

            Assert.That(
                tactics.Blue.BlockPosition.X,
                Is.EqualTo(tactics.Orange.AttackerPosition.X).Within(0.001f));
            Assert.That(tactics.Blue.BlockPosition.Z, Is.LessThan(0f));
        }

        [Test]
        public void Create_OrangeBlockCoverageTracksBlueAttackLane()
        {
            var tactics = new PhysicalRallyTacticPlanner().Create();

            Assert.That(
                tactics.Orange.BlockPosition.X,
                Is.EqualTo(tactics.Blue.AttackerPosition.X).Within(0.001f));
            Assert.That(tactics.Orange.BlockPosition.Z, Is.GreaterThan(0f));
        }

        [Test]
        public void PlanBlockCoverage_AttackerTakesPrimaryBlockWhenCloseToAttackLane()
        {
            var plan = PhysicalRallyTacticPlanner.PlanBlockCoverage(
                new CourtPoint(2.8f, 2.4f),
                TeamSideSign.Orange);

            Assert.That(plan.Blocker, Is.EqualTo(PlayerRole.Attacker));
            Assert.That(plan.BlockPosition.X, Is.EqualTo(2.8f).Within(0.01f));
            Assert.That(plan.BlockPosition.Z, Is.EqualTo(0.65f).Within(0.01f));
            Assert.That(plan.CoverReceiver, Is.EqualTo(PlayerRole.Setter));
            Assert.That(plan.CoverPosition.Z, Is.GreaterThan(3.0f));
        }

        [Test]
        public void PlanBlockCoverage_SetterCanBlockWhenSetterIsCloserToMiddleLane()
        {
            var plan = PhysicalRallyTacticPlanner.PlanBlockCoverage(
                new CourtPoint(0.15f, -2.1f),
                TeamSideSign.Blue);

            Assert.That(plan.Blocker, Is.EqualTo(PlayerRole.Setter));
            Assert.That(plan.BlockPosition.X, Is.EqualTo(0.15f).Within(0.01f));
            Assert.That(plan.BlockPosition.Z, Is.EqualTo(-0.65f).Within(0.01f));
            Assert.That(plan.CoverReceiver, Is.EqualTo(PlayerRole.Attacker));
            Assert.That(plan.CoverPosition.Z, Is.LessThan(-3.0f));
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void PlanBlockCoverage_WithTeamIdPlacesBlockOnDefendingCourt(TeamId defendingTeam)
        {
            var plan = PhysicalRallyTacticPlanner.PlanBlockCoverage(
                new CourtPoint(1.75f, 2.4f),
                defendingTeam);

            Assert.That(plan.BlockPosition.X, Is.EqualTo(1.75f).Within(0.01f));
            if (defendingTeam == TeamId.Blue)
            {
                Assert.That(plan.BlockPosition.Z, Is.LessThan(0f));
            }
            else
            {
                Assert.That(plan.BlockPosition.Z, Is.GreaterThan(0f));
            }
        }

        private static void AssertInside(TeamRallyTactic tactic)
        {
            Assert.That(tactic.SetterPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.AttackerPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.DefenderPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.BlockPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.CoverPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.SetterPosition.Z, Is.InRange(-9f, 9f));
            Assert.That(tactic.AttackerPosition.Z, Is.InRange(-9f, 9f));
            Assert.That(tactic.DefenderPosition.Z, Is.InRange(-9f, 9f));
            Assert.That(tactic.BlockPosition.Z, Is.InRange(-9f, 9f));
            Assert.That(tactic.CoverPosition.Z, Is.InRange(-9f, 9f));
        }

        private static void AssertRhythm(TeamRallyTactic tactic)
        {
            var expected = tactic.SetRoute switch
            {
                SetRoute.MiddleQuick => SetRhythm.CloseQuick,
                SetRoute.BackSet => SetRhythm.BackQuick,
                _ => SetRhythm.FastPin
            };
            Assert.That(tactic.SetRhythm, Is.EqualTo(expected));
        }
    }
}

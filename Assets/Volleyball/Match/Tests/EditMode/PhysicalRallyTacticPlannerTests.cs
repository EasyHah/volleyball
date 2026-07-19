using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class PhysicalRallyTacticPlannerTests
    {
        [Test]
        public void Create_ReplaysExactlyForSameSeedAndRevision()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);

            Assert.That(planner.Create(8), Is.EqualTo(planner.Create(8)));
            Assert.That(
                new PhysicalRallyTacticPlanner(999).Create(8),
                Is.Not.EqualTo(planner.Create(8)));
        }

        [Test]
        public void Create_CoversMultipleSetSpikeAndMovementRoutes()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);
            var sets = new HashSet<SetRoute>();
            var spikes = new HashSet<SpikeRoute>();
            var attackPositions = new HashSet<float>();
            var defensePositions = new HashSet<float>();

            for (var revision = 0; revision < 32; revision++)
            {
                var tactics = planner.Create(revision);
                sets.Add(tactics.Blue.SetRoute);
                sets.Add(tactics.Orange.SetRoute);
                spikes.Add(tactics.Blue.SpikeRoute);
                spikes.Add(tactics.Orange.SpikeRoute);
                attackPositions.Add(tactics.Blue.AttackerPosition.X);
                defensePositions.Add(tactics.Orange.DefenderPosition.X);
            }

            Assert.That(sets.Count, Is.EqualTo(4));
            Assert.That(spikes.Count, Is.EqualTo(4));
            Assert.That(attackPositions.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(defensePositions.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Create_KeepsEveryMovementTargetInsidePlayableCourt()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);
            for (var revision = 0; revision < 64; revision++)
            {
                var tactics = planner.Create(revision);
                AssertInside(tactics.Blue);
                AssertInside(tactics.Orange);
            }
        }

        [Test]
        public void Create_BackSetPlacesAttackerBehindSetterForEitherTeam()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);
            var blueBackSets = 0;
            var orangeBackSets = 0;

            for (var revision = 0; revision < 64; revision++)
            {
                var tactics = planner.Create(revision);
                if (tactics.Blue.SetRoute == SetRoute.BackSet)
                {
                    blueBackSets++;
                    Assert.That(
                        tactics.Blue.AttackerPosition.Z,
                        Is.LessThan(tactics.Blue.SetterPosition.Z));
                }

                if (tactics.Orange.SetRoute == SetRoute.BackSet)
                {
                    orangeBackSets++;
                    Assert.That(
                        tactics.Orange.AttackerPosition.Z,
                        Is.GreaterThan(tactics.Orange.SetterPosition.Z));
                }
            }

            Assert.That(blueBackSets, Is.GreaterThan(0));
            Assert.That(orangeBackSets, Is.GreaterThan(0));
        }

        [Test]
        public void Create_BackSetUsesLongerAttackFlightThanFrontRowSpike()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);
            var checkedBackSets = 0;

            for (var revision = 0; revision < 64; revision++)
            {
                var tactics = planner.Create(revision);
                if (tactics.Blue.SetRoute == SetRoute.BackSet &&
                    tactics.Blue.SpikeRoute != SpikeRoute.RollShot)
                {
                    checkedBackSets++;
                    Assert.That(tactics.Blue.AttackFlightSeconds, Is.EqualTo(0.625f).Within(0.0001f));
                }

                if (tactics.Orange.SetRoute == SetRoute.BackSet &&
                    tactics.Orange.SpikeRoute != SpikeRoute.RollShot)
                {
                    checkedBackSets++;
                    Assert.That(tactics.Orange.AttackFlightSeconds, Is.EqualTo(0.625f).Within(0.0001f));
                }
            }

            Assert.That(checkedBackSets, Is.GreaterThan(0));
        }

        [Test]
        public void Create_BlueBlockCoverageTracksOrangeAttackLane()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);

            for (var revision = 0; revision < 64; revision++)
            {
                var tactics = planner.Create(revision);

                Assert.That(
                    tactics.Blue.BlockPosition.X,
                    Is.EqualTo(tactics.Orange.AttackerPosition.X).Within(0.001f));
                Assert.That(tactics.Blue.BlockPosition.Z, Is.LessThan(0f));
            }
        }

        [Test]
        public void Create_OrangeBlockCoverageTracksBlueAttackLane()
        {
            var planner = new PhysicalRallyTacticPlanner(7351);

            for (var revision = 0; revision < 64; revision++)
            {
                var tactics = planner.Create(revision);

                Assert.That(
                    tactics.Orange.BlockPosition.X,
                    Is.EqualTo(tactics.Blue.AttackerPosition.X).Within(0.001f));
                Assert.That(tactics.Orange.BlockPosition.Z, Is.GreaterThan(0f));
            }
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
    }
}

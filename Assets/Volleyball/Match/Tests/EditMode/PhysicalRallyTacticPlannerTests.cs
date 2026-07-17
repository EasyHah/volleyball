using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.AI;

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

        private static void AssertInside(TeamRallyTactic tactic)
        {
            Assert.That(tactic.SetterPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.AttackerPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.DefenderPosition.X, Is.InRange(-4.5f, 4.5f));
            Assert.That(tactic.SetterPosition.Z, Is.InRange(-9f, 9f));
            Assert.That(tactic.AttackerPosition.Z, Is.InRange(-9f, 9f));
            Assert.That(tactic.DefenderPosition.Z, Is.InRange(-9f, 9f));
        }
    }
}

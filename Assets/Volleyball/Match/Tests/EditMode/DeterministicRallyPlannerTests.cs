using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class DeterministicRallyPlannerTests
    {
        [Test]
        public void CreatePlan_SameInputs_ProducesSamePlan()
        {
            var first = new DeterministicRallyPlanner(7429).CreatePlan(TeamId.Blue, 4);
            var second = new DeterministicRallyPlanner(7429).CreatePlan(TeamId.Blue, 4);

            Assert.That(second.WinningTeam, Is.EqualTo(first.WinningTeam));
            CollectionAssert.AreEqual(first.Contacts, second.Contacts);
        }

        [Test]
        public void CreatePlan_DifferentSeeds_ProduceDifferentPlans()
        {
            var first = new DeterministicRallyPlanner(1).CreatePlan(TeamId.Blue, 0);
            var second = new DeterministicRallyPlanner(2).CreatePlan(TeamId.Blue, 0);

            var plansAreIdentical = first.WinningTeam == second.WinningTeam
                && first.Contacts.SequenceEqual(second.Contacts);

            Assert.That(plansAreIdentical, Is.False);
        }

        [Test]
        public void CreatePlan_ContainsRequiredRallyActionsWithinContactBounds()
        {
            var plan = new DeterministicRallyPlanner(7429).CreatePlan(TeamId.Blue, 0);
            var kinds = plan.Contacts.Select(contact => contact.Kind).ToArray();

            Assert.That(kinds[0], Is.EqualTo(RallyActionKind.Serve));
            Assert.That(kinds, Does.Contain(RallyActionKind.Receive));
            Assert.That(kinds, Does.Contain(RallyActionKind.Set));
            Assert.That(kinds, Does.Contain(RallyActionKind.Spike));
            Assert.That(
                kinds.Any(kind => kind == RallyActionKind.Block || kind == RallyActionKind.Dig),
                Is.True);
            Assert.That(kinds.Length, Is.InRange(5, 9));
        }

        [Test]
        public void CreatePlan_EachActorMeetsThePreviousBallTarget()
        {
            var planner = new DeterministicRallyPlanner(7429);

            for (var rallyNumber = 0; rallyNumber < 32; rallyNumber++)
            {
                var contacts = planner.CreatePlan(TeamId.Blue, rallyNumber).Contacts;

                for (var contactIndex = 1; contactIndex < contacts.Count; contactIndex++)
                {
                    Assert.That(
                        contacts[contactIndex].ActorTarget,
                        Is.EqualTo(contacts[contactIndex - 1].BallTarget),
                        $"Rally {rallyNumber}, contact {contactIndex} does not meet the ball.");
                }
            }
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void CreatePlan_SequencesTeamsRolesAndActionsLegally(TeamId servingTeam)
        {
            var receivingTeam = Opposite(servingTeam);
            var contacts = new DeterministicRallyPlanner(7429).CreatePlan(servingTeam, 0).Contacts;

            CollectionAssert.AreEqual(
                new[]
                {
                    servingTeam,
                    receivingTeam,
                    receivingTeam,
                    receivingTeam,
                    servingTeam,
                    servingTeam,
                    servingTeam
                },
                contacts.Select(contact => contact.Actor.Team).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    PlayerRole.Attacker,
                    PlayerRole.Defender,
                    PlayerRole.Setter,
                    PlayerRole.Attacker,
                    PlayerRole.Attacker,
                    PlayerRole.Setter,
                    PlayerRole.Attacker
                },
                contacts.Select(contact => contact.Actor.Role).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    RallyActionKind.Serve,
                    RallyActionKind.Receive,
                    RallyActionKind.Set,
                    RallyActionKind.Spike
                },
                contacts.Take(4).Select(contact => contact.Kind).ToArray());
            Assert.That(
                contacts[4].Kind,
                Is.EqualTo(RallyActionKind.Block).Or.EqualTo(RallyActionKind.Dig));
            CollectionAssert.AreEqual(
                new[] { RallyActionKind.Set, RallyActionKind.Spike },
                contacts.Skip(5).Select(contact => contact.Kind).ToArray());
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void CreatePlan_FinalServingAttackMatchesTheWinningOutcome(TeamId servingTeam)
        {
            var planner = new DeterministicRallyPlanner(7429);
            var receivingTeam = Opposite(servingTeam);
            var receivingHalfSign = receivingTeam == TeamId.Blue ? -1 : 1;

            for (var rallyNumber = 0; rallyNumber < 32; rallyNumber++)
            {
                var plan = planner.CreatePlan(servingTeam, rallyNumber);
                var finalContact = plan.Contacts[plan.Contacts.Count - 1];

                Assert.That(finalContact.Actor.Team, Is.EqualTo(servingTeam));
                Assert.That(finalContact.Actor.Role, Is.EqualTo(PlayerRole.Attacker));
                Assert.That(finalContact.Kind, Is.EqualTo(RallyActionKind.Spike));
                Assert.That(Math.Sign(finalContact.BallTarget.Z), Is.EqualTo(receivingHalfSign));
                if (plan.WinningTeam == servingTeam)
                {
                    Assert.That(Math.Abs(finalContact.BallTarget.Z), Is.LessThanOrEqualTo(7.5f));
                }
                else
                {
                    Assert.That(plan.WinningTeam, Is.EqualTo(receivingTeam));
                    Assert.That(Math.Abs(finalContact.BallTarget.Z), Is.GreaterThan(7.5f));
                }
            }
        }

        [Test]
        public void CreatePlan_BoundedSampleUsesFixedValidShapeAndExercisesChoices()
        {
            var planner = new DeterministicRallyPlanner(7429);
            var winners = new HashSet<TeamId>();
            var defenses = new HashSet<RallyActionKind>();

            for (var rallyNumber = 0; rallyNumber < 32; rallyNumber++)
            {
                var plan = planner.CreatePlan(TeamId.Blue, rallyNumber);

                Assert.That(plan.Contacts, Has.Count.EqualTo(7));
                winners.Add(plan.WinningTeam);
                defenses.Add(plan.Contacts[4].Kind);
                foreach (var contact in plan.Contacts)
                {
                    Assert.That(contact.ContactHeight, Is.GreaterThan(0f));
                    Assert.That(contact.BallTargetHeight, Is.GreaterThan(0f));
                    Assert.That(contact.DurationSeconds, Is.GreaterThan(0f));
                    Assert.That(contact.ArcHeight, Is.GreaterThan(0f));
                }
            }

            CollectionAssert.AreEquivalent(new[] { TeamId.Blue, TeamId.Orange }, winners);
            CollectionAssert.AreEquivalent(
                new[] { RallyActionKind.Block, RallyActionKind.Dig },
                defenses);
        }

        [Test]
        public void CreatePlan_RejectsInvalidInputs()
        {
            var planner = new DeterministicRallyPlanner(7429);

            Assert.Throws<ArgumentOutOfRangeException>(() => planner.CreatePlan((TeamId)123, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.CreatePlan(TeamId.Blue, -1));
        }

        private static TeamId Opposite(TeamId team)
        {
            return team == TeamId.Blue ? TeamId.Orange : TeamId.Blue;
        }
    }
}

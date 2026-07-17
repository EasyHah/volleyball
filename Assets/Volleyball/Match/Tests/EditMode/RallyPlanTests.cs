using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class RallyPlanTests
    {
        [Test]
        public void Constructor_DefensivelyCopiesContacts()
        {
            var firstContact = CreateContact();
            var callerContacts = new List<ContactPlan> { firstContact };
            var plan = new RallyPlan(callerContacts, TeamId.Blue);

            callerContacts[0] = CreateContact(kind: RallyActionKind.Dig);
            callerContacts.Add(CreateContact(kind: RallyActionKind.Block));

            Assert.That(plan.Contacts, Has.Count.EqualTo(1));
            Assert.That(plan.Contacts[0], Is.SameAs(firstContact));
            Assert.That(plan.WinningTeam, Is.EqualTo(TeamId.Blue));
        }

        [Test]
        public void Constructor_RejectsNullContactsCollection()
        {
            Assert.Throws<ArgumentNullException>(() => new RallyPlan(null, TeamId.Blue));
        }

        [Test]
        public void Constructor_RejectsNullContactEntry()
        {
            Assert.Throws<ArgumentException>(
                () => new RallyPlan(new ContactPlan[] { null }, TeamId.Blue));
        }

        [Test]
        public void ContactPlan_EqualityUsesEveryFieldAndConsistentHashes()
        {
            var original = CreateContact();
            var equal = CreateContact();
            var variants = new[]
            {
                CreateContact(actor: new PlayerId(TeamId.Orange, PlayerRole.Setter)),
                CreateContact(kind: RallyActionKind.Receive),
                CreateContact(actorTarget: new CourtPoint(10f, 2f)),
                CreateContact(ballTarget: new CourtPoint(3f, 40f)),
                CreateContact(contactHeight: 50f),
                CreateContact(ballTargetHeight: 60f),
                CreateContact(durationSeconds: 70f),
                CreateContact(arcHeight: 80f)
            };

            Assert.That(original.Equals(equal), Is.True);
            Assert.That(original.Equals((object)equal), Is.True);
            Assert.That(original.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            foreach (var variant in variants)
            {
                Assert.That(original.Equals(variant), Is.False);
            }
        }

        [Test]
        public void PlayerId_HasValueEqualityAndConsistentHashes()
        {
            var player = new PlayerId(TeamId.Blue, PlayerRole.Setter);
            var equal = new PlayerId(TeamId.Blue, PlayerRole.Setter);

            Assert.That(player.Equals(equal), Is.True);
            Assert.That(player.Equals((object)equal), Is.True);
            Assert.That(player.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(player.Equals(new PlayerId(TeamId.Orange, PlayerRole.Setter)), Is.False);
            Assert.That(player.Equals(new PlayerId(TeamId.Blue, PlayerRole.Defender)), Is.False);
        }

        [Test]
        public void CourtPoint_HasValueEqualityAndConsistentHashes()
        {
            var point = new CourtPoint(1f, 2f);
            var equal = new CourtPoint(1f, 2f);

            Assert.That(point.Equals(equal), Is.True);
            Assert.That(point.Equals((object)equal), Is.True);
            Assert.That(point.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(point.Equals(new CourtPoint(10f, 2f)), Is.False);
            Assert.That(point.Equals(new CourtPoint(1f, 20f)), Is.False);
        }

        private static ContactPlan CreateContact(
            PlayerId? actor = null,
            RallyActionKind kind = RallyActionKind.Serve,
            CourtPoint? actorTarget = null,
            CourtPoint? ballTarget = null,
            float contactHeight = 5f,
            float ballTargetHeight = 6f,
            float durationSeconds = 7f,
            float arcHeight = 8f)
        {
            return new ContactPlan(
                actor ?? new PlayerId(TeamId.Blue, PlayerRole.Setter),
                kind,
                actorTarget ?? new CourtPoint(1f, 2f),
                ballTarget ?? new CourtPoint(3f, 4f),
                contactHeight,
                ballTargetHeight,
                durationSeconds,
                arcHeight);
        }
    }
}

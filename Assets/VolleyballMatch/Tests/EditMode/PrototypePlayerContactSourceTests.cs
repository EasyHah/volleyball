using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Domain.Prototype;
using VolleyballMatch.Domain.Simulation;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class PrototypePlayerContactSourceTests
    {
        [Test]
        public void ScheduledSet_ActivatesTwoVisiblePalmSurfacesAtContactTime()
        {
            var playerObject = new GameObject("ScheduledSetter");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Setter), Color.blue, "1");
                var noError = new SkillExecutionError(
                    0f,
                    SimVector3.Zero,
                    SimVector3.Zero,
                    0f,
                    1f,
                    SimVector3.Zero,
                    0.7f);
                player.ScheduleContact(
                    TechniqueAction.Set,
                    2f,
                    new SimVector3(0f, 7f, 3f),
                    noError,
                    40);
                var before = new List<BallContactCandidate>();
                var contact = new List<BallContactCandidate>();

                player.CollectContacts(1.8f, 1f / 120f, before);
                player.CollectContacts(2f, 1f / 120f, contact);

                Assert.That(before.Count, Is.EqualTo(2));
                Assert.That(before[0].Surface.Active, Is.False);
                Assert.That(contact.Count, Is.EqualTo(2));
                Assert.That(contact[0].Surface.Active, Is.True);
                Assert.That(contact[1].Surface.ContactGroupId, Is.EqualTo(40));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScheduledAttack_HasContinuousJumpAtContactAndReturnsToGround()
        {
            var playerObject = new GameObject("ScheduledAttacker");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Attacker), Color.blue, "2");
                var noError = new SkillExecutionError(
                    0f,
                    SimVector3.Zero,
                    SimVector3.Zero,
                    0f,
                    1f,
                    SimVector3.Zero,
                    0.25f);
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    5f,
                    new SimVector3(0f, -4f, 14f),
                    noError,
                    55);
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(4.62f, 1f / 120f, contacts);
                var takeoffHeight = player.transform.position.y;
                contacts.Clear();
                player.CollectContacts(4.98f, 1f / 120f, contacts);
                var palmBeforeContact = player.transform.InverseTransformPoint(
                    player.Rig.GetJoint("RightPalm").position).y;
                contacts.Clear();
                player.CollectContacts(5f, 1f / 120f, contacts);
                var contactHeight = player.transform.position.y;
                var palmAtContact = player.transform.InverseTransformPoint(
                    player.Rig.GetJoint("RightPalm").position).y;
                contacts.Clear();
                player.CollectContacts(5.02f, 1f / 120f, contacts);
                var palmAfterContact = player.transform.InverseTransformPoint(
                    player.Rig.GetJoint("RightPalm").position).y;
                contacts.Clear();
                player.CollectContacts(5.7f, 1f / 120f, contacts);

                Assert.That(takeoffHeight, Is.EqualTo(0f).Within(0.001f));
                Assert.That(contactHeight, Is.GreaterThan(0.8f));
                Assert.That(palmBeforeContact, Is.GreaterThan(palmAtContact));
                Assert.That(palmAfterContact, Is.LessThan(palmAtContact));
                Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(player.transform.position.z, Is.GreaterThan(0.4f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void PreviewContactFrames_UsesClosedSetHandsAndAttackJumpHeight()
        {
            var playerObject = new GameObject("ContactPreviewPlayer");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Attacker), Color.blue, "2");

                var setFrames = player.PreviewContactFrames(TechniqueAction.Set);
                var attackFrames = player.PreviewContactFrames(TechniqueAction.Attack);
                var attackBallCenter = attackFrames[0].Origin +
                                       (attackFrames[0].Normal * SimulatedBall.DefaultRadius);

                Assert.That(setFrames.Count, Is.EqualTo(2));
                Assert.That(
                    (setFrames[0].Origin - setFrames[1].Origin).Magnitude,
                    Is.LessThan(0.28f));
                Assert.That(attackFrames[0].Origin.Y, Is.GreaterThan(3f));
                Assert.That(attackBallCenter.Z, Is.GreaterThan(0.95f));
                Assert.That(Mathf.Abs(attackBallCenter.X), Is.LessThan(0.4f));
                Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }
    }
}

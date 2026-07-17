using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
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
        public void ScheduledSet_DrawsHandsBackThenPopsTowardDirectionalContactPose()
        {
            var playerObject = new GameObject("DirectionalSetter");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Setter), Color.blue, "1");
                player.SetAbility(new PlayerAbilityProfile(1f, 1f, 1f, 1f, 1f, 1f, 1f));
                var noError = new SkillExecutionError(
                    0f,
                    SimVector3.Zero,
                    SimVector3.Zero,
                    0f,
                    1f,
                    SimVector3.Zero,
                    1f);
                player.ScheduleContact(
                    TechniqueAction.Set,
                    2f,
                    new SimVector3(6f, 7f, 1f),
                    noError,
                    41);
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(1.65f, 1f / 120f, contacts);
                var drawnHands = AveragePalms(player);
                contacts.Clear();
                player.CollectContacts(2f, 1f / 120f, contacts);
                var poppedHands = AveragePalms(player);
                foreach (var sampleTime in new[] { 2.10f, 2.20f, 2.30f, 2.40f, 2.49f })
                {
                    contacts.Clear();
                    player.CollectContacts(sampleTime, 1f / 120f, contacts);
                }

                var recoveredHands = AveragePalms(player);

                Assert.That(player.CurrentSetStyle, Is.EqualTo(SetTechniqueStyle.SideRightTwoHand));
                Assert.That(Vector3.Distance(drawnHands, poppedHands), Is.GreaterThan(0.08f));
                Assert.That(poppedHands.y, Is.GreaterThan(drawnHands.y));
                Assert.That(recoveredHands.y, Is.LessThan(poppedHands.y - 0.08f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScheduledEmergencySet_UsesOnePalmOnlyForEliteSetter()
        {
            var playerObject = new GameObject("EmergencySetter");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Setter), Color.blue, "1");
                player.SetAbility(new PlayerAbilityProfile(1f, 1f, 1f, 1f, 0.95f, 1f, 1f));
                var noError = new SkillExecutionError(
                    0f,
                    SimVector3.Zero,
                    SimVector3.Zero,
                    0f,
                    1f,
                    SimVector3.Zero,
                    1f);
                player.ScheduleContact(
                    TechniqueAction.Set,
                    2f,
                    new SimVector3(6f, 7f, 1f),
                    noError,
                    42,
                    emergencyOneHand: true);
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(2f, 1f / 120f, contacts);

                Assert.That(player.CurrentSetStyle, Is.EqualTo(SetTechniqueStyle.OneHandRight));
                Assert.That(contacts, Has.Count.EqualTo(1));
                Assert.That(contacts[0].Surface.Active, Is.True);
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

        [Test]
        public void ScheduledMovement_ReachesNearbyTacticalPositionBeforeContact()
        {
            var playerObject = new GameObject("MovingDefender");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Defender), Color.blue, "3");
                player.SetAbility(new PlayerAbilityProfile(0.9f, 1f, 0.8f, 1f, 0.8f, 0.8f, 0.8f));
                var target = new Vector3(2f, 0f, -1f);
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    2f,
                    new SimVector3(0f, 5f, 4f),
                    NoExecutionError(),
                    61,
                    movementTarget: target,
                    movementStartSimulationTime: 0f);
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(0.8f, 1f / 120f, contacts);
                var movingPosition = player.transform.position;
                contacts.Clear();
                player.CollectContacts(1.95f, 1f / 120f, contacts);

                Assert.That(movingPosition.sqrMagnitude, Is.GreaterThan(0.01f));
                Assert.That(Vector3.Distance(player.ScheduledMovementTarget, target), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(player.transform.position, target), Is.LessThan(0.2f));
                Assert.That(player.MovementShortfall, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScheduledMovement_LeavesMeasuredShortfallWhenTargetIsUnreachable()
        {
            var playerObject = new GameObject("LateDefender");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Orange, PlayerRole.Defender), Color.red, "6");
                player.SetAbility(new PlayerAbilityProfile(0.2f, 0.5f, 0.8f, 1f, 0.8f, 0.8f, 0.8f));
                var requestedTarget = new Vector3(10f, 0f, 0f);

                player.ScheduleContact(
                    TechniqueAction.Receive,
                    0.5f,
                    new SimVector3(0f, 5f, -4f),
                    NoExecutionError(),
                    62,
                    movementTarget: requestedTarget,
                    movementStartSimulationTime: 0.35f);

                Assert.That(player.MovementShortfall, Is.GreaterThan(8f));
                Assert.That(player.ScheduledMovementTarget.x, Is.LessThan(2f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        private static Vector3 AveragePalms(PrototypePlayerAgent player)
        {
            return (player.Rig.GetJoint("LeftPalm").position +
                    player.Rig.GetJoint("RightPalm").position) * 0.5f;
        }

        private static SkillExecutionError NoExecutionError()
        {
            return new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                1f);
        }
    }
}

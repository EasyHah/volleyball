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
        public void ScheduleContact_RecordsAssignedMovementDistance()
        {
            var playerObject = new GameObject("MovingReceiver");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Defender), Color.blue, "3");
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    2f,
                    new SimVector3(0f, 6f, 4f),
                    new SkillExecutionError(0f, SimVector3.Zero, SimVector3.Zero, 0f, 1f, SimVector3.Zero, 1f),
                    39,
                    movementTarget: new Vector3(3f, 0f, 0f),
                    movementStartSimulationTime: 0f);

                Assert.That(player.ScheduledMovementDistance, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

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
                Assert.That(player.transform.position.z, Is.LessThanOrEqualTo(-PrototypePlayerAgent.NetClearance));
                Assert.That(player.IsWithinOwnCourt, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void PrepareForTraining_ClampsPlayersInsideTheirOwnCourt()
        {
            var blue = CreatePlayer("BlueBoundaryPlayer", TeamId.Blue, PlayerRole.Defender);
            var orange = CreatePlayer("OrangeBoundaryPlayer", TeamId.Orange, PlayerRole.Defender);
            try
            {
                blue.PrepareForTraining(new Vector3(20f, 0f, 3f));
                orange.PrepareForTraining(new Vector3(-20f, 0f, -3f));

                Assert.That(blue.transform.position.x, Is.EqualTo(
                    CourtBuilder.HalfWidth - PrototypePlayerAgent.BoundaryClearance).Within(0.001f));
                Assert.That(blue.transform.position.z, Is.EqualTo(
                    -PrototypePlayerAgent.NetClearance).Within(0.001f));
                Assert.That(orange.transform.position.x, Is.EqualTo(
                    -CourtBuilder.HalfWidth + PrototypePlayerAgent.BoundaryClearance).Within(0.001f));
                Assert.That(orange.transform.position.z, Is.EqualTo(
                    PrototypePlayerAgent.NetClearance).Within(0.001f));
                Assert.That(blue.IsWithinOwnCourt, Is.True);
                Assert.That(orange.IsWithinOwnCourt, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(blue.gameObject);
                Object.DestroyImmediate(orange.gameObject);
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
                var rootPosition = new Vector3(0f, 0f, -2.5f);
                player.PrepareForTraining(rootPosition);

                var setFrames = player.PreviewContactFrames(TechniqueAction.Set);
                var attackFrames = player.PreviewContactFrames(TechniqueAction.Attack);
                var attackBallCenter = attackFrames[0].Origin +
                                       (attackFrames[0].Normal * SimulatedBall.DefaultRadius);

                Assert.That(setFrames.Count, Is.EqualTo(2));
                Assert.That(
                    (setFrames[0].Origin - setFrames[1].Origin).Magnitude,
                    Is.LessThan(0.28f));
                Assert.That(attackFrames[0].Origin.Y, Is.GreaterThan(3f));
                Assert.That(attackBallCenter.Z, Is.GreaterThan(rootPosition.z + 0.95f));
                Assert.That(Mathf.Abs(attackBallCenter.X), Is.LessThan(0.4f));
                Assert.That(player.transform.position, Is.EqualTo(rootPosition));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void PreviewAttackContactFramesAt_MatchesTheScheduledApproachContactGeometry()
        {
            var player = CreatePlayer("PlannedAttackPreview", TeamId.Blue, PlayerRole.Defender);
            try
            {
                var approach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -1.2f),
                    new SimVector3(0f, 0f, 0f),
                    1.2f,
                    0.35f,
                    0.1f);
                player.transform.position = new Vector3(0f, 0f, -1.2f);
                var preview = player.PreviewAttackContactFramesAt(approach);
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    5f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    704,
                    movementStartSimulationTime: 3.5f,
                    attackApproach: approach);

                var contacts = Collect(player, 5f);

                Assert.That(contacts, Has.Count.EqualTo(1));
                Assert.That(
                    (preview[0].Origin - contacts[0].Surface.Current.Origin).Magnitude,
                    Is.LessThan(0.01f));
                Assert.That(preview[0].Origin.Z, Is.LessThan(0.4f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
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

        [Test]
        public void ScheduledMovement_GroundsRootWhenSchedulingFromAirbornePosition()
        {
            var playerObject = new GameObject("GroundedReceiver");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Defender), Color.blue, "3");
                player.transform.position = new Vector3(-1f, 1.25f, -4f);
                var contacts = new List<BallContactCandidate>();

                player.ScheduleContact(
                    TechniqueAction.Receive,
                    2f,
                    new SimVector3(0f, 5f, 4f),
                    NoExecutionError(),
                    63,
                    movementTarget: new Vector3(1f, 0f, -3f),
                    movementStartSimulationTime: 0f);
                player.CollectContacts(0.25f, 1f / 120f, contacts);

                Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void SupportAction_AdvancesFromSimulationTimeWithoutAddingContactCandidates()
        {
            var playerObject = new GameObject("SupportBlocker");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Orange, PlayerRole.Attacker), Color.red, "5");
                player.transform.position = new Vector3(-2f, 0f, 4f);
                var contacts = new List<BallContactCandidate>();

                player.ScheduleSupportAction(
                    TechniqueAction.Block,
                    10f,
                    new Vector3(1f, 0f, 0.65f),
                    9f);
                player.CollectContacts(9.50f, 1f / 120f, contacts);
                var movingPosition = player.transform.position;
                player.CollectContacts(10f, 1f / 120f, contacts);
                var contactPosition = player.transform.position;

                Assert.That(contacts, Is.Empty);
                Assert.That(movingPosition.x, Is.GreaterThan(-1.9f));
                Assert.That(movingPosition.z, Is.LessThan(3.9f));
                Assert.That(contactPosition.y, Is.GreaterThan(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void SupportAction_StaysGroundedUntilMovementStartToAvoidTeleporting()
        {
            var playerObject = new GameObject("SupportCover");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Setter), Color.blue, "1");
                player.transform.position = new Vector3(0f, 1.4f, -3f);
                var contacts = new List<BallContactCandidate>();

                player.ScheduleSupportAction(
                    TechniqueAction.Receive,
                    12f,
                    new Vector3(2f, 0f, -4.15f),
                    11f);
                player.CollectContacts(10.90f, 1f / 120f, contacts);
                var beforeStart = player.transform.position;

                Assert.That(contacts, Is.Empty);
                Assert.That(beforeStart.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(beforeStart.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(beforeStart.z, Is.EqualTo(-3f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ScheduledBlockContact_EmitsTwoActivePalmsOnlyInsideItsWindow()
        {
            var player = CreatePlayer("PhysicalBlocker", TeamId.Orange, PlayerRole.Attacker);
            try
            {
                player.ScheduleBlockContact(
                    10f,
                    new Vector3(1f, 0f, 0.28f),
                    9f,
                    new SimVector3(0f, 2f, -8f),
                    701);

                var before = Collect(player, 9.5f);
                var atContact = Collect(player, 10f);

                Assert.That(before, Is.Empty);
                Assert.That(atContact, Has.Count.EqualTo(2));
                Assert.That(atContact, Has.All.Matches<BallContactCandidate>(candidate =>
                    candidate.Action == TechniqueAction.Block &&
                    candidate.Actor.HasValue && candidate.Actor.Value.Equals(player.Id) &&
                    candidate.Surface.ContactGroupId == 701 &&
                    candidate.Surface.Active));
                Assert.That(player.transform.position.y, Is.GreaterThan(0.2f));
                Assert.That(player.PhysicalBlockContactAssignments, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void RetargetBlockContact_ClampsLargeLateCorrection()
        {
            var player = CreatePlayer("RetargetedBlocker", TeamId.Blue, PlayerRole.Setter);
            try
            {
                player.ScheduleBlockContact(
                    8f,
                    Vector3.zero,
                    7f,
                    new SimVector3(0f, 2f, 8f),
                    702);
                var previousTarget = player.ScheduledMovementTarget;

                Assert.That(player.RetargetBlockContact(
                    8.8f,
                    new Vector3(10f, 0f, -10f),
                    new SimVector3(0f, 2f, 8f)), Is.True);
                Assert.That(player.BlockRetargetDistance, Is.LessThanOrEqualTo(0.55f));
                Assert.That(player.BlockRetargetTimeShift, Is.LessThanOrEqualTo(0.12f));
                Assert.That(
                    Vector3.Distance(previousTarget, player.ScheduledMovementTarget),
                    Is.LessThanOrEqualTo(0.55f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledAttackApproach_MovesContinuouslyScalesJumpAndReturnsToGround()
        {
            var low = CreatePlayer("LowApproachAttacker", TeamId.Blue, PlayerRole.Defender);
            var high = CreatePlayer("HighApproachAttacker", TeamId.Blue, PlayerRole.Defender);
            try
            {
                var lowApproach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -2.4f),
                    new SimVector3(0f, 0f, -1.2f),
                    1.2f,
                    0.35f,
                    0.1f);
                var highApproach = new AttackApproachPlan(
                    lowApproach.ApproachStart,
                    lowApproach.Takeoff,
                    lowApproach.Distance,
                    0.9f,
                    lowApproach.AnglePenalty);
                ScheduleApproach(low, lowApproach);
                ScheduleApproach(high, highApproach);

                var previous = low.transform.position;
                foreach (var sampleTime in new[] { 4.1f, 4.3f, 4.5f, 4.62f, 4.8f, 5f })
                {
                    Collect(low, sampleTime);
                    Assert.That(
                        Vector3.Distance(previous, low.transform.position),
                        Is.LessThan(1.5f),
                        "Approach root motion must remain continuous.");
                    previous = low.transform.position;
                }

                Collect(high, 5f);
                var lowContactHeight = low.transform.position.y;
                var highContactHeight = high.transform.position.y;
                Collect(low, 5.7f);
                Collect(high, 5.7f);

                Assert.That(low.transform.position.z, Is.EqualTo(-1.2f).Within(0.05f));
                Assert.That(low.IsWithinOwnCourt, Is.True);
                Assert.That(high.IsWithinOwnCourt, Is.True);
                Assert.That(highContactHeight, Is.GreaterThan(lowContactHeight + 0.2f));
                Assert.That(low.transform.position.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(high.transform.position.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(low.gameObject);
                Object.DestroyImmediate(high.gameObject);
            }
        }

        [Test]
        public void EmergencyReceiveWindow_AddsReceiveCandidateWithoutScheduledContact()
        {
            var playerObject = new GameObject("EmergencyDigSetter");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Setter), Color.blue, "1");
                var contacts = new List<BallContactCandidate>();

                player.EnableEmergencyReceiveWindow(
                    4f,
                    4.5f,
                    new SimVector3(0f, 5.5f, 2.5f),
                    91);
                player.CollectContacts(4.1f, 1f / 120f, contacts);

                Assert.That(contacts, Has.Count.EqualTo(1));
                Assert.That(contacts[0].Action, Is.EqualTo(TechniqueAction.Receive));
                Assert.That(contacts[0].Actor, Is.EqualTo(player.Id));
                Assert.That(contacts[0].Surface.Active, Is.True);
                Assert.That(contacts[0].Surface.ContactGroupId, Is.EqualTo(91));
                Assert.That(contacts[0].TargetVelocity.Z, Is.EqualTo(2.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void EmergencyReceiveWindow_ExpiresAndDoesNotOverrideScheduledContact()
        {
            var playerObject = new GameObject("ScheduledSetterWithExpiredWindow");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Setter), Color.blue, "1");
                var contacts = new List<BallContactCandidate>();

                player.EnableEmergencyReceiveWindow(
                    1f,
                    1.25f,
                    new SimVector3(0f, 5.5f, 2.5f),
                    92);
                player.CollectContacts(1.5f, 1f / 120f, contacts);

                Assert.That(contacts, Is.Empty);

                player.ScheduleContact(
                    TechniqueAction.Set,
                    2f,
                    new SimVector3(0f, 6f, 4f),
                    NoExecutionError(),
                    93);
                player.CollectContacts(2f, 1f / 120f, contacts);

                Assert.That(contacts, Has.Count.EqualTo(2));
                Assert.That(contacts[0].Action, Is.EqualTo(TechniqueAction.Set));
                Assert.That(contacts[0].Actor, Is.EqualTo(player.Id));
                Assert.That(contacts[0].Surface.ContactGroupId, Is.EqualTo(93));
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

        private static PrototypePlayerAgent CreatePlayer(
            string name,
            TeamId team,
            PlayerRole role)
        {
            var playerObject = new GameObject(name);
            var player = playerObject.AddComponent<PrototypePlayerAgent>();
            player.Initialize(new PlayerId(team, role), team == TeamId.Blue ? Color.blue : Color.red, "1");
            return player;
        }

        private static List<BallContactCandidate> Collect(
            PrototypePlayerAgent player,
            float simulationTime)
        {
            var contacts = new List<BallContactCandidate>();
            player.CollectContacts(simulationTime, 1f / 120f, contacts);
            return contacts;
        }

        private static void ScheduleApproach(
            PrototypePlayerAgent player,
            AttackApproachPlan approach)
        {
            player.transform.position = new Vector3(0f, 0f, -2.4f);
            player.ScheduleContact(
                TechniqueAction.Attack,
                5f,
                new SimVector3(0f, -4f, 14f),
                NoExecutionError(),
                703,
                movementStartSimulationTime: 4f,
                attackApproach: approach);
        }
    }
}

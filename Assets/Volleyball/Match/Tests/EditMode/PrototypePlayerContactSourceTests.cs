using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class PrototypePlayerContactSourceTests
    {
        [Test]
        public void Facade_DeclaresOnlyIdentityConfigurationAndComponentFields()
        {
            var allowed = new HashSet<string>
            {
                "_moveSpeed",
                "_courtHalfLength",
                "<Id>k__BackingField",
                "<StableId>k__BackingField",
                "<Ability>k__BackingField",
                "_actionTimelineState",
                "_techniqueExecutor",
                "_presentation",
                "_contactSurfaceProvider",
                "_locomotion",
                "SupportActionActivated"
            };
            var fields = typeof(PrototypePlayerAgent).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (var field in fields)
            {
                Assert.That(
                    allowed.Contains(field.Name),
                    Is.True,
                    "Facade-owned business state: " + field.Name);
            }

            var allowedProperties = new HashSet<string>
            {
                "Id", "StableId", "Rig", "Ability", "ContactSurfaces", "CurrentSetStyle",
                "RequestedSetStyle", "PreparedForward", "ScheduledMovementTarget", "ReplayScheduledAction",
                "MovementShortfall", "ScheduledMovementDistance", "PhysicalBlockContactAssignments",
                "BlockRetargetDistance", "BlockRetargetTimeShift", "PhysicalBlockContactTime",
                "MaximumAppliedContactCorrection", "LastScheduledSurfaceCenter", "LastScheduledSurfaceNormal",
                "ScheduledExecutionEnvelopeV4", "ScheduledExecutionSampleV4",
                "ScheduledExecutionClassificationV4", "ScheduledTrajectoryPredictionArtifactV4",
                "MinimumActiveSurfacePlanError", "IsWithinOwnCourt", "EmergencyReceiveWindowEnabled"
            };
            var properties = typeof(PrototypePlayerAgent).GetProperties(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var property in properties)
            {
                Assert.That(
                    allowedProperties.Contains(property.Name),
                    Is.True,
                    "Facade private business-state proxy: " + property.Name);
            }
        }

        [Test]
        public void CancelScheduledContact_ClearsContactDiagnosticsAndFutureCandidates()
        {
            var playerObject = new GameObject("CancelledContactProvider");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Blue, PlayerRole.Defender), Color.blue, "3");
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    2f,
                    new SimVector3(0f, 4f, 3f),
                    new SkillExecutionError(0f, SimVector3.Zero, SimVector3.Zero, 0f, 1f, SimVector3.Zero, 1f),
                    811,
                    new SimVector3(0f, 2f, 0f));
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(2f, 1f / 120f, contacts);
                player.CancelScheduledContact();
                contacts.Clear();
                player.CollectContacts(2.01f, 1f / 120f, contacts);

                Assert.That(contacts, Is.Empty);
                Assert.That(player.LastScheduledSurfaceCenter.SqrMagnitude, Is.EqualTo(0f));
                Assert.That(player.LastScheduledSurfaceNormal.SqrMagnitude, Is.EqualTo(0f));
                Assert.That(ReadMinimumActiveSurfacePlanError(player), Is.EqualTo(float.PositiveInfinity));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

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
                player.SetAbility(MatchV4TestFixture.CreateAbility(1f, 1f, 1f, 1f, 1f, 1f, 1f));
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
                player.SetAbility(MatchV4TestFixture.CreateAbility(1f, 1f, 1f, 1f, 0.95f, 1f, 1f));
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
                Assert.That(palmBeforeContact, Is.GreaterThanOrEqualTo(palmAtContact));
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

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void PlannedAttack_UsesTheSameContactCenterForPreviewAndScheduledPalm(TeamId team)
        {
            var player = CreatePlayer("UnifiedAttackContact", team, PlayerRole.Attacker);
            try
            {
                var depthSign = new TeamCourtFrame(team).WorldDepthSign;
                player.transform.rotation = Quaternion.Euler(0f, team == TeamId.Orange ? 180f : 0f, 0f);
                player.SetAbility(MatchV4TestFixture.CreateAbility(
                    0.9f, 0.9f, 0.9f, 0.8f, 0.8f, 0.9f, 0.9f, 3.50f));
                var approach = new AttackApproachPlan(
                    new SimVector3(1f, 0f, depthSign * 3.65f),
                    new SimVector3(1f, 0f, depthSign * 2.45f),
                    1.2f,
                    1f,
                    0f);
                var plan = AttackContactPlanner.Plan(new AttackContactInput(
                    player.Ability.PlannedAttackContactHeightMeters,
                    1f,
                    1f,
                    SetQualityGrade.A,
                    approach.Takeoff,
                    0.8f,
                    1.1f));

                var preview = player.PreviewAttackContactFramesAt(plan);
                var previewCenter = preview[0].Origin +
                                    (preview[0].Normal * SimulatedBall.DefaultRadius);
                player.transform.position = new Vector3(1f, 0f, depthSign * 3.65f);
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    5f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    705,
                    plan.ContactCenter,
                    movementStartSimulationTime: 3.8f,
                    attackApproach: approach,
                    attackContactPlan: plan);

                var contacts = Collect(player, 5f);
                var scheduledCenter = contacts[0].Surface.Current.Origin +
                                      (contacts[0].Surface.Current.Normal * SimulatedBall.DefaultRadius);

                Assert.That((previewCenter - plan.ContactCenter).Magnitude, Is.LessThan(0.05f));
                Assert.That((scheduledCenter - plan.ContactCenter).Magnitude, Is.LessThan(0.05f));
                Assert.That((scheduledCenter - previewCenter).Magnitude, Is.LessThan(0.01f));
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
                player.SetAbility(MatchV4TestFixture.CreateAbility(0.9f, 1f, 0.8f, 1f, 0.8f, 0.8f, 0.8f));
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
        public void ResolveContactRootTarget_OffsetsReceiverRootForTheVisiblePlatform()
        {
            var playerObject = new GameObject("RootResolvedReceiver");
            try
            {
                playerObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PlayerId(TeamId.Orange, PlayerRole.Defender), Color.red, "6");
                var desiredCenter = new SimVector3(-3f, 1.36f, 1.80f);
                var resolvedRoot = player.ResolveContactRootTarget(
                    TechniqueAction.Receive,
                    desiredCenter,
                    new Vector3(desiredCenter.X, 0f, desiredCenter.Z));
                var frame = player.PreviewContactFramesAt(TechniqueAction.Receive, resolvedRoot)[0];
                var actualCenter = frame.Origin +
                                   (frame.Normal * SimulatedBall.DefaultRadius);

                Assert.That(actualCenter.X, Is.EqualTo(desiredCenter.X).Within(0.001f));
                Assert.That(actualCenter.Z, Is.EqualTo(desiredCenter.Z).Within(0.001f));
                Assert.That(resolvedRoot.z, Is.GreaterThan(desiredCenter.Z + 0.3f));
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
                player.SetAbility(MatchV4TestFixture.CreateAbility(0.2f, 0.5f, 0.8f, 1f, 0.8f, 0.8f, 0.8f));
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
        public void AttackPreparation_MovesTowardApproachStartWithoutAddingContactCandidates()
        {
            var gameObject = new GameObject("AttackPreparationPlayer");
            try
            {
                var player = gameObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(
                    new PlayerId(TeamId.Blue, PlayerRole.OutsideHitter),
                    Color.blue,
                    "4");
                player.transform.position = new Vector3(0f, 0f, -3f);
                var target = new Vector3(1.5f, 0f, -1.4f);
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    0.5f,
                    new SimVector3(0f, 5f, 4f),
                    NoExecutionError(),
                    70);
                player.ScheduleAttackPreparation(1f, target, 0f);
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(0.89f, 1f / 120f, contacts);

                Assert.That(contacts, Is.Empty);
                Assert.That(player.ReplayScheduledAction, Is.EqualTo(TechniqueAction.Attack.ToString()));
                Assert.That(Vector3.Distance(player.transform.position, target), Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ContinueAttackPreparation_PreservesPreparedProgressTowardTakeoff()
        {
            var player = CreatePlayer("ContinuingAttackPreparation", TeamId.Blue, PlayerRole.Attacker);
            try
            {
                player.SetAbility(MatchV4TestFixture.CreateAbility(
                    1f, 1f, 1f, 1f, 1f, 1f, 1f, 3.42f));
                player.transform.position = new Vector3(0f, 0f, -4.2f);
                var preparedStart = new Vector3(0f, 0f, -3.1f);
                var takeoff = new SimVector3(0f, 0f, -1.1f);
                var contact = AttackContactPlanner.Plan(new AttackContactInput(
                    3.42f, 1f, 1f, SetQualityGrade.A, takeoff, 0.4f, 1f));
                var originalApproach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -3.9f),
                    takeoff,
                    2.8f,
                    1f,
                    0f);
                var contacts = new List<BallContactCandidate>();

                player.ScheduleAttackPreparation(2f, preparedStart, 1f);
                player.CollectContacts(1.95f, 1f / 120f, contacts);
                var preparedPosition = player.transform.position;

                player.ContinueAttackPreparation(originalApproach, contact, 2.6f);
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2.6f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    708,
                    contact.ContactCenter,
                    movementStartSimulationTime: 1.95f,
                    attackApproach: originalApproach,
                    attackContactPlan: contact);
                var remainingDistance = player.ScheduledMovementDistance;
                player.CollectContacts(2.2f, 1f / 120f, contacts);

                Assert.That(preparedPosition.z, Is.EqualTo(preparedStart.z).Within(0.05f));
                Assert.That(remainingDistance, Is.LessThan(2.20f));
                Assert.That(remainingDistance, Is.GreaterThan(1.7f));
                Assert.That(player.transform.position.z, Is.GreaterThanOrEqualTo(preparedPosition.z - 0.001f));
                Assert.That(player.ScheduledMovementTarget.z, Is.GreaterThan(preparedPosition.z));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ContinueAttackPreparation_CapsRemainingTakeoffMovementByAvailableSpeed()
        {
            var player = CreatePlayer("SpeedBoundedContinuingAttacker", TeamId.Blue, PlayerRole.Attacker);
            try
            {
                player.SetAbility(MatchV4TestFixture.CreateAbility(
                    1f, 1f, 1f, 1f, 1f, 1f, 1f, 3.42f));
                player.transform.position = new Vector3(0f, 0f, -4.2f);
                var takeoff = new SimVector3(0f, 0f, -0.7f);
                var contact = AttackContactPlanner.Plan(new AttackContactInput(
                    3.42f, 1f, 1f, SetQualityGrade.A, takeoff, 0.4f, 1f));
                var approach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -3.9f),
                    takeoff,
                    3.5f,
                    1f,
                    0f);

                player.ScheduleAttackPreparation(2f, new Vector3(0f, 0f, -3.1f), 1f);
                Collect(player, 1.95f);
                var continuationStart = player.transform.position;

                player.ContinueAttackPreparation(approach, contact, 2.6f);
                var continuationDistance = player.ScheduledMovementDistance;
                var continuationShortfall = player.MovementShortfall;
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2.6f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    710,
                    contact.ContactCenter,
                    movementStartSimulationTime: 1.95f,
                    attackApproach: approach,
                    attackContactPlan: contact);
                var maximumSpeed = 7f * (0.65f + (player.Ability.Mobility * 0.5f));
                var availableSeconds = 2.6f - 0.38f - 2f;

                Assert.That(
                    continuationDistance,
                    Is.LessThanOrEqualTo((maximumSpeed * availableSeconds) + 0.001f));
                Assert.That(continuationShortfall, Is.GreaterThan(0f));

                var previous = continuationStart;
                foreach (var sampleTime in new[] { 2.05f, 2.10f, 2.15f, 2.20f })
                {
                    Collect(player, sampleTime);
                    Assert.That(
                        Vector3.Distance(previous, player.transform.position),
                        Is.LessThanOrEqualTo((maximumSpeed * 0.05f * 1.5f) + 0.001f),
                        "Continuation samples must follow the bounded approach trajectory.");
                    previous = player.transform.position;
                }
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledAttack_ContactAlignmentNeverExceedsPointEighteenMeters()
        {
            var player = CreatePlayer("BoundedAttackAlignment", TeamId.Blue, PlayerRole.Attacker);
            try
            {
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    709,
                    new SimVector3(4f, 4f, -1f));

                Collect(player, 2f);

                Assert.That(
                    player.MaximumAppliedContactCorrection,
                    Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance));
                Assert.That(player.MovementShortfall, Is.GreaterThan(0f),
                    "The facade path must expose the unresolved component-owned correction.");
                Assert.That(player.IsWithinOwnCourt, Is.True,
                    "Contact alignment must use the locomotion court clamp.");

                player.ScheduleContact(
                    TechniqueAction.Attack,
                    3f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    710,
                    new SimVector3(-4f, 4f, -1f));
                Collect(player, 3f);

                Assert.That(player.MaximumAppliedContactCorrection,
                    Is.GreaterThan(0f),
                    "A newly configured facade attack must receive a fresh correction budget.");
                Assert.That(player.MaximumAppliedContactCorrection,
                    Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void SetPreparation_ReplacesAnOldContactAndMovesWithoutAddingContactCandidates()
        {
            var gameObject = new GameObject("SetPreparationPlayer");
            try
            {
                var player = gameObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(
                    new PlayerId(TeamId.Blue, PlayerRole.Setter),
                    Color.blue,
                    "2");
                player.transform.position = new Vector3(-3f, 0f, -7f);
                var target = new Vector3(1.2f, 0f, -1.1f);
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    0.5f,
                    new SimVector3(0f, 5f, 4f),
                    NoExecutionError(),
                    71);
                player.ScheduleSetPreparation(1.5f, target, 0f);
                var contacts = new List<BallContactCandidate>();

                player.CollectContacts(1.39f, 1f / 120f, contacts);

                Assert.That(contacts, Is.Empty);
                Assert.That(player.ReplayScheduledAction, Is.EqualTo(TechniqueAction.Set.ToString()));
                Assert.That(Vector3.Distance(player.transform.position, target), Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ScheduledBlockContact_EmitsSixArmVolumesOnlyInsideItsWindow()
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
                Assert.That(atContact.Count, Is.EqualTo(6));
                Assert.That(atContact, Has.All.Matches<BallContactCandidate>(candidate =>
                    candidate.Action == TechniqueAction.Block &&
                    candidate.Actor.HasValue && candidate.Actor.Value.Equals(player.Id) &&
                    candidate.IsCapsule &&
                    candidate.Capsule.ContactGroupId == 701 &&
                    candidate.Capsule.Active));
                Assert.That(player.transform.position.y, Is.GreaterThan(0.2f));
                Assert.That(player.PhysicalBlockContactAssignments, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledAndRetargetedBlock_UseRequestedContactHeights()
        {
            var player = CreatePlayer("RequestedBlockHeight", TeamId.Blue, PlayerRole.MiddleBlocker);
            try
            {
                player.ScheduleBlockContact(
                    2f,
                    new Vector3(0f, 0.16f, -1f),
                    0f,
                    new SimVector3(0f, -2f, 4f),
                    811);
                Collect(player, 2f);
                Assert.That(player.transform.position.y, Is.EqualTo(0.16f).Within(0.01f));

                Assert.That(player.RetargetBlockContact(
                    2.05f,
                    new Vector3(0f, 0.34f, -1f),
                    new SimVector3(0f, -2f, 4f)), Is.True);
                Collect(player, 2.05f);
                Assert.That(player.transform.position.y, Is.EqualTo(0.34f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void OrdinarySupportBlock_DoesNotReusePhysicalBlockRequestedHeight()
        {
            var player = CreatePlayer("SupportBlockHeightReset", TeamId.Blue, PlayerRole.MiddleBlocker);
            try
            {
                player.ScheduleBlockContact(
                    2f,
                    new Vector3(0f, 0.16f, -1f),
                    0f,
                    new SimVector3(0f, -2f, 4f),
                    814);
                Collect(player, 2f);
                Assert.That(player.transform.position.y, Is.EqualTo(0.16f).Within(0.01f));

                player.DisableBlockContactWindow();
                player.ScheduleSupportAction(
                    TechniqueAction.Block,
                    4f,
                    new Vector3(0f, 0f, -1f),
                    2f);
                Collect(player, 4f);

                Assert.That(player.transform.position.y, Is.GreaterThan(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void PhysicalBlockScheduling_ReplacesStaleScheduledMovementDistance()
        {
            var player = CreatePlayer("BlockMovementDistance", TeamId.Blue, PlayerRole.MiddleBlocker);
            try
            {
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    4f,
                    new SimVector3(0f, 3f, 4f),
                    NoExecutionError(),
                    812,
                    movementTarget: new Vector3(4f, 0f, -0.3f),
                    movementStartSimulationTime: 0f);
                var staleDistance = player.ScheduledMovementDistance;

                player.ScheduleBlockContact(
                    2f,
                    player.transform.position,
                    0f,
                    new SimVector3(0f, -2f, 4f),
                    813);

                Assert.That(staleDistance, Is.GreaterThan(0.01f));
                Assert.That(player.ScheduledMovementDistance, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void PreviewBlockArmFrames_ReturnsBlockPoseWithoutMutatingPlayerState()
        {
            var player = CreatePlayer("PreviewBlocker", TeamId.Blue, PlayerRole.MiddleBlocker);
            try
            {
                player.transform.position = new Vector3(-1.5f, 0f, -3f);
                player.Rig.SetPose(StickFigurePose.Receive, 1f);
                var savedPosition = player.transform.position;
                var savedRotations = player.Rig.CaptureLocalRotations();

                var frames = player.PreviewBlockArmFrames(
                    4.25f,
                    new Vector3(0.75f, 0f, -PrototypePlayerAgent.NetClearance));

                Assert.That(frames.Count, Is.EqualTo(6));
                Assert.That(player.transform.position, Is.EqualTo(savedPosition));
                foreach (var pair in savedRotations)
                {
                    Assert.That(player.Rig.GetJoint(pair.Key).localRotation, Is.EqualTo(pair.Value));
                }
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void PreviewBlockArmFrames_MatchesScheduledArmHeightAtContact()
        {
            var player = CreatePlayer("PreviewJumpingBlocker", TeamId.Blue, PlayerRole.MiddleBlocker);
            try
            {
                const float contactTime = 10f;
                var rootTarget = player.ResolveBlockRootTarget(
                    new SimVector3(0.75f, 2.7f, 0f),
                    new Vector3(0.75f, 0f, -PrototypePlayerAgent.NetClearance));
                player.Rig.SetPose(StickFigurePose.Block, 1f);
                var preview = player.PreviewBlockArmFrames(contactTime, rootTarget);
                player.ScheduleBlockContact(
                    contactTime,
                    rootTarget,
                    9f,
                    new SimVector3(0f, 2f, 8f),
                    705);

                var actual = Collect(player, contactTime);
                var previewCenterHeight = 0f;
                var actualCenterHeight = 0f;
                for (var index = 0; index < preview.Count; index++)
                {
                    previewCenterHeight += (preview[index].Start.Y + preview[index].End.Y) * 0.5f;
                    actualCenterHeight +=
                        (actual[index].Capsule.Current.Start.Y +
                         actual[index].Capsule.Current.End.Y) * 0.5f;
                }

                previewCenterHeight /= preview.Count;
                actualCenterHeight /= actual.Count;
                Assert.That(previewCenterHeight, Is.EqualTo(actualCenterHeight).Within(0.03f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ResolveBlockRootTarget_UsesLessJumpForALowerInterception()
        {
            var player = CreatePlayer("HeightAwareBlocker", TeamId.Blue, PlayerRole.MiddleBlocker);
            try
            {
                var nominal = new Vector3(0.75f, 0f, -PrototypePlayerAgent.NetClearance);

                var low = player.ResolveBlockRootTarget(
                    new SimVector3(0.75f, 2.25f, 0f),
                    nominal);
                var high = player.ResolveBlockRootTarget(
                    new SimVector3(0.75f, 2.85f, 0f),
                    nominal);

                Assert.That(low.y, Is.GreaterThanOrEqualTo(0f));
                Assert.That(low.y, Is.LessThan(high.y));
                Assert.That(high.y, Is.LessThanOrEqualTo(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledBlockContact_ReorientsPreviousSetFacingTowardTheNet()
        {
            var player = CreatePlayer("ReorientedBlocker", TeamId.Orange, PlayerRole.Setter);
            try
            {
                player.transform.forward = new Vector3(0.8f, 0f, 0.6f);
                player.ScheduleBlockContact(
                    10f,
                    new Vector3(0f, 0f, PrototypePlayerAgent.NetClearance),
                    9f,
                    new SimVector3(0f, 2f, -8f),
                    704);

                var contacts = Collect(player, 10f);

                Assert.That(contacts, Has.Count.EqualTo(6));
                Assert.That(player.transform.forward.z, Is.EqualTo(-1f).Within(0.001f));
                Assert.That(
                    player.Rig.GetJoint("LeftPalm").position.z,
                    Is.LessThan(player.transform.position.z));
                Assert.That(
                    player.Rig.GetJoint("RightPalm").position.z,
                    Is.LessThan(player.transform.position.z));
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
        public void ScheduledAttackApproach_UsesActualEarlyContactTimeForMovementWindow()
        {
            var player = CreatePlayer("EarlyContactAttacker", TeamId.Blue, PlayerRole.Defender);
            try
            {
                player.SetAbility(MatchV4TestFixture.CreateAbility(0.8f, 0.5f, 0.8f, 1f, 0.8f, 0.8f, 0.8f));
                var approach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -3.2f),
                    new SimVector3(0f, 0f, -1.2f),
                    2f,
                    1f,
                    0f);
                var plan = AttackContactPlanner.Plan(new AttackContactInput(
                    player.Ability.PlannedAttackContactHeightMeters,
                    approach.JumpQuality,
                    1f,
                    SetQualityGrade.A,
                    approach.Takeoff,
                    0.6f,
                    1f));
                var earlyExecution = new SkillExecutionError(
                    0f,
                    SimVector3.Zero,
                    SimVector3.Zero,
                    -0.08f,
                    1f,
                    SimVector3.Zero,
                    1f);
                player.transform.position = new Vector3(0f, 0f, -3.2f);

                player.ScheduleContact(
                    TechniqueAction.Attack,
                    5f,
                    new SimVector3(0f, -4f, 14f),
                    earlyExecution,
                    706,
                    plan.ContactCenter,
                    movementStartSimulationTime: 4f,
                    attackApproach: approach,
                    attackContactPlan: plan);

                Assert.That(player.MovementShortfall, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ObservedAttackTakeoff_PlannedBehindLineButActualFrontZoneReportsActual()
        {
            var player = CreatePlayer("ObservedFrontZoneAttacker", TeamId.Blue, PlayerRole.Defender);
            try
            {
                player.transform.position = new Vector3(0f, 0f, -1.2f);
                var plannedTakeoff = new SimVector3(0f, 0f, -3.2f);
                ScheduleObservedGeometryAttack(player, plannedTakeoff);

                Collect(player, 5f);

                Assert.That(player.TryGetObservedAttackTakeoff(out var observed), Is.True);
                var observedGeometry = new AttackGeometryFactV3(
                    player.StableId,
                    Volleyball.Shared.Contracts.TeamSide.Home,
                    observed.Point,
                    new SimVector3(0f, 2.5f, -0.2f),
                    3f,
                    CourtBuilder.NetHeight);
                Assert.That(plannedTakeoff.Z, Is.LessThan(-3f));
                Assert.That(observed.Point.Z, Is.GreaterThan(-3f));
                Assert.That(observed.SimulationTime, Is.LessThan(5f));
                Assert.That(observedGeometry.IsTakeoffInFrontZone, Is.True);
                Assert.That(observedGeometry.IsContactAboveNet, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ObservedAttackTakeoff_PlannedFrontZoneButActualBehindLineReportsActual()
        {
            var player = CreatePlayer("ObservedBackZoneAttacker", TeamId.Blue, PlayerRole.Defender);
            try
            {
                player.transform.position = new Vector3(0f, 0f, -3.4f);
                var plannedTakeoff = new SimVector3(0f, 0f, -2.5f);
                ScheduleObservedGeometryAttack(player, plannedTakeoff);

                Collect(player, 5f);

                Assert.That(player.TryGetObservedAttackTakeoff(out var observed), Is.True);
                var observedGeometry = new AttackGeometryFactV3(
                    player.StableId,
                    Volleyball.Shared.Contracts.TeamSide.Home,
                    observed.Point,
                    new SimVector3(0f, 2.5f, -0.2f),
                    3f,
                    CourtBuilder.NetHeight);
                Assert.That(plannedTakeoff.Z, Is.GreaterThan(-3f));
                Assert.That(observed.Point.Z, Is.LessThan(-3f));
                Assert.That(observed.SimulationTime, Is.LessThan(5f));
                Assert.That(observedGeometry.IsTakeoffInFrontZone, Is.False);
                Assert.That(observedGeometry.IsContactAboveNet, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledAttackApproach_ReplanAfterPreparationPreservesRemainingApproachProgress()
        {
            var player = CreatePlayer("PreparedReplanAttacker", TeamId.Blue, PlayerRole.OutsideHitter);
            try
            {
                player.SetAbility(MatchV4TestFixture.CreateAbility(1f, 1f, 1f, 1f, 1f, 1f, 1f));
                var preparedStart = new Vector3(0f, 0f, -3.1f);
                var takeoff = new SimVector3(0f, 0f, -1.1f);
                var contact = AttackContactPlanner.Plan(new AttackContactInput(
                    3.42f,
                    1f,
                    1f,
                    SetQualityGrade.A,
                    takeoff,
                    0.4f,
                    1f));
                var approach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -4.2f),
                    takeoff,
                    3.1f,
                    1f,
                    0f);
                player.transform.position = new Vector3(0f, 0f, -4.2f);
                player.ScheduleAttackPreparation(1f, preparedStart, 0f);
                Collect(player, 0.9f);
                var preparedPosition = player.transform.position;

                player.ScheduleContact(
                    TechniqueAction.Attack,
                    1.4f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    708,
                    contact.ContactCenter,
                    movementStartSimulationTime: 1f,
                    attackApproach: approach,
                    attackContactPlan: contact);
                var afterSchedulePosition = player.transform.position;

                Assert.That(afterSchedulePosition.z, Is.GreaterThanOrEqualTo(preparedPosition.z - 0.001f));
                var maximumSpeed = 7f * (0.65f + (player.Ability.Mobility * 0.5f));
                var availableSeconds = 1.4f - 0.38f - 0.9f;
                Assert.That(
                    player.ScheduledMovementDistance,
                    Is.LessThanOrEqualTo((maximumSpeed * availableSeconds) + 0.001f));
                Assert.That(player.MovementShortfall, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledAttackApproach_CapsFarContactRootTravelDuringJump()
        {
            var player = CreatePlayer("BoundedAttackContactRoot", TeamId.Blue, PlayerRole.Attacker);
            try
            {
                player.SetAbility(MatchV4TestFixture.CreateAbility(
                    1f, 1f, 1f, 1f, 1f, 1f, 1f, 3.42f));
                var takeoff = new SimVector3(0f, 0f, -1.2f);
                var approach = new AttackApproachPlan(takeoff, takeoff, 0f, 1f, 0f);
                var farContact = new AttackContactPlan(
                    takeoff,
                    new SimVector3(4.2f, 3.5f, -1.2f),
                    1f,
                    1f,
                    0.4f,
                    1f,
                    AttackContactOutcome.FullAttack);
                player.transform.position = new Vector3(0f, 0f, -1.2f);
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2f,
                    new SimVector3(0f, -4f, 14f),
                    NoExecutionError(),
                    711,
                    farContact.ContactCenter,
                    movementStartSimulationTime: 1f,
                    attackApproach: approach,
                    attackContactPlan: farContact);

                Collect(player, 1.62f);
                var takeoffPosition = player.transform.position;
                Collect(player, 1.81f);
                var midJumpPosition = player.transform.position;
                Collect(player, 2f);
                var contactPosition = player.transform.position;
                Collect(player, 2.45f);
                var landingPosition = player.transform.position;
                var maximumSpeed = 7f * (0.65f + (player.Ability.Mobility * 0.5f));
                const float jumpSeconds = 0.38f;

                Assert.That(
                    HorizontalDistance(takeoffPosition, contactPosition),
                    Is.LessThanOrEqualTo((maximumSpeed * jumpSeconds) + 0.181f));
                Assert.That(
                    HorizontalDistance(takeoffPosition, midJumpPosition),
                    Is.LessThanOrEqualTo((maximumSpeed * 0.19f * 1.5f) + 0.001f));
                Assert.That(
                    HorizontalDistance(contactPosition, landingPosition),
                    Is.LessThanOrEqualTo(0.181f),
                    "Only the bounded contact-alignment allowance may separate contact and landing.");
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void ScheduledQuickAttack_AlignsPalmAtBallArrivalBeforeSlightlyLateContact()
        {
            var player = CreatePlayer("QuickAttackDefender", TeamId.Orange, PlayerRole.Defender);
            try
            {
                player.transform.forward = Vector3.back;
                var takeoff = new SimVector3(-0.34f, 0f, 2.055f);
                var approach = new AttackApproachPlan(
                    new SimVector3(-0.34f, 0f, 2.27f),
                    takeoff,
                    0.215f,
                    1f,
                    0f);
                var plan = AttackContactPlanner.Plan(new AttackContactInput(
                    player.Ability.PlannedAttackContactHeightMeters,
                    1f,
                    1f,
                    SetQualityGrade.A,
                    takeoff,
                    0.4f,
                    0.42f));
                var slightlyLate = new SkillExecutionError(
                    0f,
                    new SimVector3(0.04f, 0.04f, 0.04f),
                    new SimVector3(2f, 2f, 2f),
                    0.025f,
                    1f,
                    SimVector3.Zero,
                    1f);
                player.transform.position = new Vector3(-0.34f, 0f, 2.27f);
                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2.033f,
                    new SimVector3(0f, -4f, -14f),
                    slightlyLate,
                    707,
                    plan.ContactCenter,
                    movementStartSimulationTime: 1.614f,
                    attackApproach: approach,
                    attackContactPlan: plan);

                for (var simulationTime = 1.614f;
                     simulationTime < 2.025f;
                     simulationTime += 1f / 120f)
                {
                    Collect(player, simulationTime);
                }
                Collect(player, 2.025f);
                var rootBeforeContactStep = player.transform.position;
                var contacts = Collect(player, 2.033f);
                var palmCenter = contacts[0].Surface.Current.Origin +
                                 (contacts[0].Surface.Current.Normal * SimulatedBall.DefaultRadius);

                var maximumStepDistance =
                    7f * (0.65f + (player.Ability.Mobility * 0.5f)) * (1f / 120f);
                Assert.That(
                    Vector3.Distance(rootBeforeContactStep, player.transform.position),
                    Is.LessThanOrEqualTo(maximumStepDistance + 0.0001f),
                    "The planned root sample and persistent alignment must share one locomotion step budget.");

                Assert.That(
                    (palmCenter - plan.ContactCenter).Magnitude,
                    Is.LessThan(0.01f));
                Assert.That(
                    player.MaximumAppliedContactCorrection,
                    Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance));
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
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

        private static float ReadMinimumActiveSurfacePlanError(PrototypePlayerAgent player)
        {
            var property = typeof(PrototypePlayerAgent).GetProperty(
                "MinimumActiveSurfacePlanError",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            return (float)property.GetValue(player);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
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

        private static void ScheduleObservedGeometryAttack(
            PrototypePlayerAgent player,
            SimVector3 plannedTakeoff)
        {
            var approach = new AttackApproachPlan(
                new SimVector3(
                    player.transform.position.x,
                    0f,
                    player.transform.position.z),
                plannedTakeoff,
                (plannedTakeoff - new SimVector3(
                    player.transform.position.x,
                    0f,
                    player.transform.position.z)).Magnitude,
                1f,
                0f);
            var contactPlan = AttackContactPlanner.Plan(new AttackContactInput(
                player.Ability.PlannedAttackContactHeightMeters,
                1f,
                1f,
                SetQualityGrade.A,
                plannedTakeoff,
                0.8f,
                1.1f));
            player.ScheduleContact(
                TechniqueAction.Attack,
                5f,
                new SimVector3(0f, -4f, 14f),
                NoExecutionError(),
                708,
                contactPlan.ContactCenter,
                movementStartSimulationTime: 4.7f,
                attackApproach: approach,
                attackContactPlan: contactPlan);
        }
    }
}

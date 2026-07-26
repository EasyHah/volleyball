using NUnit.Framework;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class PlayerLocomotionTests
    {
        [Test]
        public void AttackSamples_AreSpeedBoundedAndCorrectionNeverExceedsBaseline()
        {
            var locomotion = CreateAttackLocomotion();
            var first = locomotion.Sample(1.90f);
            var second = locomotion.Sample(1.91f);

            Assert.That(Vector3.Distance(first.Position, second.Position),
                Is.LessThanOrEqualTo(locomotion.MaximumSpeed * 0.01f + 0.0001f));
            Assert.That(locomotion.MaximumAppliedContactCorrection,
                Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance));

            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void AttackCorrection_CapsCumulativeRootMovementAndRecordsUnresolvedShortfall()
        {
            var locomotion = CreateAttackLocomotion();
            var initial = locomotion.Root.position;

            locomotion.ApplyLimitedContactAlignment(
                new Volleyball.Domain.Simulation.SimVector3(1f, 0f, -1f),
                Volleyball.Domain.Simulation.SimVector3.Zero);
            locomotion.ApplyLimitedContactAlignment(
                new Volleyball.Domain.Simulation.SimVector3(1f, 0f, -1f),
                Volleyball.Domain.Simulation.SimVector3.Zero);

            Assert.That(Vector3.Distance(initial, locomotion.Root.position),
                Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance + 0.0001f));
            Assert.That(locomotion.MovementShortfall, Is.GreaterThan(0f));
            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void ConfigureAttackApproach_ResetsCorrectionBudgetForTheNextAttack()
        {
            var locomotion = CreateAttackLocomotion();
            locomotion.ApplyLimitedContactAlignment(
                new Volleyball.Domain.Simulation.SimVector3(2f, 0f, -1f),
                Volleyball.Domain.Simulation.SimVector3.Zero);
            locomotion.ConfigureAttackApproach(
                new AttackApproachPlan(
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -2f),
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.8f),
                    1.2f,
                    1f,
                    0f),
                PlayerAbilityProfile.Default,
                4f);

            var beforeSecondAttack = locomotion.Root.position;
            locomotion.ApplyLimitedContactAlignment(
                new Volleyball.Domain.Simulation.SimVector3(2f, 0f, -1f),
                Volleyball.Domain.Simulation.SimVector3.Zero);

            Assert.That(Vector3.Distance(beforeSecondAttack, locomotion.Root.position),
                Is.EqualTo(PrototypePlayerAgent.NetClearance).Within(0.0001f));
            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void AttackAlignment_PersistsIntoTheNextPlannedAttackSample()
        {
            var locomotion = CreateAttackLocomotion();
            SampleAndSetRoot(locomotion, 1.98f);
            SampleAndSetRoot(locomotion, 1.99f);

            var applied = locomotion.ApplyAttackContactAlignment(new Vector3(0.05f, 0f, 0f));
            var nextSample = locomotion.Sample(2f);

            Assert.That(applied.x, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(nextSample.Position.x, Is.EqualTo(applied.x).Within(0.0001f),
                "A locomotion-owned alignment must survive the next planned root sample.");
            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void PersistentAttackAlignment_NextFixedSampleKeepsTheActualRootWithinOneSpeedBudget()
        {
            var root = new GameObject("PersistentAlignmentAttackRoot");
            root.transform.position = new Vector3(0f, 0f, -1f);
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 1f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(4f, 0f, -1f),
                0f,
                0.5f,
                TechniqueAction.Attack,
                PlayerAbilityProfile.Default,
                0f);

            SampleAndSetRoot(locomotion, 0.24f, 0.01f, false);
            SampleAndSetRoot(locomotion, 0.25f, 0.01f, false);
            locomotion.ApplyAttackContactAlignment(new Vector3(0.05f, 0f, 0f));
            var rootBeforeNextSample = locomotion.Root.position;

            var nextSample = locomotion.Sample(0.26f, 0.01f, true);

            Assert.That(Vector3.Distance(rootBeforeNextSample, nextSample.Position),
                Is.LessThanOrEqualTo((locomotion.MaximumSpeed * 0.01f) + 0.0001f),
                "A persistent alignment must be part of the single speed-limited live-root target.");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void FirstFixedLiveSample_UsesElapsedSimulationTimeWithoutTeleporting()
        {
            var root = new GameObject("FirstFixedLiveAttackRoot");
            root.transform.position = new Vector3(0f, 0f, -4f);
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 7f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -0.18f),
                0f,
                1f,
                TechniqueAction.Attack,
                PlayerAbilityProfile.Default,
                0.5f);
            locomotion.ConfigureAttackApproach(
                new AttackApproachPlan(
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.18f),
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.18f),
                    0f,
                    1f,
                    0f),
                PlayerAbilityProfile.Default,
                1f);
            locomotion.ConfigureAttackContact(new Vector3(0f, 20f, -0.18f), 0.38f, PlayerAbilityProfile.Default);

            var sample = locomotion.Sample(1f, 0.01f, true);

            Assert.That(Vector3.Distance(root.transform.position, sample.Position),
                Is.LessThanOrEqualTo(locomotion.MaximumSpeed + 0.0001f),
                "The first live sample must consume its elapsed simulation-time speed budget.");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AttackAlignment_StaysWithinPointEighteenMetersAndThePerStepSpeedBound()
        {
            var locomotion = CreateAttackLocomotion();
            SampleAndSetRoot(locomotion, 1.98f);
            SampleAndSetRoot(locomotion, 1.99f);
            var beforeAlignment = locomotion.Root.position;

            locomotion.ApplyAttackContactAlignment(new Vector3(0.18f, 0f, 0f));

            Assert.That(Vector3.Distance(beforeAlignment, locomotion.Root.position),
                Is.LessThanOrEqualTo((locomotion.MaximumSpeed * 0.01f) + 0.0001f),
                "Each alignment step must be limited by locomotion speed and elapsed simulation time.");
            Assert.That(locomotion.CurrentAttackAlignmentOffset.magnitude,
                Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance + 0.0001f));
            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void AttackStep_PlannedMotionAndPersistentAlignmentShareOneSpeedBudget()
        {
            var root = new GameObject("SharedAttackStepBudget");
            root.transform.position = new Vector3(0f, 0f, -1f);
            var locomotion = new PlayerLocomotion(
                root.transform,
                TeamId.Blue,
                CourtBuilder.HalfLength,
                7f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(1f, 0f, -1f),
                0f,
                0.6f,
                TechniqueAction.Attack,
                PlayerAbilityProfile.Default);

            SampleAndSetRoot(locomotion, 0.14f, 0.01f, true);
            var rootBeforeStep = root.transform.position;
            SampleAndSetRoot(locomotion, 0.15f, 0.01f, true);
            locomotion.ApplyAttackContactAlignment(new Vector3(0.18f, 0f, 0f));

            Assert.That(Vector3.Distance(rootBeforeStep, root.transform.position),
                Is.LessThanOrEqualTo((locomotion.MaximumSpeed * 0.01f) + 0.0001f),
                "Planned root motion and alignment must use one MaximumSpeed * dt budget.");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SaturatedSmoothStepMidpoint_LiveAttackRootNeverExceedsOneStepBudget()
        {
            var root = new GameObject("SaturatedMidpointAttackRoot");
            root.transform.position = new Vector3(0f, 0f, -4f);
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 7f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -0.18f),
                0f,
                1f,
                TechniqueAction.Attack,
                PlayerAbilityProfile.Default,
                0f);
            locomotion.ConfigureAttackApproach(
                new AttackApproachPlan(
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.18f),
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.18f),
                    0f,
                    1f,
                    0f),
                PlayerAbilityProfile.Default,
                1f);
            locomotion.ConfigureAttackContact(new Vector3(0f, 3f, -0.18f), 0.38f, PlayerAbilityProfile.Default);

            var previous = root.transform.position;
            for (var time = 0.01f; time <= 1.35f; time += 0.01f)
            {
                var sample = locomotion.Sample(time, 0.01f, true);
                locomotion.SetRootPosition(sample.Position);
                Assert.That(Vector3.Distance(previous, root.transform.position),
                    Is.LessThanOrEqualTo((locomotion.MaximumSpeed * 0.01f) + 0.0001f),
                    "The live root must share one speed budget across saturated ground, ascent, and landing motion.");
                previous = root.transform.position;
            }

            Object.DestroyImmediate(root);
        }

        [Test]
        public void SaturatedAttackRoute_MaximumSpeedCoversTheConfiguredAscentAndKeepsContactReachable()
        {
            var root = new GameObject("SaturatedAttackRouteSpeedBound");
            root.transform.position = new Vector3(0f, 0f, -4f);
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 7f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -0.18f), 0f, 1f, TechniqueAction.Attack,
                PlayerAbilityProfile.Default, 0f);
            locomotion.ConfigureAttackApproach(
                new AttackApproachPlan(
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.18f),
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.18f),
                    0f, 1f, 0f),
                PlayerAbilityProfile.Default, 1f);
            locomotion.ConfigureAttackContact(
                new Vector3(0f, 3f, -0.18f), .38f, PlayerAbilityProfile.Default);

            var previous = root.transform.position;
            for (var time = .01f; time <= 1f; time += .01f)
            {
                var sample = locomotion.Sample(time, .01f, true);
                locomotion.SetRootPosition(sample.Position);
                Assert.That(Vector3.Distance(previous, root.transform.position),
                    Is.LessThanOrEqualTo((locomotion.MaximumSpeed * .01f) + .0001f));
                previous = root.transform.position;
            }

            Assert.That(locomotion.MaximumSpeed, Is.GreaterThanOrEqualTo(10f));
            Assert.That(Vector3.Distance(new Vector3(0f, 0f, -.18f), root.transform.position),
                Is.LessThanOrEqualTo(.001f),
                "The live root must not trail a desired route already declared feasible.");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ConfigureAttackApproach_ResetsThePersistentAlignmentOffsetForANewAttack()
        {
            var locomotion = CreateAttackLocomotion();
            SampleAndSetRoot(locomotion, 1.98f);
            SampleAndSetRoot(locomotion, 1.99f);
            locomotion.ApplyAttackContactAlignment(new Vector3(0.05f, 0f, 0f));

            locomotion.ConfigureAttackApproach(
                new AttackApproachPlan(
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -2f),
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.8f),
                    1.2f,
                    1f,
                    0f),
                PlayerAbilityProfile.Default,
                4f);

            Assert.That(locomotion.CurrentAttackAlignmentOffset, Is.EqualTo(Vector3.zero));
            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void ContactAlignment_ClampsRootToOwnCourt()
        {
            var root = new GameObject("CourtClampedAttacker");
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 7f);
            locomotion.SetRootPosition(new Vector3(CourtBuilder.HalfWidth - PrototypePlayerAgent.BoundaryClearance, 0f,
                -PrototypePlayerAgent.NetClearance));

            locomotion.ApplyLimitedContactAlignment(
                new Volleyball.Domain.Simulation.SimVector3(1f, 0f, 1f),
                Volleyball.Domain.Simulation.SimVector3.Zero);

            Assert.That(locomotion.Root.position.x,
                Is.LessThanOrEqualTo(CourtBuilder.HalfWidth - PrototypePlayerAgent.BoundaryClearance));
            Assert.That(locomotion.Root.position.z,
                Is.LessThanOrEqualTo(-PrototypePlayerAgent.NetClearance));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PlannedAttackThenUnplannedAttack_SamplesTheNewUnplannedTrajectory()
        {
            var locomotion = CreateAttackLocomotion();
            locomotion.ConfigureAttackContact(new Vector3(0f, 2.5f, -1f), 0.38f, PlayerAbilityProfile.Default);
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -1f),
                3f,
                4f,
                TechniqueAction.Attack,
                PlayerAbilityProfile.Default);

            var sample = locomotion.Sample(4f);

            Assert.That(locomotion.HasAttackApproach, Is.False);
            Assert.That(sample.Position.y, Is.LessThan(2f));
            Object.DestroyImmediate(locomotion.Root.gameObject);
        }

        [Test]
        public void ScheduledReceive_MaximumSpeedCoversSmoothStepPeakWithoutChangingReach()
        {
            var root = new GameObject("ReceiveRoutePeak");
            root.transform.position = new Vector3(0f, 0f, -4f);
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 7f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -.18f), 0f, 1f, TechniqueAction.Receive,
                PlayerAbilityProfile.Default);

            Assert.That(locomotion.MaximumSpeed,
                Is.GreaterThanOrEqualTo((1.5f * locomotion.ScheduledMovementDistance) / .9f));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void UnplannedAttack_MaximumSpeedCoversTheActualVerticalParabola()
        {
            var root = new GameObject("UnplannedVerticalRoutePeak");
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 1f);
            locomotion.ConfigureScheduledMovement(
                Vector3.zero, 0f, 1f, TechniqueAction.Attack, PlayerAbilityProfile.Default);

            Assert.That(locomotion.MaximumSpeed, Is.GreaterThanOrEqualTo(4f));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SaturatedAttackRoute_ReservesFiniteAlignmentSpeedWithinThePublishedBound()
        {
            var root = new GameObject("SaturatedAlignmentAllowance");
            root.transform.position = new Vector3(0f, 0f, -4f);
            var locomotion = new PlayerLocomotion(root.transform, TeamId.Blue, CourtBuilder.HalfLength, 7f);
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -.18f), 0f, 1f, TechniqueAction.Attack,
                PlayerAbilityProfile.Default, 0f);
            locomotion.ConfigureAttackContact(new Vector3(0f, 3f, -.18f), .38f, PlayerAbilityProfile.Default);
            locomotion.SetRootPosition(locomotion.Sample(.50f, .01f, true).Position);
            var before = root.transform.position;
            locomotion.SetRootPosition(locomotion.Sample(.51f, .01f, true).Position);
            var applied = locomotion.ApplyAttackContactAlignment(new Vector3(.18f, 0f, 0f), .01f);

            Assert.That(applied.magnitude, Is.GreaterThan(0f));
            Assert.That(Vector3.Distance(before, root.transform.position),
                Is.LessThanOrEqualTo((locomotion.MaximumSpeed * .01f) + .0001f));
            Assert.That(locomotion.CurrentAttackAlignmentOffset.magnitude,
                Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance + .0001f));
            Object.DestroyImmediate(root);
        }

        private static PlayerLocomotion CreateAttackLocomotion()
        {
            var root = new GameObject("LocomotionAttacker");
            var locomotion = new PlayerLocomotion(
                root.transform,
                TeamId.Blue,
                CourtBuilder.HalfLength,
                7f);
            var ability = PlayerAbilityProfile.Default;
            locomotion.ConfigureScheduledMovement(
                new Vector3(0f, 0f, -1.2f),
                1.5f,
                2f,
                TechniqueAction.Attack,
                ability);
            locomotion.ConfigureAttackApproach(
                new AttackApproachPlan(
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -2f),
                    new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -0.8f),
                    1.2f,
                    1f,
                    0f),
                ability,
                2f);
            return locomotion;
        }

        private static void SampleAndSetRoot(PlayerLocomotion locomotion, float simulationTime)
        {
            locomotion.SetRootPosition(locomotion.Sample(simulationTime).Position);
        }

        private static void SampleAndSetRoot(
            PlayerLocomotion locomotion,
            float simulationTime,
            float elapsedStepSeconds)
        {
            locomotion.SetRootPosition(locomotion.Sample(simulationTime, elapsedStepSeconds).Position);
        }

        private static void SampleAndSetRoot(
            PlayerLocomotion locomotion,
            float simulationTime,
            float elapsedStepSeconds,
            bool shareAttackAlignmentStepBudget)
        {
            locomotion.SetRootPosition(locomotion.Sample(
                simulationTime,
                elapsedStepSeconds,
                shareAttackAlignmentStepBudget).Position);
        }
    }
}

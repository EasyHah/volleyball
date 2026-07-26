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
    }
}

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
    }
}

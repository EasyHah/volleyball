using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class AttackContactPlannerTests
    {
        [Test]
        public void Plan_FullReadinessUsesMaximumReach()
        {
            var plan = AttackContactPlanner.Plan(new AttackContactInput(
                3.50f,
                1f,
                1f,
                SetQualityGrade.A,
                new SimVector3(1f, 0f, -2f),
                0.9f,
                1.2f));

            Assert.That(plan.Takeoff, Is.EqualTo(new SimVector3(1f, 0f, -2f)));
            Assert.That(plan.ContactCenter, Is.EqualTo(new SimVector3(1f, 3.50f, -2f)));
            Assert.That(plan.ApproachCompletion, Is.EqualTo(1f));
            Assert.That(plan.JumpTiming, Is.EqualTo(1f));
            Assert.That(plan.RequiredApproachSeconds, Is.EqualTo(0.9f));
            Assert.That(plan.AvailableApproachSeconds, Is.EqualTo(1.2f));
            Assert.That(plan.Outcome, Is.EqualTo(AttackContactOutcome.FullAttack));
        }

        [Test]
        public void Plan_PartialReadinessProducesAnAdjustedReachableContact()
        {
            var plan = AttackContactPlanner.Plan(new AttackContactInput(
                3.50f,
                0.5f,
                0.5f,
                SetQualityGrade.B,
                new SimVector3(1f, 0f, -2f),
                0.9f,
                1.2f));

            Assert.That(plan.ContactCenter.Y, Is.InRange(3.20f, 3.50f));
            Assert.That(plan.ContactCenter.Y, Is.LessThan(3.50f));
            Assert.That(plan.Outcome, Is.EqualTo(AttackContactOutcome.AdjustedAttack));
        }

        [Test]
        public void Plan_InsufficientTimeFallsBackToHandlingAtMinimumReach()
        {
            var plan = AttackContactPlanner.Plan(new AttackContactInput(
                3.42f,
                0.2f,
                0.2f,
                SetQualityGrade.C,
                new SimVector3(-1f, 0f, -2.4f),
                0.9f,
                0.1f));

            Assert.That(plan.ContactCenter.Y, Is.EqualTo(3.20f).Within(0.0001f));
            Assert.That(plan.Outcome, Is.EqualTo(AttackContactOutcome.Handling));
        }

        [Test]
        public void Input_RejectsInvalidReachReadinessGradeGeometryAndTime()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(maxAttackReach: 1.94f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(maxAttackReach: 3.96f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(approachCompletion: float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(jumpTiming: 1.01f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(grade: (SetQualityGrade)99));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(
                takeoff: new SimVector3(float.PositiveInfinity, 0f, 0f)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(requiredSeconds: -0.01f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create(availableSeconds: float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => AttackContactPlanner.Plan(default));
        }

        private static AttackContactInput Create(
            float maxAttackReach = 3.42f,
            float approachCompletion = 1f,
            float jumpTiming = 1f,
            SetQualityGrade grade = SetQualityGrade.A,
            SimVector3 takeoff = default,
            float requiredSeconds = 0.9f,
            float availableSeconds = 1.2f)
        {
            return new AttackContactInput(
                maxAttackReach,
                approachCompletion,
                jumpTiming,
                grade,
                takeoff,
                requiredSeconds,
                availableSeconds);
        }
    }
}

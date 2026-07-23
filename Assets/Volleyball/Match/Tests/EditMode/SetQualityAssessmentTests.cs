using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class SetQualityAssessmentTests
    {
        [Test]
        public void Evaluate_GradesExcellentAndPoorSetsAcrossAllContributingErrors()
        {
            var excellent = SetQualityAssessment.Evaluate(
                new SetQualityInput(0.02f, 0.03f, 0.02f, 1.1f, 0.8f));
            var poor = SetQualityAssessment.Evaluate(
                new SetQualityInput(0.45f, 0.35f, 0.35f, 0.7f, 0.1f));

            Assert.That(excellent.Grade, Is.EqualTo(SetQualityGrade.A));
            Assert.That(excellent.IsAdjustable, Is.True);
            Assert.That(poor.Grade, Is.EqualTo(SetQualityGrade.D));
            Assert.That(poor.IsAdjustable, Is.False);
        }

        [Test]
        public void PrimaryResponsibility_SeparatesGoodSetAttackFaultFromUnattackableSet()
        {
            Assert.That(
                SetQualityAssessment.PrimaryResponsibility(SetQualityGrade.A, AttackOutcome.Out),
                Is.EqualTo(AttackResponsibility.Attacker));
            Assert.That(
                SetQualityAssessment.PrimaryResponsibility(SetQualityGrade.E, AttackOutcome.NoNormalAttack),
                Is.EqualTo(AttackResponsibility.Setter));
        }

        [Test]
        public void Replan_BTrajectoryKeepsAttackerInNearNetBand()
        {
            var provisionalApproach = new AttackApproachPlan(
                new SimVector3(-1f, 0f, -2.8f),
                new SimVector3(0f, 0f, -1.2f),
                1.89f,
                0.9f,
                0.1f);
            var provisionalContact = AttackContactPlanner.Plan(new AttackContactInput(
                3.50f,
                0.9f,
                1f,
                SetQualityGrade.A,
                provisionalApproach.Takeoff,
                0.75f,
                1f));
            var actualCenter = new SimVector3(0.16f, 3.40f, -1.35f);
            var quality = SetQualityAssessment.Evaluate(
                new SetQualityInput(0.19f, 0.08f, 0.1f, 2.05f, 0.85f));

            var replan = SetAttackReplanner.Replan(
                provisionalApproach,
                provisionalContact,
                actualCenter,
                0.85f,
                3.50f,
                PlayerRole.Attacker,
                TeamId.Blue,
                1f,
                quality);

            Assert.That(quality.Grade, Is.EqualTo(SetQualityGrade.B));
            Assert.That(replan.Approach.Takeoff.X, Is.EqualTo(actualCenter.X));
            Assert.That(replan.Approach.Takeoff.Z, Is.InRange(-1.50f, -0.75f));
            Assert.That(replan.Approach.Takeoff, Is.Not.EqualTo(provisionalApproach.Takeoff));
            Assert.That(replan.ContactPlan.Takeoff, Is.EqualTo(replan.Approach.Takeoff));
            Assert.That(replan.ContactPlan.ContactCenter, Is.EqualTo(actualCenter));
            Assert.That(replan.ContactPlan.Outcome, Is.EqualTo(AttackContactOutcome.AdjustedAttack));
            Assert.That(replan.OpensSpikeContactWindow, Is.True);
        }

        [Test]
        public void Replan_BTrajectoryDoesNotOpenSpikeWindowWhenActualBallIsBeyondTakeoffBand()
        {
            var provisionalApproach = new AttackApproachPlan(
                new SimVector3(-1f, 0f, -2.8f),
                new SimVector3(0f, 0f, -2.2f),
                1.17f,
                0.9f,
                0.1f);
            var provisionalContact = AttackContactPlanner.Plan(new AttackContactInput(
                3.50f,
                0.9f,
                1f,
                SetQualityGrade.A,
                provisionalApproach.Takeoff,
                0.75f,
                1f));
            var actualCenter = new SimVector3(0.16f, 3.40f, -3.80f);
            var quality = SetQualityAssessment.Evaluate(
                new SetQualityInput(0.19f, 0.08f, 0.1f, 2.05f, 0.85f));

            var replan = SetAttackReplanner.Replan(
                provisionalApproach,
                provisionalContact,
                actualCenter,
                0.85f,
                3.50f,
                PlayerRole.Attacker,
                TeamId.Blue,
                1f,
                quality);

            Assert.That(replan.Approach.Takeoff.Z, Is.InRange(-1.50f, -0.75f));
            Assert.That(replan.ContactPlan.ContactCenter, Is.EqualTo(actualCenter));
            Assert.That(
                GroundDistance(replan.ContactPlan.ContactCenter, replan.ContactPlan.Takeoff),
                Is.GreaterThan(2f));
            Assert.That(replan.ContactPlan.Outcome, Is.EqualTo(AttackContactOutcome.Handling));
            Assert.That(replan.OpensSpikeContactWindow, Is.False);
            Assert.That(replan.Outcome, Is.EqualTo(AttackOutcome.NoNormalAttack));
        }

        [Test]
        public void Replan_DTrajectoryReturnsHandlingAndNoSpikeWindow()
        {
            var approach = new AttackApproachPlan(
                new SimVector3(-1f, 0f, -2.8f),
                new SimVector3(0f, 0f, -2.2f),
                1.17f,
                0.9f,
                0.1f);
            var contact = AttackContactPlanner.Plan(new AttackContactInput(
                3.50f, 0.9f, 1f, SetQualityGrade.A, approach.Takeoff, 0.75f, 1f));
            var quality = SetQualityAssessment.Evaluate(
                new SetQualityInput(0.45f, 0.35f, 0.35f, 0.7f, 0.1f));

            var replan = SetAttackReplanner.Replan(
                approach,
                contact,
                new SimVector3(0.45f, 3.20f, -0.7f),
                0.1f,
                3.50f,
                PlayerRole.Attacker,
                TeamId.Blue,
                1f,
                quality);

            Assert.That(replan.ContactPlan.Outcome, Is.EqualTo(AttackContactOutcome.Handling));
            Assert.That(replan.OpensSpikeContactWindow, Is.False);
            Assert.That(replan.Outcome, Is.EqualTo(AttackOutcome.NoNormalAttack));
        }

        [Test]
        public void Replan_DTrajectoryPreservesLowActualHeightForHandling()
        {
            var approach = new AttackApproachPlan(
                new SimVector3(-1f, 0f, -2.8f),
                new SimVector3(0f, 0f, -2.2f),
                1.17f,
                0.9f,
                0.1f);
            var contact = AttackContactPlanner.Plan(new AttackContactInput(
                3.50f, 0.9f, 1f, SetQualityGrade.A, approach.Takeoff, 0.75f, 1f));
            var quality = SetQualityAssessment.Evaluate(
                new SetQualityInput(0.45f, 0.8f, 0.35f, 0.7f, 0.1f));

            var replan = SetAttackReplanner.Replan(
                approach,
                contact,
                new SimVector3(0.45f, 2.40f, -0.7f),
                0.1f,
                3.50f,
                PlayerRole.Attacker,
                TeamId.Blue,
                1f,
                quality);

            Assert.That(replan.ContactPlan.Outcome, Is.EqualTo(AttackContactOutcome.Handling));
            Assert.That(replan.ContactPlan.ContactCenter.Y, Is.EqualTo(2.40f).Within(0.0001f));
        }

        [Test]
        public void ResponsiblePlayer_MapsFinalResponsibilityToSetterOrAttacker()
        {
            var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
            var attacker = new PlayerId(TeamId.Blue, PlayerRole.Attacker);
            var fallback = new PlayerId(TeamId.Blue, PlayerRole.Defender);

            Assert.That(
                SetChainAttribution.ResponsiblePlayer(
                    AttackResponsibility.Setter,
                    setter,
                    attacker,
                    fallback),
                Is.EqualTo(setter));
            Assert.That(
                SetChainAttribution.ResponsiblePlayer(
                    AttackResponsibility.Attacker,
                    setter,
                    attacker,
                    fallback),
                Is.EqualTo(attacker));
            Assert.That(
                SetChainAttribution.ResponsiblePlayer(
                    AttackResponsibility.None,
                    setter,
                    attacker,
                    fallback),
                Is.EqualTo(fallback));
        }

        private static float GroundDistance(SimVector3 a, SimVector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)System.Math.Sqrt((dx * dx) + (dz * dz));
        }
    }
}

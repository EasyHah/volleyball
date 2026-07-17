using System;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class LlmDecisionProtocolTests
    {
        [Test]
        public void RoundDecision_ValidActorsAndRoutes_PreservesValues()
        {
            var decision = new RoundDecisionV1(
                PlayerRole.Defender,
                PlayerRole.Setter,
                SetRoute.RightPin,
                PlayerRole.Attacker,
                SpikeRoute.Line);

            Assert.That(decision.Receiver, Is.EqualTo(PlayerRole.Defender));
            Assert.That(decision.SecondActor, Is.EqualTo(PlayerRole.Setter));
            Assert.That(decision.ThirdActor, Is.EqualTo(PlayerRole.Attacker));
            Assert.That(decision.SetRoute, Is.EqualTo(SetRoute.RightPin));
            Assert.That(decision.AttackRoute, Is.EqualTo(SpikeRoute.Line));
        }

        [Test]
        public void RoundDecision_AdjacentActorRepeats_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RoundDecisionV1(
                PlayerRole.Setter,
                PlayerRole.Setter,
                SetRoute.LeftPin,
                PlayerRole.Attacker,
                SpikeRoute.CrossCourt));

            Assert.Throws<ArgumentException>(() => new RoundDecisionV1(
                PlayerRole.Defender,
                PlayerRole.Setter,
                SetRoute.LeftPin,
                PlayerRole.Setter,
                SpikeRoute.CrossCourt));
        }

        [Test]
        public void RoundDecision_FirstActorMayReturnForThirdContact()
        {
            Assert.DoesNotThrow(() => new RoundDecisionV1(
                PlayerRole.Attacker,
                PlayerRole.Setter,
                SetRoute.BackSet,
                PlayerRole.Attacker,
                SpikeRoute.RollShot));
        }

        [Test]
        public void TouchDecision_ThirdCountedTouch_MustGoOverNet()
        {
            var set = new TouchDecisionV1(
                PlayerRole.Setter,
                TouchDecisionAction.Set,
                TargetZone.RightFront,
                DecisionTempo.High,
                DecisionRisk.Safe);

            Assert.That(TouchDecisionRules.Validate(set, countedTeamTouches: 2).IsValid, Is.False);

            var attack = new TouchDecisionV1(
                PlayerRole.Attacker,
                TouchDecisionAction.Attack,
                TargetZone.MiddleBack,
                DecisionTempo.Quick,
                DecisionRisk.Balanced);

            Assert.That(TouchDecisionRules.Validate(attack, countedTeamTouches: 2).IsValid, Is.True);
        }
    }
}

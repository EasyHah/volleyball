using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class AttackRouteSelectorTests
    {
        [Test]
        public void Select_AvoidsTheObstructedLineRouteDeterministically()
        {
            var input = new AttackRouteSelectionInput(
                TeamId.Blue,
                new SimVector3(0f, 3.42f, -1.10f),
                normalFlightSeconds: 0.45f,
                predictedArms: CentralBlockArms(),
                parameters: new BallSimulationParameters(-9.8f, 0.9995f),
                fixedStepSeconds: 1f / 120f);

            var first = AttackRouteSelector.Select(input);
            var repeated = AttackRouteSelector.Select(input);

            Assert.That(
                first.Route,
                Is.EqualTo(GeometricAttackRoute.CrossCourt)
                    .Or.EqualTo(GeometricAttackRoute.OverHand));
            Assert.That(first.Route, Is.Not.EqualTo(GeometricAttackRoute.Line));
            Assert.That(repeated, Is.EqualTo(first));
        }

        [Test]
        public void Select_PinAttackAvoidsAnUnexecutableExtremeCrossCourtVelocity()
        {
            var selected = AttackRouteSelector.Select(new AttackRouteSelectionInput(
                TeamId.Orange,
                new SimVector3(-4.03f, 3.36f, 1.51f),
                normalFlightSeconds: 0.45f,
                predictedArms: new[]
                {
                    new ContactCapsuleFrame(
                        new SimVector3(-3.93f, 2.55f, 0f),
                        new SimVector3(-3.93f, 3.65f, 0f),
                        0.18f)
                },
                parameters: new BallSimulationParameters(-9.8f, 0.9995f),
                fixedStepSeconds: 1f / 120f));

            Assert.That(selected.InitialVelocity.Magnitude, Is.LessThan(19f));
            Assert.That(selected.FlightSeconds, Is.LessThan(0.8f));
        }

        private static ContactCapsuleFrame[] CentralBlockArms()
        {
            return new[]
            {
                new ContactCapsuleFrame(
                    new SimVector3(0f, 2.55f, 0f),
                    new SimVector3(0f, 3.65f, 0f),
                    0.18f)
            };
        }
    }
}

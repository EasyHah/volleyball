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

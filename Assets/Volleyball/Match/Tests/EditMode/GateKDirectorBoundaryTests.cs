using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class GateKDirectorBoundaryTests
    {
        [Test]
        public void Director_DoesNotOwnTheTacticalDecisionPlanner()
        {
            var fields = typeof(PhysicalMatchRallyDirector).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public);

            Assert.That(fields.Select(field => field.FieldType),
                Has.None.EqualTo(typeof(TeamRallyDecisionPlanner)));
        }

        [Test]
        public void DecisionCoordinator_IsPureAiAndOwnsPlannerCreation()
        {
            var type = typeof(RallyDecisionCoordinatorV3);

            Assert.That(type.Assembly, Is.EqualTo(typeof(TeamRallyDecisionPlanner).Assembly));
            Assert.That(type.GetMethod(nameof(
                RallyDecisionCoordinatorV3.CreateReceiveOrganizationPlanner)),
                Is.Not.Null);
        }
    }
}

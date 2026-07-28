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

        [Test]
        public void Director_DoesNotOwnEventScopedAuthorityReceiptStores()
        {
            var fields = typeof(PhysicalMatchRallyDirector).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public);

            Assert.That(fields.Select(field => field.Name),
                Has.None.Contains("_pendingGateHContactReceipts"));
            Assert.That(fields.Select(field => field.Name),
                Has.None.Contains("_pendingGateISetIntentReceipts"));
            Assert.That(fields.Select(field => field.Name),
                Has.None.Contains("_pendingGateIContactReceipts"));
            Assert.That(fields.Select(field => field.Name),
                Has.None.Contains("_activeGateISetIntent"));
            Assert.That(fields.Select(field => field.Name),
                Has.None.Contains("_gateHPlanRevision"));
            Assert.That(fields.Select(field => field.Name),
                Has.None.Contains("_gateHSourceSequence"));
            Assert.That(fields.Select(field => field.FieldType),
                Does.Contain(typeof(FormalRallyAuthorityOrchestrator)));
            Assert.That(fields.Select(field => field.FieldType),
                Has.None.EqualTo(
                    typeof(ReceiveOrganizationAuthorityCoordinator)));
            Assert.That(fields.Select(field => field.FieldType),
                Has.None.EqualTo(
                    typeof(AttackDefenseAuthorityCoordinator)));
        }
    }
}

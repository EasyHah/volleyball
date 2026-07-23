using System;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerTrainingEmphasisPersistenceTests
    {
        [Test]
        public void CreatedAndPlanningSnapshotsCarryRequiredEmptyLedger()
        {
            var created = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.Parse("31000000-0000-0000-0000-000000000001")),
                new SaveId(Guid.Parse("32000000-0000-0000-0000-000000000001")),
                new LineageId(Guid.Parse("33000000-0000-0000-0000-000000000001")));
            var planning = CareerPersistenceTestData.PlanningSnapshot(
                new ProfileId(Guid.Parse("31000000-0000-0000-0000-000000000002")),
                new SaveId(Guid.Parse("32000000-0000-0000-0000-000000000002")),
                new LineageId(Guid.Parse("33000000-0000-0000-0000-000000000002")));

            Assert.That(created.TrainingEmphases.Contributions, Is.Empty);
            Assert.That(planning.TrainingEmphases.Contributions, Is.Empty);
        }

        [Test]
        public void CanonicalJsonPlacesContentIdAfterKindAndEmphasesAfterProgression()
        {
            var snapshot = CareerPersistenceTestData.PlanningSnapshot(
                new ProfileId(Guid.Parse("31000000-0000-0000-0000-000000000003")),
                new SaveId(Guid.Parse("32000000-0000-0000-0000-000000000003")),
                new LineageId(Guid.Parse("33000000-0000-0000-0000-000000000003")));
            var text = Encoding.UTF8.GetString(
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(snapshot)));

            StringAssert.Contains(
                "\"kind\":\"specialized_training\",\"contentId\":\"week_action.specialized.spike\"",
                text);
            StringAssert.Contains(
                "\"pendingEvent\":null,\"matchSessionId\":null}," +
                "\"trainingEmphases\":[],\"pendingMatch\":null,\"player\":",
                text);
        }

        [Test]
        public void StrictJsonRejectsIncompletePreStage4V1Shape()
        {
            var snapshot = CareerPersistenceTestData.PlanningSnapshot(
                new ProfileId(Guid.Parse("31000000-0000-0000-0000-000000000004")),
                new SaveId(Guid.Parse("32000000-0000-0000-0000-000000000004")),
                new LineageId(Guid.Parse("33000000-0000-0000-0000-000000000004")));
            var canonical = Encoding.UTF8.GetString(
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(snapshot)));
            var withoutEmphases = canonical.Replace("\"trainingEmphases\":[],", string.Empty);
            var withoutContentId = canonical.Replace(
                ",\"contentId\":\"week_action.specialized.spike\"",
                string.Empty);

            Assert.That(
                () => CareerSaveJsonCodec.Deserialize(Encoding.UTF8.GetBytes(withoutEmphases)),
                Throws.TypeOf<FormatException>());
            Assert.That(
                () => CareerSaveJsonCodec.Deserialize(Encoding.UTF8.GetBytes(withoutContentId)),
                Throws.TypeOf<FormatException>());
        }
    }
}

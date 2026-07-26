using NUnit.Framework;
using Volleyball.Domain.Players;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class PlayerActionTimelineTests
    {
        [Test]
        public void Schedule_ExposesActualTimelineAndCancelClearsWindow()
        {
            var timeline = new PlayerActionTimeline();
            timeline.ScheduleContact(TechniqueAction.Attack, 2f, 0.03f);

            Assert.That(timeline.Sample(2.03f).SurfaceActive, Is.True);
            Assert.That(timeline.HasScheduledContact, Is.True);

            timeline.CancelContact();

            Assert.That(timeline.HasScheduledContact, Is.False);
            Assert.That(timeline.TrySampleContact(2.03f, out _), Is.False);
        }
    }
}

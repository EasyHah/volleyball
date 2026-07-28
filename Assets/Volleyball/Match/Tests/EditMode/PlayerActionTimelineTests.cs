using NUnit.Framework;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
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

        [Test]
        public void SupportWindow_SamplesAtContactAndDisableClearsIt()
        {
            var timeline = new PlayerActionTimeline();
            timeline.ScheduleSupport(TechniqueAction.Receive, 2f);

            Assert.That(timeline.SupportAction, Is.EqualTo(TechniqueAction.Receive));
            Assert.That(timeline.TrySampleSupport(2f, out var sample), Is.True);
            Assert.That(sample.SurfaceActive, Is.True);

            timeline.DisableSupport();

            Assert.That(timeline.TrySampleSupport(2f, out _), Is.False);
        }

        [Test]
        public void EmergencyReceive_NormalizesWindowAndDisableClearsIt()
        {
            var timeline = new PlayerActionTimeline();
            var velocity = new SimVector3(1f, 2f, 3f);

            timeline.EnableEmergencyReceive(4f, 3f, velocity, 7);

            Assert.That(timeline.HasEmergencyReceiveWindow, Is.True);
            Assert.That(timeline.EmergencyReceiveStartSimulationTime, Is.EqualTo(4f));
            Assert.That(timeline.EmergencyReceiveEndSimulationTime, Is.EqualTo(4f));
            Assert.That(timeline.EmergencyReceiveTargetVelocity, Is.EqualTo(velocity));
            Assert.That(timeline.EmergencyReceiveContactGroupId, Is.EqualTo(7));

            timeline.DisableEmergencyReceive();

            Assert.That(timeline.HasEmergencyReceiveWindow, Is.False);
        }
    }
}

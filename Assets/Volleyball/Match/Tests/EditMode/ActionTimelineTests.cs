using NUnit.Framework;
using Volleyball.Domain.Players;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class ActionTimelineTests
    {
        [Test]
        public void Sample_ProgressesThroughFivePhasesAroundAbsoluteContactTime()
        {
            var timeline = new ActionTimeline(TechniqueAction.Attack, 10f);

            Assert.That(timeline.Sample(9.4f).Phase, Is.EqualTo(ActionPhase.Prepare));
            Assert.That(timeline.Sample(9.9f).Phase, Is.EqualTo(ActionPhase.Power));
            Assert.That(timeline.Sample(10f).Phase, Is.EqualTo(ActionPhase.Contact));
            Assert.That(timeline.Sample(10.12f).Phase, Is.EqualTo(ActionPhase.FollowThrough));
            Assert.That(timeline.Sample(10.5f).Phase, Is.EqualTo(ActionPhase.Recover));
        }

        [Test]
        public void Sample_UsesHysteresisForOneStableContactWindow()
        {
            var timeline = new ActionTimeline(TechniqueAction.Receive, 3f);

            Assert.That(timeline.Sample(2.9f).SurfaceActive, Is.False);
            Assert.That(timeline.Sample(2.96f).SurfaceActive, Is.True);
            Assert.That(timeline.Sample(3.03f).SurfaceActive, Is.True);
            Assert.That(timeline.Sample(3.07f).SurfaceActive, Is.False);
        }

        [Test]
        public void Constructor_AppliesVisibleTimingErrorToScheduledContact()
        {
            var early = new ActionTimeline(TechniqueAction.Set, 5f, -0.08f);

            Assert.That(early.ActualContactTime, Is.EqualTo(4.92f).Within(0.0001f));
            Assert.That(early.Sample(4.92f).Phase, Is.EqualTo(ActionPhase.Contact));
        }
    }
}

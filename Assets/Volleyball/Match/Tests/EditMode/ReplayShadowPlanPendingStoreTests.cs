using NUnit.Framework;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class ReplayShadowPlanPendingStoreTests
    {
        [Test]
        public void PendingStore_AttachesOnlyPlanForNextOneBasedV3SourceSequence()
        {
            var store = new ReplayShadowPlanPendingStore<string>();
            Assert.That(store.TryAdd(1, "first"), Is.True);
            Assert.That(store.TryAdd(2, "second"), Is.True);

            Assert.That(store.TryTakeForReplaySequence(0, out var first), Is.True);
            Assert.That(first, Is.EqualTo("first"));
            Assert.That(store.TryTakeForReplaySequence(1, out var second), Is.True);
            Assert.That(second, Is.EqualTo("second"));
        }

        [Test]
        public void PendingStore_RejectsDuplicateSourceAndCannotAttachStaleOrOutOfOrderPlan()
        {
            var store = new ReplayShadowPlanPendingStore<string>();
            Assert.That(store.TryAdd(2, "future"), Is.True);
            Assert.That(store.TryAdd(2, "duplicate"), Is.False);

            Assert.That(store.TryTakeForReplaySequence(0, out var first), Is.False);
            Assert.That(first, Is.Null);
            Assert.That(store.TryAdd(1, "stale"), Is.False);
            Assert.That(store.TryTakeForReplaySequence(1, out var skipped), Is.True);
            Assert.That(skipped, Is.EqualTo("future"));
            Assert.That(store.TryTakeForReplaySequence(0, out var stale), Is.False);
            Assert.That(stale, Is.Null);
        }

        [Test]
        public void PendingStore_RejectsOutOfOrderSourceCallbacksBeforeAnyReplayContact()
        {
            var store = new ReplayShadowPlanPendingStore<string>();
            Assert.That(store.TryAdd(3, "third"), Is.True);

            Assert.That(store.TryAdd(2, "second"), Is.False);
            Assert.That(store.TryTakeForReplaySequence(1, out var second), Is.False);
            Assert.That(second, Is.Null);
            Assert.That(store.TryTakeForReplaySequence(2, out var third), Is.True);
            Assert.That(third, Is.EqualTo("third"));
        }

        [Test]
        public void PendingStore_RejectsSourceThatArrivesAfterItsReplayContact()
        {
            var store = new ReplayShadowPlanPendingStore<string>();
            Assert.That(store.TryTakeForReplaySequence(0, out var first), Is.False);
            Assert.That(first, Is.Null);

            Assert.That(store.TryAdd(1, "late"), Is.False);
            Assert.That(store.TryTakeForReplaySequence(1, out var second), Is.False);
            Assert.That(second, Is.Null);
        }

        [Test]
        public void PendingStore_DiscardsUnresolvedPlansOnlyAtResolution()
        {
            var store = new ReplayShadowPlanPendingStore<string>();
            Assert.That(store.TryAdd(4, "unresolved"), Is.True);

            Assert.That(store.Clear(), Is.EqualTo(1));
            Assert.That(store.TryTakeForReplaySequence(3, out var discarded), Is.False);
            Assert.That(discarded, Is.Null);
        }
    }
}

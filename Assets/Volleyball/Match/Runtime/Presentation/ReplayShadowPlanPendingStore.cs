using System;
using System.Collections.Generic;

namespace Volleyball.Presentation
{
    /// <summary>
    /// Binds one-based V3 transition sources to zero-based replay contact indexes.
    /// </summary>
    public sealed class ReplayShadowPlanPendingStore<T> where T : class
    {
        private readonly Dictionary<long, T> _plansBySourceSequence =
            new Dictionary<long, T>();
        private readonly long _captureBaseSourceSequence;
        private long _lastAcceptedSourceSequence;
        private long _lastReplaySourceSequence;

        public ReplayShadowPlanPendingStore(long captureBaseSourceSequence = 0)
        {
            if (captureBaseSourceSequence < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(captureBaseSourceSequence));
            }

            _captureBaseSourceSequence = captureBaseSourceSequence;
            _lastAcceptedSourceSequence = captureBaseSourceSequence;
            _lastReplaySourceSequence = captureBaseSourceSequence;
        }

        public bool TryAdd(long sourceSequence, T plan)
        {
            if (sourceSequence < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceSequence),
                    "V3 source sequences are one-based.");
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (sourceSequence <= _lastAcceptedSourceSequence ||
                sourceSequence <= _lastReplaySourceSequence)
            {
                return false;
            }

            if (!_plansBySourceSequence.TryAdd(sourceSequence, plan))
            {
                return false;
            }

            _lastAcceptedSourceSequence = sourceSequence;
            return true;
        }

        public bool TryTakeForReplaySequence(int replaySequence, out T plan)
        {
            if (replaySequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(replaySequence));
            }

            var expectedSourceSequence = _captureBaseSourceSequence +
                (long)replaySequence + 1L;
            if (expectedSourceSequence > _lastReplaySourceSequence)
            {
                _lastReplaySourceSequence = expectedSourceSequence;
            }
            if (_plansBySourceSequence.TryGetValue(expectedSourceSequence, out plan))
            {
                _plansBySourceSequence.Remove(expectedSourceSequence);
                return true;
            }

            plan = null;
            return false;
        }

        public int Clear()
        {
            var count = _plansBySourceSequence.Count;
            _plansBySourceSequence.Clear();
            return count;
        }
    }
}

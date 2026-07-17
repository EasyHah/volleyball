using System;
using System.Collections;
using UnityEngine;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.Presentation
{
    public sealed class BallFlight : MonoBehaviour
    {
        public IEnumerator Play(BallArc arc, float durationSeconds, Action onCompleted)
        {
            if (!IsFinite(durationSeconds) || durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    durationSeconds,
                    "Duration must be finite and greater than zero.");
            }

            return PlayValidated(arc, durationSeconds, onCompleted);
        }

        private IEnumerator PlayValidated(BallArc arc, float durationSeconds, Action onCompleted)
        {
            var elapsedSeconds = 0f;
            while (elapsedSeconds < durationSeconds)
            {
                ApplyPosition(arc.Evaluate(elapsedSeconds / durationSeconds));
                yield return null;
                elapsedSeconds += Time.deltaTime;
            }

            ApplyPosition(arc.Evaluate(1f));
            if (onCompleted != null)
            {
                onCompleted();
            }
        }

        private void ApplyPosition(BallArcPoint point)
        {
            transform.position = new Vector3(point.X, point.Y, point.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

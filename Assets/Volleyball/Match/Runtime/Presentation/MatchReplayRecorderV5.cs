using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    /// <summary>Records native V5 attribute-consumption evidence from accepted contacts.</summary>
    public sealed class MatchReplayRecorderV5 : MonoBehaviour
    {
        private FormalSixVsSixRallyDirector _director;
        private readonly List<MatchReplayAttributeEvidenceV5> _evidence =
            new List<MatchReplayAttributeEvidenceV5>();
        private bool _capturing;

        public static MatchReplayRecorderV5 Attach(FormalSixVsSixRallyDirector director)
        {
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (director.MatchContextV5 == null)
                throw new InvalidOperationException("V5 replay recording requires a V5 formal context.");
            var recorder = director.gameObject.AddComponent<MatchReplayRecorderV5>();
            recorder._director = director;
            director.ReplayContactAccepted += recorder.RecordContact;
            recorder._capturing = true;
            return recorder;
        }

        public MatchReplayV5 Complete()
        {
            if (!_capturing) throw new InvalidOperationException("V5 replay capture is not active.");
            if (_director.ResultV5 == null) throw new InvalidOperationException("V5 replay cannot complete before its result.");
            _capturing = false;
            return MatchReplayV5.Create("formal-v5-" + _director.MatchContextV5.SessionId.ToString("D"),
                _director.MatchContextV5, _evidence);
        }

        private void RecordContact(ReplayContactEvent replayEvent)
        {
            if (!_capturing || replayEvent?.PlayerId == null) return;
            var fingerprint = Fingerprint(_director.MatchContextV5, replayEvent.PlayerId.Value);
            _evidence.Add(new MatchReplayAttributeEvidenceV5(_evidence.Count,
                replayEvent.PlayerId.Value, replayEvent.Action.ToString(),
                Mathf.Max(0, Mathf.RoundToInt(replayEvent.SimulationTimeSeconds * 1000f)), fingerprint));
        }

        private void OnDestroy()
        {
            if (_director != null) _director.ReplayContactAccepted -= RecordContact;
        }

        private static string Fingerprint(MatchContextV5 context,
            Volleyball.Shared.Contracts.PlayerId playerId)
        {
            foreach (var player in context.Home.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            foreach (var player in context.Away.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            throw new ContractValidationException("V5 replay contact actor is absent from the V5 context.");
        }
    }
}

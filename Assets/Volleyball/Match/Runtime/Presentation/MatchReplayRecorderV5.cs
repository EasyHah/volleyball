using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    /// <summary>Records native V5 attribute-consumption evidence from accepted contacts.</summary>
    public sealed class MatchReplayRecorderV5 : MonoBehaviour
    {
        private FormalSixVsSixRallyDirector _director;
        private readonly List<MatchReplayAttributeEvidenceV5> _evidence =
            new List<MatchReplayAttributeEvidenceV5>();
        private readonly List<MatchReplayReportFactV1> _reportFacts =
            new List<MatchReplayReportFactV1>();
        private PlayerId? _lastActor;
        private TechniqueAction _lastAction;
        private int _lastContactFactSequence = -1;
        private bool _capturing;

        public static MatchReplayRecorderV5 Attach(FormalSixVsSixRallyDirector director)
        {
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (director.MatchContextV5 == null)
                throw new InvalidOperationException("V5 replay recording requires a V5 formal context.");
            var recorder = director.gameObject.AddComponent<MatchReplayRecorderV5>();
            recorder._director = director;
            director.ReplayContactAccepted += recorder.RecordContact;
            director.ReplayDecisionPlanned += recorder.RecordDecision;
            director.ReplayRallyResolved += recorder.RecordRally;
            director.ReplayPositionFault += recorder.RecordPositionFault;
            recorder._capturing = true;
            return recorder;
        }

        public MatchReplayV5 Complete()
        {
            if (!_capturing) throw new InvalidOperationException("V5 replay capture is not active.");
            if (_director.ResultV5 == null) throw new InvalidOperationException("V5 replay cannot complete before its result.");
            _capturing = false;
            return MatchReplayV5.Create("formal-v5-" + _director.MatchContextV5.SessionId.ToString("D"),
                _director.MatchContextV5, _evidence, _reportFacts,
                _director.ResultV5.PositionFaults);
        }

        public CareerMatchReportV1 CompleteReport(MatchReplayV5 replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            if (_director.ResultV5 == null) throw new InvalidOperationException("V5 report cannot complete before its result.");
            if (!string.Equals(replay.ContextHash, _director.MatchContextV5.ContextHash, StringComparison.Ordinal))
                throw new ContractValidationException("V5 replay does not bind the captured context.");
            return CareerMatchReportAggregatorV1.Aggregate(_director.MatchContextV5,
                _director.ResultV5, replay);
        }

        private void RecordContact(ReplayContactEvent replayEvent)
        {
            if (!_capturing || replayEvent?.PlayerId == null) return;
            var fingerprint = Fingerprint(_director.MatchContextV5, replayEvent.PlayerId.Value);
            _evidence.Add(new MatchReplayAttributeEvidenceV5(_evidence.Count,
                replayEvent.PlayerId.Value, replayEvent.Action.ToString(),
                Mathf.Max(0, Mathf.RoundToInt(replayEvent.SimulationTimeSeconds * 1000f)), fingerprint));
            _reportFacts.Add(new MatchReplayReportFactV1(_reportFacts.Count, replayEvent.PlayerId.Value,
                "Contact", replayEvent.Action.ToString(), true, false, Workload(replayEvent.Action),
                replayEvent.MovementMillimeters, attributeEvidenceSequenceNumber: _evidence.Count - 1));
            _lastContactFactSequence = _reportFacts.Count - 1;
            _lastActor = replayEvent.PlayerId.Value;
            _lastAction = replayEvent.Action;
        }

        private void RecordRally(ReplayRallyResolvedEvent rally)
        {
            if (!_capturing) return;
            if (_lastActor.HasValue && rally.PlayerId.HasValue && rally.PlayerId.Value.Equals(_lastActor.Value))
            {
                _reportFacts.Add(new MatchReplayReportFactV1(_reportFacts.Count, rally.PlayerId.Value,
                    "RallyResult", _lastAction.ToString(), true, rally.IsCritical, 0,
                    relatedContactSequenceNumber: _lastContactFactSequence));
            }
            if (_lastActor.HasValue && rally.ErrorPlayerId.HasValue && rally.ErrorPlayerId.Value.Equals(_lastActor.Value))
            {
                _reportFacts.Add(new MatchReplayReportFactV1(_reportFacts.Count, rally.ErrorPlayerId.Value,
                    "RallyResult", _lastAction.ToString(), false, rally.IsCritical, 0,
                    relatedContactSequenceNumber: _lastContactFactSequence));
            }
            _lastActor = null;
            _lastContactFactSequence = -1;
        }

        private void OnDestroy()
        {
            if (_director != null) _director.ReplayContactAccepted -= RecordContact;
            if (_director != null) _director.ReplayDecisionPlanned -= RecordDecision;
            if (_director != null) _director.ReplayRallyResolved -= RecordRally;
            if (_director != null) _director.ReplayPositionFault -= RecordPositionFault;
        }

        private void RecordDecision(ReplayDecisionEvent decision)
        {
            if (!_capturing || decision == null || decision.Candidates == null) return;
            var executable = 0;
            var selectedScore = float.NegativeInfinity;
            var bestScore = float.NegativeInfinity;
            foreach (var candidate in decision.Candidates)
            {
                if (!candidate.IsFeasible) continue;
                executable++;
                bestScore = Mathf.Max(bestScore, candidate.Score.Total);
                if (candidate.Actor.Equals(decision.SelectedPlayer)) selectedScore = candidate.Score.Total;
            }
            if (executable < 2) return;
            _reportFacts.Add(new MatchReplayReportFactV1(_reportFacts.Count, StablePlayer(decision.SelectedPlayer),
                "Decision", decision.SelectedAction.ToString(), selectedScore >= bestScore, false, 0,
                executableChoices: executable, selectedChoice: decision.SelectedAction.ToString(),
                decisionReason: selectedScore >= bestScore ? "HighestExecutableScore" : "LowerExecutableScore"));
        }

        private void RecordPositionFault(ReplayPositionFaultEvent fault)
        {
            if (!_capturing || fault == null) return;
            // The immutable Match artifact owns the actual facts; this event confirms no contacts ran first.
            if (_evidence.Count != 0 || _reportFacts.Count != 0)
                throw new InvalidOperationException("V5 position faults must resolve before contact evidence.");
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

        private static int Workload(TechniqueAction action)
        {
            return action == TechniqueAction.Attack ? 90 :
                action == TechniqueAction.Block ? 75 :
                action == TechniqueAction.Receive ? 40 : 25;
        }

        private PlayerId StablePlayer(Volleyball.Domain.Prototype.PlayerId player)
        {
            var team = player.Team == Volleyball.Domain.Prototype.TeamId.Blue
                ? _director.MatchContextV5.Home : _director.MatchContextV5.Away;
            if (player.RosterSlot < 0 || player.RosterSlot >= team.RotationOrder.Count)
                throw new ContractValidationException("V5 decision actor cannot be mapped to the frozen context.");
            return team.RotationOrder[player.RosterSlot].PlayerId;
        }
    }
}

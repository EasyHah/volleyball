using System;
using System.Collections.Generic;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    /// <summary>Deterministically turns Match-owned V5 replay facts into Career's read-only report.</summary>
    public static class CareerMatchReportAggregatorV1
    {
        public static CareerMatchReportV1 Aggregate(MatchContextV5 context,
            MatchResultV5 result, MatchReplayV5 replay)
        {
            if (context == null || result == null || replay == null) throw new ArgumentNullException();
            result.ValidateAgainst(context);
            replay.ValidateAgainst(context);
            if (!MatchPositionFaultV5.SequencesEqual(result.PositionFaults, replay.PositionFaults))
                throw new ContractValidationException(
                    "Physical replay position-fault evidence does not match the result.");
            var values = new Dictionary<PlayerId, Mutable>();
            foreach (var player in context.Home.RotationOrder) values.Add(player.PlayerId, new Mutable(player.PlayerId));
            foreach (var player in context.Away.RotationOrder) values.Add(player.PlayerId, new Mutable(player.PlayerId));
            foreach (var fact in replay.ReportFacts) values[fact.PlayerId].Apply(fact);
            var reports = new List<CareerMatchPlayerReportV1>(12);
            foreach (var player in context.Home.RotationOrder) reports.Add(values[player.PlayerId].Report(result.RalliesPlayed));
            foreach (var player in context.Away.RotationOrder) reports.Add(values[player.PlayerId].Report(result.RalliesPlayed));
            return CareerMatchReportV1.Create(context, result, CareerMatchEvidenceKindV1.PhysicalReplay,
                replay.ReplayHash, reports);
        }

        private sealed class Mutable
        {
            private readonly PlayerId _playerId;
            private int _attackAttempts, _attackPoints, _attackErrors, _serveAttempts, _serveAces, _serveErrors;
            private int _receiveAttempts, _receivePerfect, _receivePositive, _receiveNeutral, _receiveNegative, _receiveErrors;
            private int _defenseAttempts, _defenseSuccesses, _blockAttempts, _blockTouches, _blockPoints;
            private int _setAttempts, _setSuccesses, _setErrors, _jumps, _criticalActions, _criticalSuccesses, _criticalErrors, _workload, _movement;
            private int _decisionSuccesses, _decisionErrors, _streakEpisodes, _longestStreak, _currentErrorStreak;
            private readonly Dictionary<int, string> _receiveQualityByContact = new Dictionary<int, string>();
            public Mutable(PlayerId playerId) { _playerId = playerId; }
            public void Apply(MatchReplayReportFactV1 fact)
            {
                if (fact.Success && fact.Kind != "RallyResult") _currentErrorStreak = 0;
                if (fact.Kind == "Contact")
                {
                    _workload = Math.Min(10000, _workload + fact.WorkloadBasisPoints);
                    _movement += fact.MovementMillimeters;
                    switch (fact.Action)
                    {
                        case "Attack": _attackAttempts++; _jumps++; break;
                        case "Serve": _serveAttempts++; break;
                        case "Receive": AddReceive(fact); _defenseAttempts++; _defenseSuccesses++; break;
                        case "Set": _setAttempts++; _setSuccesses++; break;
                        // The accepted block contact is the Match proof of an
                        // effective touch; a point still requires the resolved rally.
                        case "Block": _blockAttempts++; _blockTouches++; _jumps++; break;
                    }
                    return;
                }
                if (fact.Kind == "Decision")
                {
                    if (fact.Success) _decisionSuccesses++; else _decisionErrors++;
                    return;
                }
                if (fact.Kind != "RallyResult") return;
                if (fact.Success) _currentErrorStreak = 0;
                else
                {
                    if (_currentErrorStreak == 0) _streakEpisodes++;
                    _currentErrorStreak++;
                    _longestStreak = Math.Max(_longestStreak, _currentErrorStreak);
                }
                if (fact.Action == "Attack") { if (fact.Success) _attackPoints++; else _attackErrors++; }
                if (fact.Action == "Serve") { if (fact.Success) _serveAces++; else _serveErrors++; }
                if (fact.Action == "Receive" && !fact.Success) ReclassifyReceiveAsError(fact.RelatedContactSequenceNumber);
                if (fact.Action == "Set" && !fact.Success) { _setSuccesses--; _setErrors++; }
                if (fact.Action == "Block" && fact.Success) _blockPoints++;
                if (fact.Critical) { _criticalActions++; if (fact.Success) _criticalSuccesses++; else _criticalErrors++; }
            }
            public CareerMatchPlayerReportV1 Report(int rallies)
            {
                return new CareerMatchPlayerReportV1(_playerId, _attackAttempts, _attackPoints, _attackErrors,
                    _serveAttempts, _serveAces, _serveErrors, _receiveAttempts, _receivePerfect, _receivePositive,
                    _receiveNeutral, _receiveNegative, _receiveErrors,
                    _defenseAttempts, _defenseSuccesses, _blockAttempts, _blockTouches, _blockPoints,
                    _setAttempts, _setSuccesses, _setErrors, rallies, _movement, _jumps, _workload, 1,
                    _criticalActions, _criticalSuccesses, _criticalErrors, _streakEpisodes, _longestStreak,
                    _decisionSuccesses, _decisionErrors);
            }

            private void AddReceive(MatchReplayReportFactV1 fact)
            {
                _receiveAttempts++;
                var quality = fact.MovementMillimeters <= 250 ? "Perfect" :
                    fact.MovementMillimeters <= 750 ? "Positive" :
                    fact.MovementMillimeters <= 1500 ? "Neutral" : "Negative";
                _receiveQualityByContact.Add(fact.SequenceNumber, quality);
                if (quality == "Perfect") _receivePerfect++;
                else if (quality == "Positive") _receivePositive++;
                else if (quality == "Neutral") _receiveNeutral++;
                else _receiveNegative++;
            }

            private void ReclassifyReceiveAsError(int contactSequenceNumber)
            {
                if (!_receiveQualityByContact.TryGetValue(contactSequenceNumber, out var quality))
                    throw new ContractValidationException("Receive error lacks its classified accepted contact.");
                if (quality == "Perfect") _receivePerfect--;
                else if (quality == "Positive") _receivePositive--;
                else if (quality == "Neutral") _receiveNeutral--;
                else _receiveNegative--;
                _receiveErrors++;
                _receiveQualityByContact.Remove(contactSequenceNumber);
            }
        }
    }
}

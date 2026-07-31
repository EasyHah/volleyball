using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    /// <summary>V5 replay metadata binds a formal replay to the frozen attributes it consumed.</summary>
    public sealed class MatchReplayV5
    {
        private readonly string[] _derivedAttributeFingerprints;
        private readonly MatchReplayAttributeEvidenceV5[] _attributeEvidence;

        private MatchReplayV5(string replayId, MatchContextV5 context,
            IReadOnlyList<string> derivedAttributeFingerprints,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence)
        {
            if (string.IsNullOrWhiteSpace(replayId))
            {
                throw new ContractValidationException("replayId is required.");
            }

            Context = context ?? throw new ContractValidationException("context is required.");
            Context.Validate();
            if (derivedAttributeFingerprints == null || derivedAttributeFingerprints.Count != 12)
            {
                throw new ContractValidationException("V5 replay requires twelve ordered derived fingerprints.");
            }

            _derivedAttributeFingerprints = new string[derivedAttributeFingerprints.Count];
            for (var index = 0; index < _derivedAttributeFingerprints.Length; index++)
            {
                var fingerprint = derivedAttributeFingerprints[index];
                ContractGuard.Hash(fingerprint, "derivedAttributeFingerprints");
                _derivedAttributeFingerprints[index] = fingerprint;
            }

            var expected = new List<string>(12);
            AddExpected(Context.Home, expected);
            AddExpected(Context.Away, expected);
            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(expected[index], _derivedAttributeFingerprints[index], StringComparison.Ordinal))
                {
                    throw new ContractValidationException("V5 replay attributes do not match its frozen context.");
                }
            }

            if (attributeEvidence == null)
            {
                throw new ContractValidationException("V5 replay attribute evidence is required.");
            }

            _attributeEvidence = new MatchReplayAttributeEvidenceV5[attributeEvidence.Count];
            for (var index = 0; index < _attributeEvidence.Length; index++)
            {
                var evidence = attributeEvidence[index] ??
                    throw new ContractValidationException("V5 replay evidence cannot contain null.");
                if (evidence.SequenceNumber != index)
                {
                    throw new ContractValidationException("V5 replay evidence must have contiguous sequence numbers.");
                }

                var expectedFingerprint = FindFingerprint(Context, evidence.PlayerId);
                if (!string.Equals(expectedFingerprint, evidence.DerivedAttributesFingerprint,
                        StringComparison.Ordinal))
                {
                    throw new ContractValidationException(
                        "V5 replay evidence must consume the actor's frozen derived attributes.");
                }

                _attributeEvidence[index] = evidence;
            }

            FormatVersion = ContractVersions.ReplayV5;
            ReplayId = replayId;
            ReplayHash = CanonicalMatchReplayHashV5.Compute(this);
        }

        public int FormatVersion { get; }
        public string ReplayId { get; }
        public MatchContextV5 Context { get; }
        public string ContextHash => Context.ContextHash;
        public IReadOnlyList<string> DerivedAttributeFingerprints =>
            new ReadOnlyCollection<string>(_derivedAttributeFingerprints);
        public IReadOnlyList<MatchReplayAttributeEvidenceV5> AttributeEvidence =>
            new ReadOnlyCollection<MatchReplayAttributeEvidenceV5>(_attributeEvidence);
        public string ReplayHash { get; }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints,
                Array.Empty<MatchReplayAttributeEvidenceV5>());
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints, attributeEvidence);
        }

        private static void AddExpected(TeamSnapshotV5 team, ICollection<string> output)
        {
            foreach (var player in team.RotationOrder) output.Add(player.Derived.ResultFingerprint);
        }

        private static string FindFingerprint(MatchContextV5 context, PlayerId playerId)
        {
            foreach (var player in context.Home.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            foreach (var player in context.Away.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            throw new ContractValidationException("V5 replay evidence actor is absent from its context.");
        }
    }

    /// <summary>One accepted physical contact and the frozen derived inputs it consumed.</summary>
    public sealed class MatchReplayAttributeEvidenceV5
    {
        public MatchReplayAttributeEvidenceV5(int sequenceNumber, PlayerId playerId,
            string action, int simulationMilliseconds,
            string derivedAttributesFingerprint)
        {
            if (sequenceNumber < 0) throw new ContractValidationException("V5 replay sequence cannot be negative.");
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            Action = ContractGuard.RequiredText(action, nameof(action), 32);
            if (simulationMilliseconds < 0)
                throw new ContractValidationException("V5 replay simulation time cannot be negative.");
            ContractGuard.Hash(derivedAttributesFingerprint, nameof(derivedAttributesFingerprint));
            SequenceNumber = sequenceNumber;
            SimulationMilliseconds = simulationMilliseconds;
            DerivedAttributesFingerprint = derivedAttributesFingerprint;
        }

        public int SequenceNumber { get; }
        public PlayerId PlayerId { get; }
        public string Action { get; }
        public int SimulationMilliseconds { get; }
        public string DerivedAttributesFingerprint { get; }
    }

    public static class CanonicalMatchReplayHashV5
    {
        public static string Compute(MatchReplayV5 replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            var output = new StringBuilder("volleyball.match-replay.v5\n");
            output.Append(replay.ReplayId).Append('|').Append(replay.ContextHash);
            foreach (var fingerprint in replay.DerivedAttributeFingerprints) output.Append('|').Append(fingerprint);
            foreach (var evidence in replay.AttributeEvidence)
            {
                output.Append('|').Append(evidence.SequenceNumber).Append(':')
                    .Append(evidence.PlayerId.Value).Append(':').Append(evidence.Action)
                    .Append(':').Append(evidence.SimulationMilliseconds).Append(':')
                    .Append(evidence.DerivedAttributesFingerprint);
            }
            return CanonicalJsonHashV4.Sha256(output.ToString());
        }
    }
}

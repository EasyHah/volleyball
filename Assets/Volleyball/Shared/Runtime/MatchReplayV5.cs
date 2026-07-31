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

        private MatchReplayV5(string replayId, MatchContextV5 context,
            IReadOnlyList<string> derivedAttributeFingerprints)
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
        public string ReplayHash { get; }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints);
        }

        private static void AddExpected(TeamSnapshotV5 team, ICollection<string> output)
        {
            foreach (var player in team.RotationOrder) output.Add(player.Derived.ResultFingerprint);
        }
    }

    public static class CanonicalMatchReplayHashV5
    {
        public static string Compute(MatchReplayV5 replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            var output = new StringBuilder("volleyball.match-replay.v5\n");
            output.Append(replay.ReplayId).Append('|').Append(replay.ContextHash);
            foreach (var fingerprint in replay.DerivedAttributeFingerprints) output.Append('|').Append(fingerprint);
            return CanonicalJsonHashV4.Sha256(output.ToString());
        }
    }
}

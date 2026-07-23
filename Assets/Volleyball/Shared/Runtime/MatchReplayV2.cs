using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class MatchReplayV2
    {
        [DataMember(Name = "formatVersion", Order = 1)] private int _formatVersion;
        [DataMember(Name = "replayId", Order = 2)] private string _replayId;
        [DataMember(Name = "contextHash", Order = 3)] private string _contextHash;
        [DataMember(Name = "reservedSections", Order = 4)] private string[] _reservedSections;

        private MatchReplayV2(string replayId, string contextHash, IReadOnlyList<string> reservedSections)
        {
            _formatVersion = ContractVersions.ReplayV2;
            _replayId = ContractGuard.RequiredId(replayId, nameof(replayId));
            _contextHash = contextHash;
            _reservedSections = CopySections(reservedSections);
            Validate();
        }

        public int FormatVersion => _formatVersion;
        public string ReplayId => _replayId;
        public string ContextHash => _contextHash;
        public IReadOnlyList<string> ReservedSections => new ReadOnlyCollection<string>(_reservedSections);

        public static MatchReplayV2 Create(string replayId, string contextHash, IReadOnlyList<string> reservedSections)
        {
            return new MatchReplayV2(replayId, contextHash, reservedSections);
        }

        internal void Validate()
        {
            if (_formatVersion != ContractVersions.ReplayV2)
                throw new ContractValidationException("Unsupported replay format version: " + _formatVersion + ".");
            ContractGuard.RequiredId(_replayId, nameof(ReplayId));
            ContractGuard.Hash(_contextHash, nameof(ContextHash));
            if (_reservedSections == null) throw new ContractValidationException("reservedSections is required.");
            var sections = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _reservedSections.Length; index++)
            {
                var section = ContractGuard.RequiredId(_reservedSections[index], nameof(ReservedSections));
                if (!sections.Add(section)) throw new ContractValidationException("reservedSections must contain unique values.");
            }
        }

        private static string[] CopySections(IReadOnlyList<string> reservedSections)
        {
            if (reservedSections == null) throw new ArgumentNullException(nameof(reservedSections));
            var copy = new string[reservedSections.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = reservedSections[index];
            return copy;
        }
    }
}

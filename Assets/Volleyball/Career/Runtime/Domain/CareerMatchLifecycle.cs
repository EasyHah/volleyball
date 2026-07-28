using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    public enum CareerMatchLifecycleExecutionMode
    {
        Fixture = 0,
        Direct = 1,
        QuickSimulation = 2
    }

    public enum CareerMatchPriority
    {
        AttackFirst = 0,
        FirstContactSecurity = 1,
        StaminaControl = 2
    }

    public sealed class CareerMatchLifecycleVersions : IEquatable<CareerMatchLifecycleVersions>
    {
        public const int ContractV2 = 2;
        public const int ContractV3 = 3;
        public const int ContractV4 = 4;

        public CareerMatchLifecycleVersions(
            int contractVersion,
            int contentVersion,
            int rulesetVersion,
            int careerRandomAlgorithmVersion,
            int? matchSimulationVersion,
            int? matchRandomAlgorithmVersion)
        {
            if (contractVersion != ContractV2 &&
                contractVersion != ContractV3 &&
                contractVersion != ContractV4)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contractVersion),
                    contractVersion,
                    "The durable envelope recognizes contract versions 2 through 4; production payloads require V4.");
            }

            var hasSimulation = matchSimulationVersion.HasValue;
            var hasRandom = matchRandomAlgorithmVersion.HasValue;
            if (hasSimulation != hasRandom)
            {
                throw new ArgumentException(
                    "Match simulation and random algorithm versions must be both present or both absent.");
            }

            ContractVersion = contractVersion;
            ContentVersion = CareerSaveModelGuard.PositiveVersion(contentVersion, nameof(contentVersion));
            RulesetVersion = CareerSaveModelGuard.PositiveVersion(rulesetVersion, nameof(rulesetVersion));
            CareerRandomAlgorithmVersion = CareerSaveModelGuard.PositiveVersion(
                careerRandomAlgorithmVersion,
                nameof(careerRandomAlgorithmVersion));
            MatchSimulationVersion = ValidateNullableVersion(
                matchSimulationVersion,
                nameof(matchSimulationVersion));
            MatchRandomAlgorithmVersion = ValidateNullableVersion(
                matchRandomAlgorithmVersion,
                nameof(matchRandomAlgorithmVersion));
        }

        public int ContractVersion { get; }
        public int ContentVersion { get; }
        public int RulesetVersion { get; }
        public int CareerRandomAlgorithmVersion { get; }
        public int? MatchSimulationVersion { get; }
        public int? MatchRandomAlgorithmVersion { get; }

        public bool Equals(CareerMatchLifecycleVersions other)
        {
            return other != null &&
                   ContractVersion == other.ContractVersion &&
                   ContentVersion == other.ContentVersion &&
                   RulesetVersion == other.RulesetVersion &&
                   CareerRandomAlgorithmVersion == other.CareerRandomAlgorithmVersion &&
                   MatchSimulationVersion == other.MatchSimulationVersion &&
                   MatchRandomAlgorithmVersion == other.MatchRandomAlgorithmVersion;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CareerMatchLifecycleVersions);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ContractVersion;
                hash = (hash * 397) ^ ContentVersion;
                hash = (hash * 397) ^ RulesetVersion;
                hash = (hash * 397) ^ CareerRandomAlgorithmVersion;
                hash = (hash * 397) ^ MatchSimulationVersion.GetHashCode();
                return (hash * 397) ^ MatchRandomAlgorithmVersion.GetHashCode();
            }
        }

        internal CareerMatchLifecycleVersions Copy()
        {
            return new CareerMatchLifecycleVersions(
                ContractVersion,
                ContentVersion,
                RulesetVersion,
                CareerRandomAlgorithmVersion,
                MatchSimulationVersion,
                MatchRandomAlgorithmVersion);
        }

        private static int? ValidateNullableVersion(int? value, string parameterName)
        {
            return value.HasValue
                ? CareerSaveModelGuard.PositiveVersion(value.Value, parameterName)
                : (int?)null;
        }
    }

    public sealed class FrozenCareerTrainingEmphasis : IEquatable<FrozenCareerTrainingEmphasis>
    {
        private readonly SlotActionId[] _sourceSlotActionIds;
        private readonly ReadOnlyCollection<SlotActionId> _readOnlySourceSlotActionIds;

        public FrozenCareerTrainingEmphasis(
            CareerTrainingDirection direction,
            IEnumerable<SlotActionId> sourceSlotActionIds,
            int totalBonusBasisPoints)
        {
            CareerSaveModelGuard.DefinedEnum(direction, nameof(direction));
            if (sourceSlotActionIds == null)
            {
                throw new ArgumentNullException(nameof(sourceSlotActionIds));
            }

            var copied = new List<SlotActionId>(2);
            var unique = new HashSet<SlotActionId>();
            foreach (var source in sourceSlotActionIds)
            {
                CareerSaveModelGuard.StableId(source.Value, nameof(sourceSlotActionIds));
                if (!unique.Add(source) || copied.Count == 2)
                {
                    throw new ArgumentException(
                        "A frozen emphasis requires one or two unique ordered sources.",
                        nameof(sourceSlotActionIds));
                }

                copied.Add(source);
            }

            var expectedBonus = copied.Count == 1 ? 1000 : copied.Count == 2 ? 1500 : 0;
            if (totalBonusBasisPoints != expectedBonus)
            {
                throw new ArgumentException(
                    "One frozen emphasis source grants 1000 bp and two grant 1500 bp.",
                    nameof(totalBonusBasisPoints));
            }

            Direction = direction;
            TotalBonusBasisPoints = totalBonusBasisPoints;
            _sourceSlotActionIds = copied.ToArray();
            _readOnlySourceSlotActionIds = Array.AsReadOnly(_sourceSlotActionIds);
        }

        public CareerTrainingDirection Direction { get; }
        public IReadOnlyList<SlotActionId> SourceSlotActionIds => _readOnlySourceSlotActionIds;
        public int TotalBonusBasisPoints { get; }

        public bool Equals(FrozenCareerTrainingEmphasis other)
        {
            if (other == null || Direction != other.Direction ||
                TotalBonusBasisPoints != other.TotalBonusBasisPoints ||
                _sourceSlotActionIds.Length != other._sourceSlotActionIds.Length)
            {
                return false;
            }

            for (var index = 0; index < _sourceSlotActionIds.Length; index++)
            {
                if (!_sourceSlotActionIds[index].Equals(other._sourceSlotActionIds[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FrozenCareerTrainingEmphasis);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((int)Direction * 397) ^ TotalBonusBasisPoints;
                for (var index = 0; index < _sourceSlotActionIds.Length; index++)
                {
                    hash = (hash * 397) ^ _sourceSlotActionIds[index].GetHashCode();
                }

                return hash;
            }
        }

        internal FrozenCareerTrainingEmphasis Copy()
        {
            return new FrozenCareerTrainingEmphasis(
                Direction,
                _sourceSlotActionIds,
                TotalBonusBasisPoints);
        }
    }

    public sealed class PendingCareerMatch
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly byte[] _canonicalContextUtf8;
        private readonly PlayerId[] _orderedPlayerIds;
        private readonly ReadOnlyCollection<PlayerId> _readOnlyOrderedPlayerIds;
        private readonly FrozenCareerTrainingEmphasis[] _frozenTrainingEmphases;
        private readonly ReadOnlyCollection<FrozenCareerTrainingEmphasis> _readOnlyFrozenTrainingEmphases;

        public PendingCareerMatch(
            Guid sessionId,
            OperationId creationOperationId,
            LineageId createdLineageId,
            long createdRevision,
            CareerMatchLifecycleVersions versions,
            CareerMatchLifecycleExecutionMode executionMode,
            string fixtureId,
            int? fixtureVersion,
            uint matchSeed,
            string competitionId,
            string scheduleItemId,
            WeekPlanId sourceWeekPlanId,
            SlotActionId sourceSlotActionId,
            OccurrenceId sourceActionOccurrenceId,
            CareerMatchPriority preMatchPriority,
            Sha256Digest contextDigest,
            byte[] canonicalContextUtf8,
            TeamId homeTeamId,
            TeamId awayTeamId,
            IEnumerable<PlayerId> orderedPlayerIds,
            PlayerId protagonistPlayerId,
            IEnumerable<FrozenCareerTrainingEmphasis> frozenTrainingEmphases)
        {
            CareerSaveModelGuard.StableId(sessionId, nameof(sessionId));
            CareerSaveModelGuard.StableId(creationOperationId.Value, nameof(creationOperationId));
            CareerSaveModelGuard.StableId(createdLineageId.Value, nameof(createdLineageId));
            CareerSaveModelGuard.PositiveRevision(createdRevision, nameof(createdRevision));
            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            CareerSaveModelGuard.DefinedEnum(executionMode, nameof(executionMode));
            CareerSaveModelGuard.DefinedEnum(preMatchPriority, nameof(preMatchPriority));
            ValidateMode(executionMode, fixtureId, fixtureVersion, versions);
            CareerSaveModelGuard.BusinessId(competitionId, nameof(competitionId));
            CareerSaveModelGuard.BusinessId(scheduleItemId, nameof(scheduleItemId));
            CareerSaveModelGuard.StableId(sourceWeekPlanId.Value, nameof(sourceWeekPlanId));
            CareerSaveModelGuard.StableId(sourceSlotActionId.Value, nameof(sourceSlotActionId));
            CareerSaveModelGuard.StableId(sourceActionOccurrenceId.Value, nameof(sourceActionOccurrenceId));
            RequireDigest(contextDigest, nameof(contextDigest));
            _canonicalContextUtf8 = CopyUtf8(canonicalContextUtf8, nameof(canonicalContextUtf8));
            ValidateTeamId(homeTeamId, nameof(homeTeamId));
            ValidateTeamId(awayTeamId, nameof(awayTeamId));
            if (homeTeamId.Equals(awayTeamId))
            {
                throw new ArgumentException("Home and away team IDs must differ.", nameof(awayTeamId));
            }

            _orderedPlayerIds = CopyRoster(orderedPlayerIds, protagonistPlayerId);
            _readOnlyOrderedPlayerIds = Array.AsReadOnly(_orderedPlayerIds);
            _frozenTrainingEmphases = CopyEmphases(frozenTrainingEmphases);
            _readOnlyFrozenTrainingEmphases = Array.AsReadOnly(_frozenTrainingEmphases);

            SessionId = sessionId;
            CreationOperationId = creationOperationId;
            CreatedLineageId = createdLineageId;
            CreatedRevision = createdRevision;
            Versions = versions.Copy();
            ExecutionMode = executionMode;
            FixtureId = fixtureId;
            FixtureVersion = fixtureVersion;
            MatchSeed = matchSeed;
            CompetitionId = competitionId;
            ScheduleItemId = scheduleItemId;
            SourceWeekPlanId = sourceWeekPlanId;
            SourceSlotActionId = sourceSlotActionId;
            SourceActionOccurrenceId = sourceActionOccurrenceId;
            PreMatchPriority = preMatchPriority;
            ContextDigest = contextDigest;
            HomeTeamId = homeTeamId;
            AwayTeamId = awayTeamId;
            ProtagonistPlayerId = protagonistPlayerId;
        }

        public Guid SessionId { get; }
        public OperationId CreationOperationId { get; }
        public LineageId CreatedLineageId { get; }
        public long CreatedRevision { get; }
        public CareerMatchLifecycleVersions Versions { get; }
        public CareerMatchLifecycleExecutionMode ExecutionMode { get; }
        public string FixtureId { get; }
        public int? FixtureVersion { get; }
        public uint MatchSeed { get; }
        public string CompetitionId { get; }
        public string ScheduleItemId { get; }
        public WeekPlanId SourceWeekPlanId { get; }
        public SlotActionId SourceSlotActionId { get; }
        public OccurrenceId SourceActionOccurrenceId { get; }
        public CareerMatchPriority PreMatchPriority { get; }
        public Sha256Digest ContextDigest { get; }
        public byte[] CanonicalContextUtf8 => (byte[])_canonicalContextUtf8.Clone();
        public TeamId HomeTeamId { get; }
        public TeamId AwayTeamId { get; }
        public IReadOnlyList<PlayerId> OrderedPlayerIds => _readOnlyOrderedPlayerIds;
        public PlayerId ProtagonistPlayerId { get; }
        public IReadOnlyList<FrozenCareerTrainingEmphasis> FrozenTrainingEmphases =>
            _readOnlyFrozenTrainingEmphases;

        internal PendingCareerMatch Copy()
        {
            return new PendingCareerMatch(
                SessionId,
                CreationOperationId,
                CreatedLineageId,
                CreatedRevision,
                Versions,
                ExecutionMode,
                FixtureId,
                FixtureVersion,
                MatchSeed,
                CompetitionId,
                ScheduleItemId,
                SourceWeekPlanId,
                SourceSlotActionId,
                SourceActionOccurrenceId,
                PreMatchPriority,
                ContextDigest,
                _canonicalContextUtf8,
                HomeTeamId,
                AwayTeamId,
                _orderedPlayerIds,
                ProtagonistPlayerId,
                _frozenTrainingEmphases);
        }

        private static void ValidateMode(
            CareerMatchLifecycleExecutionMode mode,
            string fixtureId,
            int? fixtureVersion,
            CareerMatchLifecycleVersions versions)
        {
            var hasFixture = fixtureId != null || fixtureVersion.HasValue;
            var hasAlgorithms = versions.MatchSimulationVersion.HasValue ||
                                versions.MatchRandomAlgorithmVersion.HasValue;
            if (mode == CareerMatchLifecycleExecutionMode.Fixture)
            {
                CareerSaveModelGuard.BusinessId(fixtureId, nameof(fixtureId));
                CareerSaveModelGuard.PositiveVersion(fixtureVersion ?? 0, nameof(fixtureVersion));
                if (hasAlgorithms)
                {
                    throw new ArgumentException("Fixture mode cannot set Match algorithm versions.");
                }
            }
            else if (mode == CareerMatchLifecycleExecutionMode.Direct)
            {
                if (hasFixture || hasAlgorithms)
                {
                    throw new ArgumentException(
                        "Direct mode requires null fixture and Match algorithm versions.");
                }
            }
            else if (hasFixture || !hasAlgorithms)
            {
                throw new ArgumentException(
                    "Quick simulation requires both Match algorithm versions and no fixture.");
            }
        }

        internal static byte[] CopyUtf8(byte[] value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length == 0)
            {
                throw new ArgumentException("Canonical UTF-8 bytes cannot be empty.", parameterName);
            }

            try
            {
                StrictUtf8.GetString(value);
            }
            catch (DecoderFallbackException exception)
            {
                throw new ArgumentException("Canonical bytes must be valid UTF-8.", parameterName, exception);
            }

            return (byte[])value.Clone();
        }

        internal static void RequireDigest(Sha256Digest digest, string parameterName)
        {
            if (string.IsNullOrEmpty(digest.Value))
            {
                throw new ArgumentException("A SHA-256 digest is required.", parameterName);
            }
        }

        private static void ValidateTeamId(TeamId value, string parameterName)
        {
            CareerSaveModelGuard.BusinessId(value.Value, parameterName);
        }

        private static PlayerId[] CopyRoster(
            IEnumerable<PlayerId> orderedPlayerIds,
            PlayerId protagonistPlayerId)
        {
            if (orderedPlayerIds == null)
            {
                throw new ArgumentNullException(nameof(orderedPlayerIds));
            }

            CareerSaveModelGuard.BusinessId(protagonistPlayerId.Value, nameof(protagonistPlayerId));
            var copied = new List<PlayerId>(12);
            var unique = new HashSet<PlayerId>();
            var protagonistCount = 0;
            foreach (var playerId in orderedPlayerIds)
            {
                CareerSaveModelGuard.BusinessId(playerId.Value, nameof(orderedPlayerIds));
                if (!unique.Add(playerId))
                {
                    throw new ArgumentException("The twelve ordered player IDs must be unique.", nameof(orderedPlayerIds));
                }

                copied.Add(playerId);
                if (playerId.Equals(protagonistPlayerId))
                {
                    protagonistCount++;
                }
            }

            if (copied.Count != 12)
            {
                throw new ArgumentException(
                    "A 6v6 pending match requires exactly twelve ordered player IDs.",
                    nameof(orderedPlayerIds));
            }

            if (protagonistCount != 1)
            {
                throw new ArgumentException(
                    "The protagonist must appear exactly once in the ordered roster.",
                    nameof(protagonistPlayerId));
            }

            return copied.ToArray();
        }

        private static FrozenCareerTrainingEmphasis[] CopyEmphases(
            IEnumerable<FrozenCareerTrainingEmphasis> emphases)
        {
            if (emphases == null)
            {
                throw new ArgumentNullException(nameof(emphases));
            }

            var byDirection = new SortedDictionary<CareerTrainingDirection, FrozenCareerTrainingEmphasis>();
            foreach (var emphasis in emphases)
            {
                if (emphasis == null || byDirection.ContainsKey(emphasis.Direction))
                {
                    throw new ArgumentException(
                        "Frozen emphasis directions must be non-null and unique.",
                        nameof(emphases));
                }

                byDirection.Add(emphasis.Direction, emphasis.Copy());
            }

            var copied = new List<FrozenCareerTrainingEmphasis>(byDirection.Count);
            foreach (var pair in byDirection)
            {
                copied.Add(pair.Value);
            }

            return copied.ToArray();
        }
    }
}

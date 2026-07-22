using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.Contracts.V2
{
    public static class MatchV2Versions
    {
        public const int ContractV2 = 2;

        public static bool SupportsContract(int version) => version == ContractV2;
    }

    public enum MatchExecutionModeV2 { Fixture, Direct, QuickSimulation }
    public enum PreMatchPriorityV2 { AttackFirst, FirstContactSecurity, StaminaControl }
    public enum TeamSideV2 { Home, Away }
    public enum PlayerPositionV2 { Setter, OutsideHitter, MiddleBlocker, Opposite, Libero }
    public enum MatchStatusV2 { Completed, Abandoned }

    public sealed class MatchV2ContractException : Exception
    {
        public MatchV2ContractException(string message) : base(message) { }
        public MatchV2ContractException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class MatchVersionSetV2 : IEquatable<MatchVersionSetV2>
    {
        public MatchVersionSetV2(
            int contractVersion,
            int contentVersion,
            int rulesetVersion,
            int careerRandomAlgorithmVersion,
            int? matchSimulationVersion,
            int? matchRandomAlgorithmVersion)
        {
            if (!MatchV2Versions.SupportsContract(contractVersion))
                throw new MatchV2ContractException("contractVersion must be 2.");
            MatchV2Guard.Positive(contentVersion, nameof(contentVersion));
            MatchV2Guard.Positive(rulesetVersion, nameof(rulesetVersion));
            MatchV2Guard.Positive(careerRandomAlgorithmVersion, nameof(careerRandomAlgorithmVersion));
            MatchV2Guard.NullablePositive(matchSimulationVersion, nameof(matchSimulationVersion));
            MatchV2Guard.NullablePositive(matchRandomAlgorithmVersion, nameof(matchRandomAlgorithmVersion));
            ContractVersion = contractVersion;
            ContentVersion = contentVersion;
            RulesetVersion = rulesetVersion;
            CareerRandomAlgorithmVersion = careerRandomAlgorithmVersion;
            MatchSimulationVersion = matchSimulationVersion;
            MatchRandomAlgorithmVersion = matchRandomAlgorithmVersion;
        }

        public int ContractVersion { get; }
        public int ContentVersion { get; }
        public int RulesetVersion { get; }
        public int CareerRandomAlgorithmVersion { get; }
        public int? MatchSimulationVersion { get; }
        public int? MatchRandomAlgorithmVersion { get; }

        public bool Equals(MatchVersionSetV2 other) => other != null &&
            ContractVersion == other.ContractVersion && ContentVersion == other.ContentVersion &&
            RulesetVersion == other.RulesetVersion &&
            CareerRandomAlgorithmVersion == other.CareerRandomAlgorithmVersion &&
            MatchSimulationVersion == other.MatchSimulationVersion &&
            MatchRandomAlgorithmVersion == other.MatchRandomAlgorithmVersion;

        public override bool Equals(object obj) => Equals(obj as MatchVersionSetV2);
        public override int GetHashCode() => ContractVersion;
    }

    public sealed class MatchFormatV2
    {
        public MatchFormatV2(
            string kind,
            int teamSize,
            int setsToWin,
            int setTargetPoints,
            int minimumLeadPoints)
        {
            if (!string.Equals(kind, "indoor_6v6", StringComparison.Ordinal) || teamSize != 6)
                throw new MatchV2ContractException("V2 requires indoor_6v6 with teamSize 6.");
            MatchV2Guard.Positive(setsToWin, nameof(setsToWin));
            MatchV2Guard.Positive(setTargetPoints, nameof(setTargetPoints));
            MatchV2Guard.Positive(minimumLeadPoints, nameof(minimumLeadPoints));
            Kind = kind;
            TeamSize = teamSize;
            SetsToWin = setsToWin;
            SetTargetPoints = setTargetPoints;
            MinimumLeadPoints = minimumLeadPoints;
        }

        public string Kind { get; }
        public int TeamSize { get; }
        public int SetsToWin { get; }
        public int SetTargetPoints { get; }
        public int MinimumLeadPoints { get; }
    }

    public sealed class MatchAbilitySnapshotV2
    {
        public MatchAbilitySnapshotV2(
            int spikeBasisPoints, int serveBasisPoints, int receptionBasisPoints,
            int defenseBasisPoints, int blockBasisPoints, int movementBasisPoints,
            int jumpBasisPoints, int staminaBasisPoints)
        {
            SpikeBasisPoints = MatchV2Guard.BasisPoints(spikeBasisPoints, nameof(spikeBasisPoints));
            ServeBasisPoints = MatchV2Guard.BasisPoints(serveBasisPoints, nameof(serveBasisPoints));
            ReceptionBasisPoints = MatchV2Guard.BasisPoints(receptionBasisPoints, nameof(receptionBasisPoints));
            DefenseBasisPoints = MatchV2Guard.BasisPoints(defenseBasisPoints, nameof(defenseBasisPoints));
            BlockBasisPoints = MatchV2Guard.BasisPoints(blockBasisPoints, nameof(blockBasisPoints));
            MovementBasisPoints = MatchV2Guard.BasisPoints(movementBasisPoints, nameof(movementBasisPoints));
            JumpBasisPoints = MatchV2Guard.BasisPoints(jumpBasisPoints, nameof(jumpBasisPoints));
            StaminaBasisPoints = MatchV2Guard.BasisPoints(staminaBasisPoints, nameof(staminaBasisPoints));
        }

        public int SpikeBasisPoints { get; }
        public int ServeBasisPoints { get; }
        public int ReceptionBasisPoints { get; }
        public int DefenseBasisPoints { get; }
        public int BlockBasisPoints { get; }
        public int MovementBasisPoints { get; }
        public int JumpBasisPoints { get; }
        public int StaminaBasisPoints { get; }
        public IReadOnlyList<int> Values => Array.AsReadOnly(new[] {
            SpikeBasisPoints, ServeBasisPoints, ReceptionBasisPoints, DefenseBasisPoints,
            BlockBasisPoints, MovementBasisPoints, JumpBasisPoints, StaminaBasisPoints });
    }

    public sealed class MatchPlayerSnapshotV2
    {
        public MatchPlayerSnapshotV2(
            PlayerId playerId, int jerseyNumber, PlayerPositionV2 position,
            int rotationSlot, int fitnessBasisPoints, MatchAbilitySnapshotV2 abilities)
        {
            MatchV2Guard.StableId(playerId.Value, nameof(playerId));
            if (jerseyNumber < 0 || jerseyNumber > 99)
                throw new MatchV2ContractException("jerseyNumber must be in [0,99].");
            MatchV2Guard.Enum(position, nameof(position));
            if (rotationSlot < 1 || rotationSlot > 6)
                throw new MatchV2ContractException("rotationSlot must be in [1,6].");
            PlayerId = playerId;
            JerseyNumber = jerseyNumber;
            Position = position;
            RotationSlot = rotationSlot;
            FitnessBasisPoints = MatchV2Guard.BasisPoints(fitnessBasisPoints, nameof(fitnessBasisPoints));
            Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        }

        public PlayerId PlayerId { get; }
        public int JerseyNumber { get; }
        public PlayerPositionV2 Position { get; }
        public int RotationSlot { get; }
        public int FitnessBasisPoints { get; }
        public MatchAbilitySnapshotV2 Abilities { get; }
    }

    public sealed class MatchTeamSnapshotV2
    {
        private readonly MatchPlayerSnapshotV2[] _players;

        public MatchTeamSnapshotV2(
            TeamId teamId, TeamSideV2 side, IReadOnlyList<MatchPlayerSnapshotV2> players)
        {
            MatchV2Guard.StableId(teamId.Value, nameof(teamId));
            MatchV2Guard.Enum(side, nameof(side));
            TeamId = teamId;
            Side = side;
            _players = MatchV2Guard.Copy(players, nameof(players));
            ValidatePlayers(_players);
        }

        public TeamId TeamId { get; }
        public TeamSideV2 Side { get; }
        public IReadOnlyList<MatchPlayerSnapshotV2> Players => Array.AsReadOnly(_players);

        private static void ValidatePlayers(IReadOnlyList<MatchPlayerSnapshotV2> players)
        {
            if (players.Count != 6)
                throw new MatchV2ContractException("A V2 team must contain exactly six players.");
            var ids = new HashSet<PlayerId>();
            var slots = new HashSet<int>();
            var positions = new Dictionary<PlayerPositionV2, int>();
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index] ?? throw new MatchV2ContractException("players cannot contain null.");
                if (player.RotationSlot != index + 1 || !slots.Add(player.RotationSlot))
                    throw new MatchV2ContractException("Players must use ascending unique rotation slots 1-6.");
                if (!ids.Add(player.PlayerId))
                    throw new MatchV2ContractException("Player IDs must be unique inside a team.");
                positions.TryGetValue(player.Position, out var count);
                positions[player.Position] = count + 1;
            }

            RequirePosition(positions, PlayerPositionV2.Setter, 1);
            RequirePosition(positions, PlayerPositionV2.OutsideHitter, 2);
            RequirePosition(positions, PlayerPositionV2.MiddleBlocker, 1);
            RequirePosition(positions, PlayerPositionV2.Opposite, 1);
            RequirePosition(positions, PlayerPositionV2.Libero, 1);
        }

        private static void RequirePosition(
            IReadOnlyDictionary<PlayerPositionV2, int> positions,
            PlayerPositionV2 position,
            int expected)
        {
            positions.TryGetValue(position, out var actual);
            if (actual != expected)
                throw new MatchV2ContractException("Each team has an invalid position topology.");
        }
    }

    internal static class MatchV2Guard
    {
        public const long MaximumSafeInteger = 9007199254740991L;

        public static int Positive(int value, string name)
        {
            if (value <= 0) throw new MatchV2ContractException(name + " must be positive.");
            return value;
        }

        public static void NullablePositive(int? value, string name)
        {
            if (value.HasValue) Positive(value.Value, name);
        }

        public static int BasisPoints(int value, string name)
        {
            if (value < 0 || value > 10000)
                throw new MatchV2ContractException(name + " must be in [0,10000].");
            return value;
        }

        public static int NonNegative(int value, string name)
        {
            if (value < 0) throw new MatchV2ContractException(name + " must be non-negative.");
            return value;
        }

        public static long NonNegative(long value, string name)
        {
            if (value < 0 || value > MaximumSafeInteger)
                throw new MatchV2ContractException(name + " must be a non-negative I-JSON-safe integer.");
            return value;
        }

        public static string StableId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
                throw new MatchV2ContractException(name + " must contain 1 to 64 characters.");
            foreach (var character in value)
            {
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_' ||
                      character == '.' || character == ':'))
                    throw new MatchV2ContractException(name + " contains an unsupported character.");
            }
            return value;
        }

        public static string Hash(string value, string name)
        {
            if (value == null || value.Length != 64)
                throw new MatchV2ContractException(name + " must be a lowercase SHA-256 value.");
            foreach (var character in value)
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                    throw new MatchV2ContractException(name + " must be a lowercase SHA-256 value.");
            return value;
        }

        public static void Enum<T>(T value, string name) where T : struct
        {
            if (!System.Enum.IsDefined(typeof(T), value))
                throw new MatchV2ContractException(name + " has an unsupported value.");
        }

        public static T[] Copy<T>(IReadOnlyList<T> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new T[values.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = values[index];
            return copy;
        }
    }
}

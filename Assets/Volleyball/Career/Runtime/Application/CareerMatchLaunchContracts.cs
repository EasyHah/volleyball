using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public enum CareerMatchExecutionMode
    {
        Fixture = 0,
        Direct = 1,
        QuickSimulation = 2
    }

    public enum CareerPreMatchPriority
    {
        AttackFirst = 0,
        FirstContactSecurity = 1,
        StaminaControl = 2
    }

    public enum CareerMatchTeamSide
    {
        Home = 0,
        Away = 1
    }

    public enum CareerMatchPlayerPosition
    {
        Setter = 0,
        OutsideHitter = 1,
        MiddleBlocker = 2,
        Opposite = 3,
        Libero = 4
    }

    public enum CareerMatchResultStatus
    {
        Completed = 0,
        Abandoned = 1
    }

    public sealed class CareerMatchVersions : IEquatable<CareerMatchVersions>
    {
        public const int ContractV3 = 3;

        public CareerMatchVersions(
            int contractVersion,
            int contentVersion,
            int rulesetVersion,
            int careerRandomAlgorithmVersion,
            int? matchSimulationVersion,
            int? matchRandomAlgorithmVersion)
        {
            if (contractVersion != ContractV3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contractVersion), contractVersion, "The match contract version must be 3.");
            }

            ContractVersion = contractVersion;
            ContentVersion = CareerMatchGuard.Positive(contentVersion, nameof(contentVersion));
            RulesetVersion = CareerMatchGuard.Positive(rulesetVersion, nameof(rulesetVersion));
            CareerRandomAlgorithmVersion = CareerMatchGuard.Positive(
                careerRandomAlgorithmVersion, nameof(careerRandomAlgorithmVersion));
            MatchSimulationVersion = CareerMatchGuard.NullablePositive(
                matchSimulationVersion, nameof(matchSimulationVersion));
            MatchRandomAlgorithmVersion = CareerMatchGuard.NullablePositive(
                matchRandomAlgorithmVersion, nameof(matchRandomAlgorithmVersion));
        }

        public int ContractVersion { get; }

        public int ContentVersion { get; }

        public int RulesetVersion { get; }

        public int CareerRandomAlgorithmVersion { get; }

        public int? MatchSimulationVersion { get; }

        public int? MatchRandomAlgorithmVersion { get; }

        public bool Equals(CareerMatchVersions other)
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
            return Equals(obj as CareerMatchVersions);
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
    }

    public sealed class CareerMatchFormat
    {
        public CareerMatchFormat(
            string kind,
            int teamSize,
            int setsToWin,
            int setTargetPoints,
            int minimumLeadPoints)
        {
            if (!string.Equals(kind, "indoor_6v6", StringComparison.Ordinal) || teamSize != 6)
            {
                throw new ArgumentException(
                    "Career match V3 requires the indoor_6v6 format and a team size of six.",
                    nameof(kind));
            }

            Kind = kind;
            TeamSize = teamSize;
            SetsToWin = CareerMatchGuard.Positive(setsToWin, nameof(setsToWin));
            SetTargetPoints = CareerMatchGuard.Positive(setTargetPoints, nameof(setTargetPoints));
            MinimumLeadPoints = CareerMatchGuard.Positive(minimumLeadPoints, nameof(minimumLeadPoints));
        }

        public string Kind { get; }

        public int TeamSize { get; }

        public int SetsToWin { get; }

        public int SetTargetPoints { get; }

        public int MinimumLeadPoints { get; }
    }

    public sealed class CareerMatchPlayerLaunch
    {
        public CareerMatchPlayerLaunch(
            PlayerId playerId,
            int jerseyNumber,
            CareerMatchPlayerPosition position,
            int rotationSlot,
            int fatigue,
            CareerPlayerAttributes attributes)
        {
            CareerMatchGuard.StableId(playerId.Value, nameof(playerId));
            if (jerseyNumber < 1 || jerseyNumber > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(jerseyNumber), jerseyNumber, "Jersey number must be in [1, 99].");
            }

            CareerMatchGuard.Enum(position, nameof(position));
            if (rotationSlot < 1 || rotationSlot > 6)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rotationSlot), rotationSlot, "Rotation slot must be in [1, 6].");
            }

            if (fatigue < 0 || fatigue > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fatigue), fatigue, "Fatigue must be in [0, 100].");
            }

            PlayerId = playerId;
            JerseyNumber = jerseyNumber;
            Position = position;
            RotationSlot = rotationSlot;
            Fatigue = fatigue;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        public PlayerId PlayerId { get; }

        public int JerseyNumber { get; }

        public CareerMatchPlayerPosition Position { get; }

        public int RotationSlot { get; }

        public int Fatigue { get; }

        public CareerPlayerAttributes Attributes { get; }
    }

    public sealed class CareerMatchTeamLaunch
    {
        private readonly CareerMatchPlayerLaunch[] _players;

        public CareerMatchTeamLaunch(
            TeamId teamId,
            CareerMatchTeamSide side,
            IReadOnlyList<CareerMatchPlayerLaunch> players)
        {
            CareerMatchGuard.StableId(teamId.Value, nameof(teamId));
            CareerMatchGuard.Enum(side, nameof(side));
            TeamId = teamId;
            Side = side;
            _players = CareerMatchGuard.Copy(players, nameof(players));
            ValidatePlayers(_players);
        }

        public TeamId TeamId { get; }

        public CareerMatchTeamSide Side { get; }

        public IReadOnlyList<CareerMatchPlayerLaunch> Players => Array.AsReadOnly(_players);

        private static void ValidatePlayers(IReadOnlyList<CareerMatchPlayerLaunch> players)
        {
            if (players.Count != 6)
            {
                throw new ArgumentException("A Career 6v6 team must contain exactly six players.", nameof(players));
            }

            var ids = new HashSet<PlayerId>();
            var jerseys = new HashSet<int>();
            var slots = new HashSet<int>();
            var positions = new Dictionary<CareerMatchPlayerPosition, int>();
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index] ??
                             throw new ArgumentException("Players cannot contain null.", nameof(players));
                if (player.RotationSlot != index + 1 || !slots.Add(player.RotationSlot))
                {
                    throw new ArgumentException(
                        "Players must use ascending unique rotation slots 1 through 6.", nameof(players));
                }

                if (!ids.Add(player.PlayerId))
                {
                    throw new ArgumentException("Player IDs must be unique inside a team.", nameof(players));
                }

                if (!jerseys.Add(player.JerseyNumber))
                {
                    throw new ArgumentException("Jersey numbers must be unique inside a team.", nameof(players));
                }

                positions.TryGetValue(player.Position, out var count);
                positions[player.Position] = count + 1;
            }

            RequirePosition(positions, CareerMatchPlayerPosition.Setter, 1);
            RequirePosition(positions, CareerMatchPlayerPosition.OutsideHitter, 2);
            RequirePosition(positions, CareerMatchPlayerPosition.MiddleBlocker, 1);
            RequirePosition(positions, CareerMatchPlayerPosition.Opposite, 1);
            RequirePosition(positions, CareerMatchPlayerPosition.Libero, 1);
        }

        private static void RequirePosition(
            IReadOnlyDictionary<CareerMatchPlayerPosition, int> positions,
            CareerMatchPlayerPosition position,
            int expected)
        {
            positions.TryGetValue(position, out var actual);
            if (actual != expected)
            {
                throw new ArgumentException("The team has an invalid 6v6 position topology.", nameof(positions));
            }
        }
    }

    public sealed class CareerMatchLaunch
    {
        private readonly CareerMatchTeamLaunch[] _teams;

        public CareerMatchLaunch(
            CareerMatchVersions versions,
            Guid sessionId,
            CareerMatchExecutionMode executionMode,
            string fixtureId,
            int? fixtureVersion,
            uint matchSeed,
            string competitionId,
            string scheduleItemId,
            int importanceBasisPoints,
            CareerMatchFormat format,
            CareerPreMatchPriority preMatchPriority,
            IReadOnlyList<CareerMatchTeamLaunch> teams)
        {
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("A non-empty match session ID is required.", nameof(sessionId));
            }

            CareerMatchGuard.Enum(executionMode, nameof(executionMode));
            CareerMatchGuard.Enum(preMatchPriority, nameof(preMatchPriority));
            ValidateMode(executionMode, fixtureId, fixtureVersion, versions);
            SessionId = sessionId;
            ExecutionMode = executionMode;
            FixtureId = fixtureId;
            FixtureVersion = fixtureVersion;
            MatchSeed = matchSeed;
            CompetitionId = CareerMatchGuard.StableId(competitionId, nameof(competitionId));
            ScheduleItemId = CareerMatchGuard.StableId(scheduleItemId, nameof(scheduleItemId));
            ImportanceBasisPoints = CareerMatchGuard.BasisPoints(
                importanceBasisPoints, nameof(importanceBasisPoints));
            Format = format ?? throw new ArgumentNullException(nameof(format));
            PreMatchPriority = preMatchPriority;
            _teams = CareerMatchGuard.Copy(teams, nameof(teams));
            ValidateTeams(_teams);
        }

        public CareerMatchVersions Versions { get; }

        public Guid SessionId { get; }

        public CareerMatchExecutionMode ExecutionMode { get; }

        public string FixtureId { get; }

        public int? FixtureVersion { get; }

        public uint MatchSeed { get; }

        public string CompetitionId { get; }

        public string ScheduleItemId { get; }

        public int ImportanceBasisPoints { get; }

        public CareerMatchFormat Format { get; }

        public CareerPreMatchPriority PreMatchPriority { get; }

        public IReadOnlyList<CareerMatchTeamLaunch> Teams => Array.AsReadOnly(_teams);

        private static void ValidateMode(
            CareerMatchExecutionMode mode,
            string fixtureId,
            int? fixtureVersion,
            CareerMatchVersions versions)
        {
            var hasFixture = fixtureId != null || fixtureVersion.HasValue;
            var hasMatchAlgorithms = versions.MatchSimulationVersion.HasValue ||
                                     versions.MatchRandomAlgorithmVersion.HasValue;
            if (mode == CareerMatchExecutionMode.Fixture)
            {
                CareerMatchGuard.StableId(fixtureId, nameof(fixtureId));
                CareerMatchGuard.Positive(fixtureVersion ?? 0, nameof(fixtureVersion));
                if (hasMatchAlgorithms)
                {
                    throw new ArgumentException(
                        "Fixture execution cannot set Match algorithm versions.", nameof(versions));
                }
            }
            else if (mode == CareerMatchExecutionMode.Direct)
            {
                if (hasFixture || hasMatchAlgorithms)
                {
                    throw new ArgumentException(
                        "Direct execution requires null fixture and Match algorithm versions.", nameof(mode));
                }
            }
            else if (hasFixture ||
                     !versions.MatchSimulationVersion.HasValue ||
                     !versions.MatchRandomAlgorithmVersion.HasValue)
            {
                throw new ArgumentException(
                    "Quick simulation requires both Match algorithm versions and no fixture.", nameof(mode));
            }
        }

        private static void ValidateTeams(IReadOnlyList<CareerMatchTeamLaunch> teams)
        {
            if (teams.Count != 2 || teams[0] == null || teams[1] == null)
            {
                throw new ArgumentException("A Career match requires home and away teams.", nameof(teams));
            }

            if (teams[0].Side != CareerMatchTeamSide.Home || teams[1].Side != CareerMatchTeamSide.Away)
            {
                throw new ArgumentException("Teams must be ordered home then away.", nameof(teams));
            }

            if (teams[0].TeamId.Equals(teams[1].TeamId))
            {
                throw new ArgumentException("Team IDs must be unique.", nameof(teams));
            }

            var playerIds = new HashSet<PlayerId>();
            foreach (var team in teams)
            {
                foreach (var player in team.Players)
                {
                    if (!playerIds.Add(player.PlayerId))
                    {
                        throw new ArgumentException(
                            "Player IDs must be globally unique in a match.", nameof(teams));
                    }
                }
            }
        }
    }

    public interface ICareerMatchExecutor
    {
        CareerCanonicalMatchContext Encode(CareerMatchLaunch launch);

        Task<CareerMatchExecutionOutcome> ExecuteAsync(
            CareerCanonicalMatchContext context,
            CancellationToken cancellationToken);

        CareerMatchExecutionOutcome DecodeAndValidate(
            byte[] canonicalContextUtf8,
            byte[] canonicalResultUtf8);
    }

    internal static class CareerMatchGuard
    {
        public const long MaximumSafeInteger = 9007199254740991L;

        public static int Positive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "The value must be positive.");
            }

            return value;
        }

        public static int? NullablePositive(int? value, string parameterName)
        {
            if (value.HasValue)
            {
                Positive(value.Value, parameterName);
            }

            return value;
        }

        public static int NonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "The value must be non-negative.");
            }

            return value;
        }

        public static long NonNegativeSafe(long value, string parameterName)
        {
            if (value < 0 || value > MaximumSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "The value must be a non-negative I-JSON-safe integer.");
            }

            return value;
        }

        public static int BasisPoints(int value, string parameterName)
        {
            if (value < 0 || value > 10000)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, "The value must be in [0, 10000].");
            }

            return value;
        }

        public static string StableId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                throw new ArgumentException(
                    parameterName + " must contain 1 to 64 characters.", parameterName);
            }

            foreach (var character in value)
            {
                if (!(char.IsLetterOrDigit(character) ||
                      character == '-' ||
                      character == '_' ||
                      character == '.' ||
                      character == ':'))
                {
                    throw new ArgumentException(
                        parameterName + " contains an unsupported character.", parameterName);
                }
            }

            return value;
        }

        public static void Digest(Sha256Digest digest, string parameterName)
        {
            if (string.IsNullOrEmpty(digest.Value))
            {
                throw new ArgumentException("A non-empty SHA-256 digest is required.", parameterName);
            }

            _ = new Sha256Digest(digest.Value);
        }

        public static void Enum<T>(T value, string parameterName) where T : struct
        {
            if (!System.Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "The enum value is unsupported.");
            }
        }

        public static T[] Copy<T>(IReadOnlyList<T> values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}

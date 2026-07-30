using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class SpikePerformanceFactsV1
    {
        public SpikePerformanceFactsV1(int attempts, int points, int errors)
        {
            Attempts = attempts;
            Points = points;
            Errors = errors;
            PerformanceReportGuardV1.Partition(
                attempts,
                points,
                errors,
                "spike");
        }

        public int Attempts { get; }
        public int Points { get; }
        public int Errors { get; }
    }

    public sealed class ServePerformanceFactsV1
    {
        public ServePerformanceFactsV1(int attempts, int aces, int errors)
        {
            Attempts = attempts;
            Aces = aces;
            Errors = errors;
            PerformanceReportGuardV1.Partition(
                attempts,
                aces,
                errors,
                "serve");
        }

        public int Attempts { get; }
        public int Aces { get; }
        public int Errors { get; }
    }

    public sealed class ReceptionPerformanceFactsV1
    {
        public ReceptionPerformanceFactsV1(
            int attempts,
            int perfect,
            int positive,
            int neutral,
            int negative,
            int errors)
        {
            Attempts = attempts;
            Perfect = perfect;
            Positive = positive;
            Neutral = neutral;
            Negative = negative;
            Errors = errors;
            PerformanceReportGuardV1.NonNegative(attempts, nameof(attempts));
            PerformanceReportGuardV1.NonNegative(perfect, nameof(perfect));
            PerformanceReportGuardV1.NonNegative(positive, nameof(positive));
            PerformanceReportGuardV1.NonNegative(neutral, nameof(neutral));
            PerformanceReportGuardV1.NonNegative(negative, nameof(negative));
            PerformanceReportGuardV1.NonNegative(errors, nameof(errors));
            var classified =
                (long)perfect + positive + neutral + negative + errors;
            if (classified != attempts)
            {
                throw new ContractValidationException(
                    "Reception quality buckets must exactly equal attempts.");
            }
        }

        public int Attempts { get; }
        public int Perfect { get; }
        public int Positive { get; }
        public int Neutral { get; }
        public int Negative { get; }
        public int Errors { get; }
    }

    public sealed class DefensePerformanceFactsV1
    {
        public DefensePerformanceFactsV1(int attempts, int successes)
        {
            Attempts = attempts;
            Successes = successes;
            PerformanceReportGuardV1.Subset(
                attempts,
                successes,
                "defense successes");
        }

        public int Attempts { get; }
        public int Successes { get; }
    }

    public sealed class BlockPerformanceFactsV1
    {
        public BlockPerformanceFactsV1(
            int attempts,
            int effectiveTouches,
            int points)
        {
            Attempts = attempts;
            EffectiveTouches = effectiveTouches;
            Points = points;
            PerformanceReportGuardV1.Subset(
                attempts,
                effectiveTouches,
                "effective block touches");
            PerformanceReportGuardV1.Subset(
                effectiveTouches,
                points,
                "block points");
        }

        public int Attempts { get; }
        public int EffectiveTouches { get; }
        public int Points { get; }
    }

    public sealed class SettingPerformanceFactsV1
    {
        public SettingPerformanceFactsV1(
            int attempts,
            int successful,
            int errors)
        {
            Attempts = attempts;
            Successful = successful;
            Errors = errors;
            PerformanceReportGuardV1.Partition(
                attempts,
                successful,
                errors,
                "setting");
        }

        public int Attempts { get; }
        public int Successful { get; }
        public int Errors { get; }
    }

    public sealed class MatchLoadFactsV1
    {
        public MatchLoadFactsV1(
            int ralliesOnCourt,
            long activeDurationMilliseconds,
            long movementDistanceMillimeters,
            int jumpCount,
            int highLoadJumpCount,
            int landingLoadBasisPoints,
            int totalWorkloadBasisPoints)
        {
            RalliesOnCourt = ralliesOnCourt;
            ActiveDurationMilliseconds = activeDurationMilliseconds;
            MovementDistanceMillimeters = movementDistanceMillimeters;
            JumpCount = jumpCount;
            HighLoadJumpCount = highLoadJumpCount;
            LandingLoadBasisPoints = landingLoadBasisPoints;
            TotalWorkloadBasisPoints = totalWorkloadBasisPoints;
            PerformanceReportGuardV1.NonNegative(
                ralliesOnCourt,
                nameof(ralliesOnCourt));
            PerformanceReportGuardV1.NonNegative(
                activeDurationMilliseconds,
                nameof(activeDurationMilliseconds));
            PerformanceReportGuardV1.NonNegative(
                movementDistanceMillimeters,
                nameof(movementDistanceMillimeters));
            PerformanceReportGuardV1.Subset(
                jumpCount,
                highLoadJumpCount,
                "high-load jumps");
            PerformanceReportGuardV1.BasisPoints(
                landingLoadBasisPoints,
                nameof(landingLoadBasisPoints));
            PerformanceReportGuardV1.BasisPoints(
                totalWorkloadBasisPoints,
                nameof(totalWorkloadBasisPoints));
        }

        public int RalliesOnCourt { get; }
        public long ActiveDurationMilliseconds { get; }
        public long MovementDistanceMillimeters { get; }
        public int JumpCount { get; }
        public int HighLoadJumpCount { get; }
        public int LandingLoadBasisPoints { get; }
        public int TotalWorkloadBasisPoints { get; }
    }

    public sealed class StabilityPerformanceFactsV1
    {
        public StabilityPerformanceFactsV1(
            int criticalActions,
            int criticalSuccesses,
            int criticalErrors,
            int errorStreakEpisodes,
            int longestErrorStreak)
        {
            CriticalActions = criticalActions;
            CriticalSuccesses = criticalSuccesses;
            CriticalErrors = criticalErrors;
            ErrorStreakEpisodes = errorStreakEpisodes;
            LongestErrorStreak = longestErrorStreak;
            PerformanceReportGuardV1.NonNegative(
                criticalActions,
                nameof(criticalActions));
            PerformanceReportGuardV1.NonNegative(
                criticalSuccesses,
                nameof(criticalSuccesses));
            PerformanceReportGuardV1.NonNegative(
                criticalErrors,
                nameof(criticalErrors));
            PerformanceReportGuardV1.NonNegative(
                errorStreakEpisodes,
                nameof(errorStreakEpisodes));
            PerformanceReportGuardV1.NonNegative(
                longestErrorStreak,
                nameof(longestErrorStreak));
            if ((long)criticalSuccesses + criticalErrors > criticalActions)
            {
                throw new ContractValidationException(
                    "Critical successes and errors must be subsets of critical actions.");
            }

            if ((errorStreakEpisodes == 0 && longestErrorStreak >= 2) ||
                (errorStreakEpisodes > 0 && longestErrorStreak < 2))
            {
                throw new ContractValidationException(
                    "Error streak episodes and longest streak are inconsistent.");
            }
        }

        public int CriticalActions { get; }
        public int CriticalSuccesses { get; }
        public int CriticalErrors { get; }
        public int ErrorStreakEpisodes { get; }
        public int LongestErrorStreak { get; }
    }

    public sealed class PlayerPerformanceReportV1
    {
        public PlayerPerformanceReportV1(
            PlayerId playerId,
            SpikePerformanceFactsV1 spike,
            ServePerformanceFactsV1 serve,
            ReceptionPerformanceFactsV1 reception,
            DefensePerformanceFactsV1 defense,
            BlockPerformanceFactsV1 block,
            SettingPerformanceFactsV1 setting,
            MatchLoadFactsV1 load,
            StabilityPerformanceFactsV1 stability)
        {
            PlayerId = new PlayerId(
                ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            Spike = spike ?? throw new ContractValidationException(
                "spike is required.");
            Serve = serve ?? throw new ContractValidationException(
                "serve is required.");
            Reception = reception ?? throw new ContractValidationException(
                "reception is required.");
            Defense = defense ?? throw new ContractValidationException(
                "defense is required.");
            Block = block ?? throw new ContractValidationException(
                "block is required.");
            Setting = setting ?? throw new ContractValidationException(
                "setting is required.");
            Load = load ?? throw new ContractValidationException(
                "load is required.");
            Stability = stability ?? throw new ContractValidationException(
                "stability is required.");
        }

        public PlayerId PlayerId { get; }
        public SpikePerformanceFactsV1 Spike { get; }
        public ServePerformanceFactsV1 Serve { get; }
        public ReceptionPerformanceFactsV1 Reception { get; }
        public DefensePerformanceFactsV1 Defense { get; }
        public BlockPerformanceFactsV1 Block { get; }
        public SettingPerformanceFactsV1 Setting { get; }
        public MatchLoadFactsV1 Load { get; }
        public StabilityPerformanceFactsV1 Stability { get; }
    }

    public sealed class MatchPerformanceReportV1
    {
        public const int CurrentContractVersion = 1;
        public const int CurrentAggregationRulesVersion = 1;
        public const int FormalPlayerCount = 12;

        private readonly PlayerPerformanceReportV1[] _playerReports;
        private readonly ReadOnlyCollection<PlayerPerformanceReportV1>
            _readOnlyPlayerReports;

        private MatchPerformanceReportV1(
            Guid sessionId,
            string contextHash,
            string resultHash,
            IReadOnlyList<PlayerPerformanceReportV1> playerReports,
            string suppliedReportHash)
        {
            ContractVersion = CurrentContractVersion;
            AggregationRulesVersion = CurrentAggregationRulesVersion;
            SessionId = sessionId;
            ContextHash = contextHash;
            ResultHash = resultHash;
            _playerReports = CopyAndSort(playerReports);
            _readOnlyPlayerReports =
                new ReadOnlyCollection<PlayerPerformanceReportV1>(_playerReports);
            ValidatePayload();
            ReportHash = CanonicalMatchPerformanceReportJsonV1.ComputeHash(this);
            if (suppliedReportHash != null &&
                !string.Equals(
                    suppliedReportHash,
                    ReportHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "reportHash does not match the canonical performance report.");
            }
        }

        public int ContractVersion { get; }
        public int AggregationRulesVersion { get; }
        public Guid SessionId { get; }
        public string ContextHash { get; }
        public string ResultHash { get; }
        public IReadOnlyList<PlayerPerformanceReportV1> PlayerReports =>
            _readOnlyPlayerReports;
        public string ReportHash { get; }

        public static MatchPerformanceReportV1 Create(
            MatchContextV4 context,
            MatchResultV4 result,
            IReadOnlyList<PlayerPerformanceReportV1> playerReports)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.ValidateAgainst(context);
            var report = new MatchPerformanceReportV1(
                context.SessionId,
                context.ContextHash,
                result.ResultHash,
                playerReports,
                null);
            report.ValidateAgainst(context, result);
            return report;
        }

        internal static MatchPerformanceReportV1 Restore(
            Guid sessionId,
            string contextHash,
            string resultHash,
            IReadOnlyList<PlayerPerformanceReportV1> playerReports,
            string reportHash)
        {
            return new MatchPerformanceReportV1(
                sessionId,
                contextHash,
                resultHash,
                playerReports,
                reportHash);
        }

        public void ValidateAgainst(MatchContextV4 context, MatchResultV4 result)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.ValidateAgainst(context);
            if (SessionId != context.SessionId ||
                !string.Equals(ContextHash, context.ContextHash, StringComparison.Ordinal) ||
                !string.Equals(ResultHash, result.ResultHash, StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The performance report does not belong to the supplied context and result.");
            }

            var expected = new HashSet<PlayerId>();
            AddPlayers(context.Home, expected);
            AddPlayers(context.Away, expected);
            if (expected.Count != FormalPlayerCount ||
                _playerReports.Length != FormalPlayerCount)
            {
                throw new ContractValidationException(
                    "A formal performance report must cover exactly 12 players.");
            }

            foreach (var player in _playerReports)
            {
                if (!expected.Remove(player.PlayerId))
                {
                    throw new ContractValidationException(
                        "The performance report contains a player outside the context.");
                }
            }

            if (expected.Count != 0)
            {
                throw new ContractValidationException(
                    "The performance report omits a player from the context.");
            }
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(ReportHash, nameof(ReportHash));
            if (!string.Equals(
                    CanonicalMatchPerformanceReportJsonV1.ComputeHash(this),
                    ReportHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "reportHash does not match the canonical performance report.");
            }
        }

        private void ValidatePayload()
        {
            if (ContractVersion != CurrentContractVersion ||
                AggregationRulesVersion != CurrentAggregationRulesVersion)
            {
                throw new ContractValidationException(
                    "Unsupported performance report version.");
            }

            if (SessionId == Guid.Empty)
            {
                throw new ContractValidationException(
                    "sessionId must be a non-empty canonical GUID.");
            }

            ContractGuard.Hash(ContextHash, nameof(ContextHash));
            ContractGuard.Hash(ResultHash, nameof(ResultHash));
            if (_playerReports == null ||
                _playerReports.Length != FormalPlayerCount)
            {
                throw new ContractValidationException(
                    "A formal performance report must contain exactly 12 players.");
            }

            var ids = new HashSet<PlayerId>();
            foreach (var player in _playerReports)
            {
                if (player == null || !ids.Add(player.PlayerId))
                {
                    throw new ContractValidationException(
                        "playerReports must contain non-null unique player IDs.");
                }
            }
        }

        private static PlayerPerformanceReportV1[] CopyAndSort(
            IReadOnlyList<PlayerPerformanceReportV1> playerReports)
        {
            if (playerReports == null)
            {
                throw new ContractValidationException(
                    "playerReports is required.");
            }

            var copy = new PlayerPerformanceReportV1[playerReports.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = playerReports[index];
            }

            Array.Sort(
                copy,
                (left, right) =>
                {
                    if (ReferenceEquals(left, right))
                    {
                        return 0;
                    }

                    if (left == null)
                    {
                        return -1;
                    }

                    if (right == null)
                    {
                        return 1;
                    }

                    return string.CompareOrdinal(
                        left.PlayerId.Value,
                        right.PlayerId.Value);
                });
            return copy;
        }

        private static void AddPlayers(
            TeamSnapshotV4 team,
            ISet<PlayerId> playerIds)
        {
            foreach (var player in team.RotationOrder)
            {
                playerIds.Add(player.PlayerId);
            }
        }
    }

    internal static class PerformanceReportGuardV1
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;

        public static void NonNegative(int value, string name)
        {
            ContractGuard.NonNegative(value, name);
        }

        public static void NonNegative(long value, string name)
        {
            if (value < 0 || value > MaximumIJsonSafeInteger)
            {
                throw new ContractValidationException(
                    name + " must be an I-JSON safe non-negative integer.");
            }
        }

        public static void BasisPoints(int value, string name)
        {
            if (value < 0 || value > 10000)
            {
                throw new ContractValidationException(
                    name + " must be in the range [0, 10000].");
            }
        }

        public static void Subset(int total, int subset, string name)
        {
            NonNegative(total, nameof(total));
            NonNegative(subset, name);
            if (subset > total)
            {
                throw new ContractValidationException(
                    name + " cannot exceed its total.");
            }
        }

        public static void Partition(
            int attempts,
            int successes,
            int errors,
            string name)
        {
            NonNegative(attempts, name + " attempts");
            NonNegative(successes, name + " successes");
            NonNegative(errors, name + " errors");
            if ((long)successes + errors > attempts)
            {
                throw new ContractValidationException(
                    name + " successes and errors cannot exceed attempts.");
            }
        }
    }

    internal static class CanonicalMatchPerformanceReportJsonV1
    {
        private const string HashFamily =
            "volleyball.match-performance-report.v1\n";

        public static string Serialize(MatchPerformanceReportV1 report)
        {
            var payload = Payload(report);
            return payload.Substring(0, payload.Length - 1) +
                ",\"reportHash\":" +
                CanonicalMatchJsonV4.Quote(report.ReportHash) +
                "}";
        }

        public static string ComputeHash(MatchPerformanceReportV1 report)
        {
            return CanonicalJsonHashV4.Sha256(HashFamily + Payload(report));
        }

        private static string Payload(MatchPerformanceReportV1 report)
        {
            var output = new StringBuilder(8192);
            output.Append("{\"contractVersion\":")
                .Append(report.ContractVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"aggregationRulesVersion\":")
                .Append(report.AggregationRulesVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"sessionId\":")
                .Append(CanonicalMatchJsonV4.Quote(report.SessionId.ToString("D")));
            output.Append(",\"contextHash\":")
                .Append(CanonicalMatchJsonV4.Quote(report.ContextHash));
            output.Append(",\"resultHash\":")
                .Append(CanonicalMatchJsonV4.Quote(report.ResultHash));
            output.Append(",\"playerReports\":[");
            for (var index = 0; index < report.PlayerReports.Count; index++)
            {
                if (index > 0)
                {
                    output.Append(',');
                }

                AppendPlayer(output, report.PlayerReports[index]);
            }

            output.Append("]}");
            return output.ToString();
        }

        private static void AppendPlayer(
            StringBuilder output,
            PlayerPerformanceReportV1 player)
        {
            output.Append("{\"playerId\":")
                .Append(CanonicalMatchJsonV4.Quote(player.PlayerId.Value));
            output.Append(",\"spike\":");
            AppendTriple(
                output,
                player.Spike.Attempts,
                "points",
                player.Spike.Points,
                player.Spike.Errors);
            output.Append(",\"serve\":");
            AppendTriple(
                output,
                player.Serve.Attempts,
                "aces",
                player.Serve.Aces,
                player.Serve.Errors);
            output.Append(",\"reception\":{\"attempts\":")
                .Append(Number(player.Reception.Attempts))
                .Append(",\"perfect\":").Append(Number(player.Reception.Perfect))
                .Append(",\"positive\":").Append(Number(player.Reception.Positive))
                .Append(",\"neutral\":").Append(Number(player.Reception.Neutral))
                .Append(",\"negative\":").Append(Number(player.Reception.Negative))
                .Append(",\"errors\":").Append(Number(player.Reception.Errors))
                .Append('}');
            output.Append(",\"defense\":{\"attempts\":")
                .Append(Number(player.Defense.Attempts))
                .Append(",\"successes\":").Append(Number(player.Defense.Successes))
                .Append('}');
            output.Append(",\"block\":{\"attempts\":")
                .Append(Number(player.Block.Attempts))
                .Append(",\"effectiveTouches\":")
                .Append(Number(player.Block.EffectiveTouches))
                .Append(",\"points\":").Append(Number(player.Block.Points))
                .Append('}');
            output.Append(",\"setting\":");
            AppendTriple(
                output,
                player.Setting.Attempts,
                "successful",
                player.Setting.Successful,
                player.Setting.Errors);
            output.Append(",\"load\":{\"ralliesOnCourt\":")
                .Append(Number(player.Load.RalliesOnCourt))
                .Append(",\"activeDurationMilliseconds\":")
                .Append(Number(player.Load.ActiveDurationMilliseconds))
                .Append(",\"movementDistanceMillimeters\":")
                .Append(Number(player.Load.MovementDistanceMillimeters))
                .Append(",\"jumpCount\":").Append(Number(player.Load.JumpCount))
                .Append(",\"highLoadJumpCount\":")
                .Append(Number(player.Load.HighLoadJumpCount))
                .Append(",\"landingLoadBasisPoints\":")
                .Append(Number(player.Load.LandingLoadBasisPoints))
                .Append(",\"totalWorkloadBasisPoints\":")
                .Append(Number(player.Load.TotalWorkloadBasisPoints))
                .Append('}');
            output.Append(",\"stability\":{\"criticalActions\":")
                .Append(Number(player.Stability.CriticalActions))
                .Append(",\"criticalSuccesses\":")
                .Append(Number(player.Stability.CriticalSuccesses))
                .Append(",\"criticalErrors\":")
                .Append(Number(player.Stability.CriticalErrors))
                .Append(",\"errorStreakEpisodes\":")
                .Append(Number(player.Stability.ErrorStreakEpisodes))
                .Append(",\"longestErrorStreak\":")
                .Append(Number(player.Stability.LongestErrorStreak))
                .Append("}}");
        }

        private static void AppendTriple(
            StringBuilder output,
            int attempts,
            string successName,
            int successes,
            int errors)
        {
            output.Append("{\"attempts\":").Append(Number(attempts))
                .Append(',').Append(CanonicalMatchJsonV4.Quote(successName))
                .Append(':').Append(Number(successes))
                .Append(",\"errors\":").Append(Number(errors))
                .Append('}');
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal static class PerformanceReportJsonParserV1
    {
        public static MatchPerformanceReportV1 Deserialize(
            StrictJsonObjectV4 root)
        {
            StrictJsonV4.RequireExactProperties(
                root,
                "contractVersion",
                "aggregationRulesVersion",
                "sessionId",
                "contextHash",
                "resultHash",
                "playerReports",
                "reportHash");
            if (StrictJsonV4.RequiredInt(root, "contractVersion") !=
                    MatchPerformanceReportV1.CurrentContractVersion ||
                StrictJsonV4.RequiredInt(root, "aggregationRulesVersion") !=
                    MatchPerformanceReportV1.CurrentAggregationRulesVersion)
            {
                throw new ContractValidationException(
                    "Unsupported performance report version.");
            }

            var values = StrictJsonV4.RequiredArray(root, "playerReports");
            var reports = new PlayerPerformanceReportV1[values.Count];
            for (var index = 0; index < reports.Length; index++)
            {
                reports[index] = ParsePlayer(
                    StrictJsonV4.AsObject(
                        values[index],
                        "playerReports[" + index + "]"));
            }

            return MatchPerformanceReportV1.Restore(
                StrictJsonV4.RequiredGuid(root, "sessionId"),
                StrictJsonV4.RequiredString(root, "contextHash"),
                StrictJsonV4.RequiredString(root, "resultHash"),
                reports,
                StrictJsonV4.RequiredString(root, "reportHash"));
        }

        private static PlayerPerformanceReportV1 ParsePlayer(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "playerId",
                "spike",
                "serve",
                "reception",
                "defense",
                "block",
                "setting",
                "load",
                "stability");
            return new PlayerPerformanceReportV1(
                new PlayerId(StrictJsonV4.RequiredString(value, "playerId")),
                ParseSpike(StrictJsonV4.RequiredObject(value, "spike")),
                ParseServe(StrictJsonV4.RequiredObject(value, "serve")),
                ParseReception(StrictJsonV4.RequiredObject(value, "reception")),
                ParseDefense(StrictJsonV4.RequiredObject(value, "defense")),
                ParseBlock(StrictJsonV4.RequiredObject(value, "block")),
                ParseSetting(StrictJsonV4.RequiredObject(value, "setting")),
                ParseLoad(StrictJsonV4.RequiredObject(value, "load")),
                ParseStability(StrictJsonV4.RequiredObject(value, "stability")));
        }

        private static SpikePerformanceFactsV1 ParseSpike(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "attempts",
                "points",
                "errors");
            return new SpikePerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "attempts"),
                StrictJsonV4.RequiredInt(value, "points"),
                StrictJsonV4.RequiredInt(value, "errors"));
        }

        private static ServePerformanceFactsV1 ParseServe(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "attempts",
                "aces",
                "errors");
            return new ServePerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "attempts"),
                StrictJsonV4.RequiredInt(value, "aces"),
                StrictJsonV4.RequiredInt(value, "errors"));
        }

        private static ReceptionPerformanceFactsV1 ParseReception(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "attempts",
                "perfect",
                "positive",
                "neutral",
                "negative",
                "errors");
            return new ReceptionPerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "attempts"),
                StrictJsonV4.RequiredInt(value, "perfect"),
                StrictJsonV4.RequiredInt(value, "positive"),
                StrictJsonV4.RequiredInt(value, "neutral"),
                StrictJsonV4.RequiredInt(value, "negative"),
                StrictJsonV4.RequiredInt(value, "errors"));
        }

        private static DefensePerformanceFactsV1 ParseDefense(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "attempts",
                "successes");
            return new DefensePerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "attempts"),
                StrictJsonV4.RequiredInt(value, "successes"));
        }

        private static BlockPerformanceFactsV1 ParseBlock(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "attempts",
                "effectiveTouches",
                "points");
            return new BlockPerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "attempts"),
                StrictJsonV4.RequiredInt(value, "effectiveTouches"),
                StrictJsonV4.RequiredInt(value, "points"));
        }

        private static SettingPerformanceFactsV1 ParseSetting(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "attempts",
                "successful",
                "errors");
            return new SettingPerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "attempts"),
                StrictJsonV4.RequiredInt(value, "successful"),
                StrictJsonV4.RequiredInt(value, "errors"));
        }

        private static MatchLoadFactsV1 ParseLoad(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "ralliesOnCourt",
                "activeDurationMilliseconds",
                "movementDistanceMillimeters",
                "jumpCount",
                "highLoadJumpCount",
                "landingLoadBasisPoints",
                "totalWorkloadBasisPoints");
            return new MatchLoadFactsV1(
                StrictJsonV4.RequiredInt(value, "ralliesOnCourt"),
                StrictJsonV4.RequiredLong(
                    value,
                    "activeDurationMilliseconds"),
                StrictJsonV4.RequiredLong(
                    value,
                    "movementDistanceMillimeters"),
                StrictJsonV4.RequiredInt(value, "jumpCount"),
                StrictJsonV4.RequiredInt(value, "highLoadJumpCount"),
                StrictJsonV4.RequiredInt(value, "landingLoadBasisPoints"),
                StrictJsonV4.RequiredInt(value, "totalWorkloadBasisPoints"));
        }

        private static StabilityPerformanceFactsV1 ParseStability(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "criticalActions",
                "criticalSuccesses",
                "criticalErrors",
                "errorStreakEpisodes",
                "longestErrorStreak");
            return new StabilityPerformanceFactsV1(
                StrictJsonV4.RequiredInt(value, "criticalActions"),
                StrictJsonV4.RequiredInt(value, "criticalSuccesses"),
                StrictJsonV4.RequiredInt(value, "criticalErrors"),
                StrictJsonV4.RequiredInt(value, "errorStreakEpisodes"),
                StrictJsonV4.RequiredInt(value, "longestErrorStreak"));
        }
    }
}

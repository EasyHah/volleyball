using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    // Immutable startup input for a formal match. It intentionally contains no
    // mid-rally ball state, forced contact, or expected result.
    public sealed class FormalMatchScenarioDefinitionV4
    {
        public const int FormatVersion = 1;
        public const string FormalIndoorConfigurationIdentity =
            "formal-indoor-6v6-v1";

        public FormalMatchScenarioDefinitionV4(
            string scenarioId,
            int formatVersion,
            MatchContextV4 context,
            TeamSide firstServingSide,
            int homeInitialRotationOffset,
            int awayInitialRotationOffset,
            string configurationIdentity,
            FormalMatchTacticInputV4 homeTactics,
            FormalMatchTacticInputV4 awayTactics,
            FormalMatchAiInputV4 ai,
            string suppliedContentHash = null)
        {
            ScenarioId = RequireText(scenarioId, nameof(scenarioId));
            if (formatVersion != FormatVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(formatVersion));
            }

            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (Context.RulesVersion != RulesVersions.FullRallyV3)
            {
                throw new ArgumentException("Formal scenarios require Full Rally V3.", nameof(context));
            }

            if (!Enum.IsDefined(typeof(TeamSide), firstServingSide))
            {
                throw new ArgumentOutOfRangeException(nameof(firstServingSide));
            }

            ValidateOffset(homeInitialRotationOffset, nameof(homeInitialRotationOffset));
            ValidateOffset(awayInitialRotationOffset, nameof(awayInitialRotationOffset));
            ConfigurationIdentity = RequireText(configurationIdentity,
                nameof(configurationIdentity));
            if (!string.Equals(ConfigurationIdentity,
                    FormalIndoorConfigurationIdentity,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Formal scenarios must target the formal indoor 6v6 configuration.",
                    nameof(configurationIdentity));
            }

            HomeTactics = homeTactics ?? throw new ArgumentNullException(nameof(homeTactics));
            AwayTactics = awayTactics ?? throw new ArgumentNullException(nameof(awayTactics));
            Ai = ai ?? throw new ArgumentNullException(nameof(ai));
            HomeTactics.Validate(nameof(homeTactics));
            AwayTactics.Validate(nameof(awayTactics));
            Ai.Validate(nameof(ai));
            FormatVersionValue = formatVersion;
            FirstServingSide = firstServingSide;
            HomeInitialRotationOffset = homeInitialRotationOffset;
            AwayInitialRotationOffset = awayInitialRotationOffset;
            ContentHash = ComputeContentHash(this);
            if (!string.IsNullOrEmpty(suppliedContentHash) &&
                !string.Equals(suppliedContentHash, ContentHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Scenario content hash does not match its complete startup input.",
                    nameof(suppliedContentHash));
            }
        }

        public string ScenarioId { get; }
        public int FormatVersionValue { get; }
        public MatchContextV4 Context { get; }
        public TeamSide FirstServingSide { get; }
        public int HomeInitialRotationOffset { get; }
        public int AwayInitialRotationOffset { get; }
        public string ConfigurationIdentity { get; }
        public FormalMatchTacticInputV4 HomeTactics { get; }
        public FormalMatchTacticInputV4 AwayTactics { get; }
        public FormalMatchAiInputV4 Ai { get; }
        public string ContentHash { get; }

        public PhysicalRallyTactics CreateTactics() => new PhysicalRallyTactics(
            HomeTactics.ToRuntime(),
            AwayTactics.ToRuntime());

        private static string ComputeContentHash(FormalMatchScenarioDefinitionV4 value)
        {
            var payload = new StringBuilder();
            payload.Append("format=").Append(value.FormatVersionValue)
                .Append("|id=").Append(value.ScenarioId)
                .Append("|context=").Append(ContractJson.SerializeV4(value.Context))
                .Append("|serve=").Append((int)value.FirstServingSide)
                .Append("|homeOffset=").Append(value.HomeInitialRotationOffset)
                .Append("|awayOffset=").Append(value.AwayInitialRotationOffset)
                .Append("|configuration=").Append(value.ConfigurationIdentity)
                .Append("|homeTactics=").Append(value.HomeTactics.CanonicalPayload())
                .Append("|awayTactics=").Append(value.AwayTactics.CanonicalPayload())
                .Append("|ai=").Append(value.Ai.CanonicalPayload());
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
            var hash = new StringBuilder(bytes.Length * 2);
            foreach (var valueByte in bytes)
            {
                hash.Append(valueByte.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hash.ToString();
        }

        private static string RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A scenario value is required.", name);
            }

            return value;
        }

        private static void ValidateOffset(int value, string name)
        {
            if (value < 0 || value >= 6)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    [Serializable]
    public sealed class FormalMatchAiInputV4
    {
        public float RolePreference = 1f;
        public float Reachability = 1.15f;
        public float ApproachDistance = 1f;
        public float DirectionTolerance = 1f;

        internal RallyTacticalWeights ToRuntime()
        {
            Validate(nameof(FormalMatchAiInputV4));
            return new RallyTacticalWeights(
                RolePreference,
                Reachability,
                ApproachDistance,
                DirectionTolerance);
        }

        internal void Validate(string name)
        {
            if (!Finite(RolePreference) || !Finite(Reachability) ||
                !Finite(ApproachDistance) || !Finite(DirectionTolerance) ||
                RolePreference < RallyTacticalWeights.Minimum ||
                RolePreference > RallyTacticalWeights.Maximum ||
                Reachability < RallyTacticalWeights.Minimum ||
                Reachability > RallyTacticalWeights.Maximum ||
                ApproachDistance < RallyTacticalWeights.Minimum ||
                ApproachDistance > RallyTacticalWeights.Maximum ||
                DirectionTolerance < RallyTacticalWeights.Minimum ||
                DirectionTolerance > RallyTacticalWeights.Maximum)
            {
                throw new ArgumentException("Invalid complete AI scenario input.", name);
            }
        }

        internal string CanonicalPayload()
        {
            return string.Join(",", new[]
            {
                Number(RolePreference), Number(Reachability),
                Number(ApproachDistance), Number(DirectionTolerance)
            });
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Number(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }

    [Serializable]
    public sealed class FormalMatchTacticInputV4
    {
        public SetRoute SetRoute = Volleyball.AI.SetRoute.LeftPin;
        public SpikeRoute SpikeRoute = Volleyball.AI.SpikeRoute.CrossCourt;
        public float SetterX;
        public float SetterZ;
        public float AttackerX;
        public float AttackerZ;
        public float DefenderX;
        public float DefenderZ;
        public PlayerRole Blocker = PlayerRole.Attacker;
        public float BlockX;
        public float BlockZ;
        public PlayerRole CoverReceiver = PlayerRole.Setter;
        public float CoverX;
        public float CoverZ;
        public SetRhythm SetRhythm = Volleyball.AI.SetRhythm.FastPin;
        public float AttackFlightSeconds = 0.45f;

        internal TeamRallyTactic ToRuntime()
        {
            Validate(nameof(FormalMatchTacticInputV4));
            return new TeamRallyTactic(
                SetRoute,
                SpikeRoute,
                new CourtPoint(SetterX, SetterZ),
                new CourtPoint(AttackerX, AttackerZ),
                new CourtPoint(DefenderX, DefenderZ),
                new BlockCoveragePlan(
                    Blocker,
                    new CourtPoint(BlockX, BlockZ),
                    CoverReceiver,
                    new CourtPoint(CoverX, CoverZ)),
                SetRhythm,
                AttackFlightSeconds);
        }

        internal void Validate(string name)
        {
            if (!Enum.IsDefined(typeof(SetRoute), SetRoute) ||
                !Enum.IsDefined(typeof(SpikeRoute), SpikeRoute) ||
                !Enum.IsDefined(typeof(PlayerRole), Blocker) ||
                !Enum.IsDefined(typeof(PlayerRole), CoverReceiver) ||
                !Enum.IsDefined(typeof(SetRhythm), SetRhythm) ||
                Blocker == CoverReceiver ||
                !Finite(SetterX) || !Finite(SetterZ) ||
                !Finite(AttackerX) || !Finite(AttackerZ) ||
                !Finite(DefenderX) || !Finite(DefenderZ) ||
                !Finite(BlockX) || !Finite(BlockZ) ||
                !Finite(CoverX) || !Finite(CoverZ) ||
                !Finite(AttackFlightSeconds) || AttackFlightSeconds <= 0f)
            {
                throw new ArgumentException("Invalid complete tactical scenario input.", name);
            }
        }

        internal string CanonicalPayload()
        {
            return string.Join(",", new[]
            {
                ((int)SetRoute).ToString(CultureInfo.InvariantCulture),
                ((int)SpikeRoute).ToString(CultureInfo.InvariantCulture),
                Number(SetterX), Number(SetterZ), Number(AttackerX), Number(AttackerZ),
                Number(DefenderX), Number(DefenderZ), ((int)Blocker).ToString(CultureInfo.InvariantCulture),
                Number(BlockX), Number(BlockZ), ((int)CoverReceiver).ToString(CultureInfo.InvariantCulture),
                Number(CoverX), Number(CoverZ), ((int)SetRhythm).ToString(CultureInfo.InvariantCulture),
                Number(AttackFlightSeconds)
            });
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Number(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed class FormalMatchScenarioProvenanceV4
    {
        public FormalMatchScenarioProvenanceV4(
            string scenarioId,
            int formatVersion,
            string contentHash)
        {
            ScenarioId = string.IsNullOrWhiteSpace(scenarioId)
                ? throw new ArgumentException("Scenario ID is required.", nameof(scenarioId))
                : scenarioId;
            if (formatVersion != FormalMatchScenarioDefinitionV4.FormatVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(formatVersion));
            }

            if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 64 ||
                !IsLowerCaseHex(contentHash))
            {
                throw new ArgumentException("Scenario content hash is required.", nameof(contentHash));
            }

            FormatVersion = formatVersion;
            ContentHash = contentHash;
        }

        public string ScenarioId { get; }
        public int FormatVersion { get; }
        public string ContentHash { get; }

        private static bool IsLowerCaseHex(string value)
        {
            foreach (var character in value)
            {
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

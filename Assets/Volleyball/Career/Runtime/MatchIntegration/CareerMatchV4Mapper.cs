using System;
using System.Collections.Generic;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public enum CareerMatchV4FactPolicy
    {
        FixtureEstimated = 0,
        DirectAggregateOnly = 1
    }

    public sealed class CareerMatchV4RuntimeConfiguration
    {
        public CareerMatchV4RuntimeConfiguration(
            string physicsConfigurationHash,
            TrajectoryPredictionProviderConfigurationV4
                trajectoryPredictionProviderConfiguration,
            CareerMatchV4FactPolicy factPolicy)
        {
            if (string.IsNullOrWhiteSpace(physicsConfigurationHash))
            {
                throw new ArgumentException(
                    "A formal physics configuration hash is required.",
                    nameof(physicsConfigurationHash));
            }

            PhysicsConfigurationHash = physicsConfigurationHash;
            TrajectoryPredictionProviderConfiguration =
                trajectoryPredictionProviderConfiguration ??
                throw new ArgumentNullException(
                    nameof(trajectoryPredictionProviderConfiguration));
            if (!Enum.IsDefined(typeof(CareerMatchV4FactPolicy), factPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(factPolicy));
            }

            FactPolicy = factPolicy;
        }

        public string PhysicsConfigurationHash { get; }

        public TrajectoryPredictionProviderConfigurationV4
            TrajectoryPredictionProviderConfiguration { get; }

        public CareerMatchV4FactPolicy FactPolicy { get; }
    }

    public sealed class CareerMatchV4Mapper
    {
        public const int ContentVersion = 1;
        public const int RulesetVersion = 1;
        public const int CareerRandomAlgorithmVersion = 1;

        // Identifies the fixed offline fixture configuration. A physical Match
        // runner must replace these with hashes of its frozen runtime settings.
        public const string FixturePhysicsConfigurationHash =
            "2f4f24e55850c25d58d5f74f89f5a78d91350ad8b7183a5d845c7e0bb55a56d4";
        public const string FixturePredictorConfigurationHash =
            "1e5242f326637e85416d8f493344c7d52fbb53a35ce2d21be96b799e70d62e20";

        private readonly CareerMatchV4RuntimeConfiguration _configuration;

        public CareerMatchV4Mapper()
            : this(new CareerMatchV4RuntimeConfiguration(
                FixturePhysicsConfigurationHash,
                new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    1,
                    FixturePredictorConfigurationHash),
                CareerMatchV4FactPolicy.FixtureEstimated))
        {
        }

        public CareerMatchV4Mapper(
            CareerMatchV4RuntimeConfiguration configuration)
        {
            _configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
        }

        public MatchContextV4 ToContext(CareerMatchLaunch launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            RequireSupportedVersions(launch.Versions);
            return MatchContextV4.Create(
                launch.SessionId,
                unchecked((int)launch.MatchSeed),
                ToTeam(launch.Teams[0]),
                ToTeam(launch.Teams[1]),
                _configuration.PhysicsConfigurationHash,
                _configuration.TrajectoryPredictionProviderConfiguration,
                RulesVersions.FullRallyV3);
        }

        public CareerMatchFacts ToCareerFacts(
            MatchContextV4 context,
            MatchResultV4 result)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (result == null) throw new ArgumentNullException(nameof(result));

            result.ValidateAgainst(context);
            var statsByPlayer = IndexStats(result.PlayerStats);
            var playerFacts = new List<CareerMatchPlayerFacts>(12);
            AddPlayerFacts(context.Home, statsByPlayer, result.RalliesPlayed, playerFacts);
            AddPlayerFacts(context.Away, statsByPlayer, result.RalliesPlayed, playerFacts);

            return new CareerMatchFacts(
                new CareerMatchVersions(
                    CareerMatchVersions.ContractV4,
                    ContentVersion,
                    RulesetVersion,
                    CareerRandomAlgorithmVersion,
                    null,
                    null),
                result.SessionId,
                new Sha256Digest(result.ContextHash),
                CareerMatchResultStatus.Completed,
                result.WinnerTeamId,
                new[]
                {
                    new CareerMatchSetScore(1, result.HomeScore, result.AwayScore, true)
                },
                result.RalliesPlayed,
                playerFacts,
                new Sha256Digest(result.ResultHash));
        }

        private static void RequireSupportedVersions(CareerMatchVersions versions)
        {
            if (versions.ContractVersion != CareerMatchVersions.ContractV4 ||
                versions.ContentVersion != ContentVersion ||
                versions.RulesetVersion != RulesetVersion ||
                versions.CareerRandomAlgorithmVersion != CareerRandomAlgorithmVersion ||
                versions.MatchSimulationVersion.HasValue ||
                versions.MatchRandomAlgorithmVersion.HasValue)
            {
                throw new ArgumentException(
                    "The offline Career integration supports Match V4 and Career content/rules/random version 1.",
                    nameof(versions));
            }
        }

        private static TeamSnapshotV4 ToTeam(CareerMatchTeamLaunch team)
        {
            var players = new PlayerSnapshotV4[team.Players.Count];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = ToPlayer(team.Players[index]);
            }

            return new TeamSnapshotV4(
                team.TeamId,
                team.TeamId.Value,
                team.Side == CareerMatchTeamSide.Home ? TeamSide.Home : TeamSide.Away,
                players);
        }

        private static PlayerSnapshotV4 ToPlayer(CareerMatchPlayerLaunch player)
        {
            var attributes = player.Attributes;
            var movement = Unit(attributes.Movement.AbilityBasisPoints);
            var reception = Unit(attributes.Reception.AbilityBasisPoints);
            var defense = Unit(attributes.Defense.AbilityBasisPoints);
            var spike = Unit(attributes.Spike.AbilityBasisPoints);
            var serve = Unit(attributes.Serve.AbilityBasisPoints);
            var block = Unit(attributes.Block.AbilityBasisPoints);
            var jump = Unit(attributes.Jump.AbilityBasisPoints);
            var stamina = Unit(attributes.Stamina.AbilityBasisPoints);
            var readiness = 0.75f + (0.25f * (1f - (player.Fatigue / 100f)));

            var physical = new PhysicalBaseAttributesV4(
                1.90f,
                2.42f,
                Ready(jump, readiness),
                Ready(movement, readiness),
                Ready((defense + reception) * 0.5f, readiness),
                Ready((movement + reception + defense) / 3f, readiness));
            var technical = new TechnicalBaseAttributesV4(
                Ready(spike, readiness),
                Ready((spike * 0.75f) + (jump * 0.25f), readiness),
                Ready(block, readiness),
                Ready(defense, readiness),
                Ready(reception, readiness),
                Ready((reception + defense) * 0.5f, readiness),
                Ready(serve, readiness),
                Ready((reception + defense) * 0.5f, readiness),
                Ready((defense + stamina) * 0.5f, readiness));

            return new PlayerSnapshotV4(
                player.PlayerId,
                player.PlayerId.Value,
                player.JerseyNumber,
                ToPosition(player.Position),
                DominantHandV4.Right,
                physical,
                technical,
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static IReadOnlyDictionary<PlayerId, PlayerMatchStatsV4> IndexStats(
            IReadOnlyList<PlayerMatchStatsV4> stats)
        {
            var result = new Dictionary<PlayerId, PlayerMatchStatsV4>();
            foreach (var item in stats) result.Add(item.PlayerId, item);
            return result;
        }

        private void AddPlayerFacts(
            TeamSnapshotV4 team,
            IReadOnlyDictionary<PlayerId, PlayerMatchStatsV4> statsByPlayer,
            int rallyCount,
            ICollection<CareerMatchPlayerFacts> output)
        {
            foreach (var player in team.RotationOrder)
            {
                if (!statsByPlayer.TryGetValue(player.PlayerId, out var stats))
                {
                    throw new ContractValidationException(
                        "Career settlement requires V4 stats for every frozen player.");
                }

                output.Add(_configuration.FactPolicy ==
                           CareerMatchV4FactPolicy.DirectAggregateOnly
                    ? ToDirectAggregateFacts(stats)
                    : ToCareerPlayerFacts(player.Position, stats, rallyCount));
            }
        }

        private static CareerMatchPlayerFacts ToDirectAggregateFacts(
            PlayerMatchStatsV4 stats)
        {
            var workloadBasisPoints = WorkloadBasisPoints(stats.Workload);
            return new CareerMatchPlayerFacts(
                stats.PlayerId,
                new CareerSpikeFacts(0, 0, 0),
                new CareerServeFacts(0, 0, 0),
                new CareerReceptionFacts(0, 0, 0, 0, 0, 0),
                new CareerDefenseFacts(0, 0),
                new CareerBlockFacts(0, 0, 0),
                new CareerMatchLoadFacts(
                    0, 0, 0, 0, 0,
                    workloadBasisPoints,
                    workloadBasisPoints),
                new CareerStabilityFacts(0, 0, 0, 0, 0));
        }

        private static CareerMatchPlayerFacts ToCareerPlayerFacts(
            PlayerPosition position,
            PlayerMatchStatsV4 stats,
            int rallyCount)
        {
            var attempts = Math.Max(stats.Contacts, checked(stats.Points + stats.Errors));
            var spike = new CareerSpikeFacts(0, 0, 0);
            var reception = new CareerReceptionFacts(0, 0, 0, 0, 0, 0);
            var defense = new CareerDefenseFacts(0, 0);
            var block = new CareerBlockFacts(0, 0, 0);

            switch (position)
            {
                case PlayerPosition.OutsideHitter:
                case PlayerPosition.Opposite:
                    spike = new CareerSpikeFacts(attempts, stats.Points, stats.Errors);
                    break;
                case PlayerPosition.MiddleBlocker:
                    block = new CareerBlockFacts(attempts, stats.Points, stats.Points);
                    break;
                case PlayerPosition.Setter:
                    defense = new CareerDefenseFacts(attempts, Math.Min(stats.Points, attempts));
                    break;
                case PlayerPosition.Libero:
                case PlayerPosition.Defender:
                    var perfect = Math.Min(stats.Points, Math.Max(0, attempts - stats.Errors));
                    var neutral = Math.Max(0, attempts - perfect - stats.Errors);
                    reception = new CareerReceptionFacts(
                        attempts, perfect, 0, neutral, 0, stats.Errors);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }

            var workloadBasisPoints = WorkloadBasisPoints(stats.Workload);
            var criticalActions = checked(stats.Points + stats.Errors);
            return new CareerMatchPlayerFacts(
                stats.PlayerId,
                spike,
                new CareerServeFacts(0, 0, 0),
                reception,
                defense,
                block,
                new CareerMatchLoadFacts(
                    Math.Min(rallyCount, stats.Contacts),
                    0, 0, 0, 0,
                    workloadBasisPoints,
                    workloadBasisPoints),
                new CareerStabilityFacts(
                    criticalActions,
                    stats.Points,
                    stats.Errors,
                    stats.Errors >= 2 ? 1 : 0,
                    stats.Errors >= 2 ? 2 : 0));
        }

        private static PlayerPosition ToPosition(CareerMatchPlayerPosition position)
        {
            switch (position)
            {
                case CareerMatchPlayerPosition.Setter: return PlayerPosition.Setter;
                case CareerMatchPlayerPosition.OutsideHitter: return PlayerPosition.OutsideHitter;
                case CareerMatchPlayerPosition.MiddleBlocker: return PlayerPosition.MiddleBlocker;
                case CareerMatchPlayerPosition.Opposite: return PlayerPosition.Opposite;
                case CareerMatchPlayerPosition.Libero: return PlayerPosition.Libero;
                default: throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        private static int WorkloadBasisPoints(float workload)
        {
            var scaled = workload <= 1f ? workload * 10000f : workload;
            return Math.Max(0, Math.Min(10000, (int)Math.Round(scaled)));
        }

        private static float Unit(int basisPoints)
        {
            return Clamp01(basisPoints / 10000f);
        }

        private static float Ready(float value, float readiness)
        {
            return Clamp01(value * readiness);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}

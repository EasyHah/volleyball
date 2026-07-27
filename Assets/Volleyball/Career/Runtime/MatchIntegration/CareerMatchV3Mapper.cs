using System;
using System.Collections.Generic;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerMatchV3Mapper
    {
        public const int ContentVersion = 1;
        public const int RulesetVersion = 1;
        public const int CareerRandomAlgorithmVersion = 1;

        public MatchContextV3 ToContext(CareerMatchLaunch launch)
        {
            if (launch == null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            RequireSupportedVersions(launch.Versions);
            return MatchContextV3.Create(
                launch.SessionId,
                unchecked((int)launch.MatchSeed),
                ToTeam(launch.Teams[0]),
                ToTeam(launch.Teams[1]));
        }

        public CareerMatchFacts ToCareerFacts(MatchContextV3 context, MatchResultV3 result)
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
            var rallyCount = checked(result.HomeScore + result.AwayScore);
            var statsByPlayer = IndexStats(result.PlayerStats);
            var playerFacts = new List<CareerMatchPlayerFacts>(12);
            AddPlayerFacts(context.Home, statsByPlayer, rallyCount, playerFacts);
            AddPlayerFacts(context.Away, statsByPlayer, rallyCount, playerFacts);

            return new CareerMatchFacts(
                new CareerMatchVersions(
                    CareerMatchVersions.ContractV3,
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
                rallyCount,
                playerFacts,
                new Sha256Digest(result.ResultHash));
        }

        private static void RequireSupportedVersions(CareerMatchVersions versions)
        {
            if (versions.ContractVersion != CareerMatchVersions.ContractV3 ||
                versions.ContentVersion != ContentVersion ||
                versions.RulesetVersion != RulesetVersion ||
                versions.CareerRandomAlgorithmVersion != CareerRandomAlgorithmVersion ||
                versions.MatchSimulationVersion.HasValue ||
                versions.MatchRandomAlgorithmVersion.HasValue)
            {
                throw new ArgumentException(
                    "The first Career V3 integration supports fixture contract 3 and Career content/rules/random version 1.",
                    nameof(versions));
            }
        }

        private static TeamSnapshotV3 ToTeam(CareerMatchTeamLaunch team)
        {
            var players = new PlayerSnapshotV3[team.Players.Count];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = ToPlayer(team.Players[index]);
            }

            return new TeamSnapshotV3(
                team.TeamId,
                team.TeamId.Value,
                team.Side == CareerMatchTeamSide.Home ? TeamSide.Home : TeamSide.Away,
                players);
        }

        private static PlayerSnapshotV3 ToPlayer(CareerMatchPlayerLaunch player)
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
            var fitness = 1f - (player.Fatigue / 100f);
            var readiness = 0.75f + (0.25f * fitness);

            return new PlayerSnapshotV3(
                player.PlayerId,
                player.PlayerId.Value,
                player.JerseyNumber,
                ToPosition(player.Position),
                new PlayerAbilitySnapshotV3(
                    Clamp01(movement * readiness),
                    Clamp01(((defense + reception) * 0.5f) * readiness),
                    Clamp01(jump * readiness),
                    3.20f + (0.35f * jump),
                    Clamp01(reception * readiness),
                    Clamp01(((reception + defense) * 0.5f) * readiness),
                    Clamp01(spike * readiness),
                    Clamp01(((spike * 0.75f) + (jump * 0.25f)) * readiness),
                    Clamp01(((serve + reception) * 0.5f) * readiness),
                    Clamp01(block * readiness),
                    Clamp01(((defense + stamina) * 0.5f) * readiness),
                    ContractVersions.MatchV3,
                    0,
                    false,
                    Array.Empty<string>()));
        }

        private static IReadOnlyDictionary<PlayerId, PlayerMatchStatsV3> IndexStats(
            IReadOnlyList<PlayerMatchStatsV3> stats)
        {
            var result = new Dictionary<PlayerId, PlayerMatchStatsV3>();
            foreach (var item in stats)
            {
                result.Add(item.PlayerId, item);
            }

            return result;
        }

        private static void AddPlayerFacts(
            TeamSnapshotV3 team,
            IReadOnlyDictionary<PlayerId, PlayerMatchStatsV3> statsByPlayer,
            int rallyCount,
            ICollection<CareerMatchPlayerFacts> output)
        {
            foreach (var player in team.Players)
            {
                if (!statsByPlayer.TryGetValue(player.PlayerId, out var stats))
                {
                    throw new ContractValidationException(
                        "Career settlement requires V3 stats for every frozen player.");
                }

                output.Add(ToCareerPlayerFacts(player.Position, stats, rallyCount));
            }
        }

        private static CareerMatchPlayerFacts ToCareerPlayerFacts(
            PlayerPosition position,
            PlayerMatchStatsV3 stats,
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
                        attempts,
                        perfect,
                        0,
                        neutral,
                        0,
                        stats.Errors);
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
                    0,
                    0,
                    0,
                    0,
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
                case CareerMatchPlayerPosition.Setter:
                    return PlayerPosition.Setter;
                case CareerMatchPlayerPosition.OutsideHitter:
                    return PlayerPosition.OutsideHitter;
                case CareerMatchPlayerPosition.MiddleBlocker:
                    return PlayerPosition.MiddleBlocker;
                case CareerMatchPlayerPosition.Opposite:
                    return PlayerPosition.Opposite;
                case CareerMatchPlayerPosition.Libero:
                    return PlayerPosition.Libero;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, null);
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

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}

using System;
using System.Collections.Generic;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerMatchV2Mapper
    {
        public MatchContextV2 ToContext(CareerMatchLaunch launch)
        {
            if (launch == null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            var teams = new MatchTeamSnapshotV2[launch.Teams.Count];
            for (var teamIndex = 0; teamIndex < teams.Length; teamIndex++)
            {
                teams[teamIndex] = ToTeam(launch.Teams[teamIndex]);
            }

            return MatchContextV2.Create(
                ToVersions(launch.Versions),
                launch.SessionId,
                ToExecutionMode(launch.ExecutionMode),
                launch.FixtureId,
                launch.FixtureVersion,
                launch.MatchSeed,
                launch.CompetitionId,
                launch.ScheduleItemId,
                launch.ImportanceBasisPoints,
                new MatchFormatV2(
                    launch.Format.Kind,
                    launch.Format.TeamSize,
                    launch.Format.SetsToWin,
                    launch.Format.SetTargetPoints,
                    launch.Format.MinimumLeadPoints),
                ToPriority(launch.PreMatchPriority),
                teams);
        }

        public CareerMatchFacts ToCareerFacts(MatchResultV2 result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _ = MatchContractV2Json.SerializeResult(result);
            var sets = new CareerMatchSetScore[result.Sets.Count];
            for (var index = 0; index < sets.Length; index++)
            {
                var set = result.Sets[index];
                sets[index] = new CareerMatchSetScore(
                    set.SetNumber, set.HomePoints, set.AwayPoints, set.IsComplete);
            }

            var playerFacts = new CareerMatchPlayerFacts[result.PlayerFacts.Count];
            for (var index = 0; index < playerFacts.Length; index++)
            {
                playerFacts[index] = ToCareerPlayerFacts(result.PlayerFacts[index]);
            }

            return new CareerMatchFacts(
                ToCareerVersions(result.Versions),
                result.SessionId,
                new Sha256Digest(result.ContextHash),
                ToStatus(result.Status),
                result.WinnerTeamId,
                sets,
                result.RallyCount,
                playerFacts,
                new Sha256Digest(result.ResultHash));
        }

        private static MatchVersionSetV2 ToVersions(CareerMatchVersions versions)
        {
            return new MatchVersionSetV2(
                versions.ContractVersion,
                versions.ContentVersion,
                versions.RulesetVersion,
                versions.CareerRandomAlgorithmVersion,
                versions.MatchSimulationVersion,
                versions.MatchRandomAlgorithmVersion);
        }

        private static CareerMatchVersions ToCareerVersions(MatchVersionSetV2 versions)
        {
            return new CareerMatchVersions(
                versions.ContractVersion,
                versions.ContentVersion,
                versions.RulesetVersion,
                versions.CareerRandomAlgorithmVersion,
                versions.MatchSimulationVersion,
                versions.MatchRandomAlgorithmVersion);
        }

        private static MatchTeamSnapshotV2 ToTeam(CareerMatchTeamLaunch team)
        {
            var players = new MatchPlayerSnapshotV2[team.Players.Count];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = ToPlayer(team.Players[index]);
            }

            return new MatchTeamSnapshotV2(team.TeamId, ToSide(team.Side), players);
        }

        private static MatchPlayerSnapshotV2 ToPlayer(CareerMatchPlayerLaunch player)
        {
            var attributes = player.Attributes;
            return new MatchPlayerSnapshotV2(
                player.PlayerId,
                player.JerseyNumber,
                ToPosition(player.Position),
                player.RotationSlot,
                (100 - player.Fatigue) * 100,
                new MatchAbilitySnapshotV2(
                    attributes.Spike.AbilityBasisPoints,
                    attributes.Serve.AbilityBasisPoints,
                    attributes.Reception.AbilityBasisPoints,
                    attributes.Defense.AbilityBasisPoints,
                    attributes.Block.AbilityBasisPoints,
                    attributes.Movement.AbilityBasisPoints,
                    attributes.Jump.AbilityBasisPoints,
                    attributes.Stamina.AbilityBasisPoints));
        }

        private static CareerMatchPlayerFacts ToCareerPlayerFacts(MatchPlayerFactsV2 facts)
        {
            return new CareerMatchPlayerFacts(
                facts.PlayerId,
                new CareerSpikeFacts(facts.Spike.Attempts, facts.Spike.Points, facts.Spike.Errors),
                new CareerServeFacts(facts.Serve.Attempts, facts.Serve.Aces, facts.Serve.Errors),
                new CareerReceptionFacts(
                    facts.Reception.Attempts,
                    facts.Reception.Perfect,
                    facts.Reception.Positive,
                    facts.Reception.Neutral,
                    facts.Reception.Negative,
                    facts.Reception.Errors),
                new CareerDefenseFacts(facts.Defense.Attempts, facts.Defense.Successes),
                new CareerBlockFacts(
                    facts.Block.Attempts, facts.Block.EffectiveTouches, facts.Block.Points),
                new CareerMatchLoadFacts(
                    facts.Load.RalliesPlayed,
                    facts.Load.ActiveDurationMilliseconds,
                    facts.Load.MovementDistanceMillimeters,
                    facts.Load.JumpCount,
                    facts.Load.HighLoadJumpCount,
                    facts.Load.LandingLoadBasisPoints,
                    facts.Load.TotalWorkloadBasisPoints),
                new CareerStabilityFacts(
                    facts.Stability.CriticalActions,
                    facts.Stability.CriticalSuccesses,
                    facts.Stability.CriticalErrors,
                    facts.Stability.ErrorStreakEpisodes,
                    facts.Stability.LongestErrorStreak));
        }

        private static MatchExecutionModeV2 ToExecutionMode(CareerMatchExecutionMode mode)
        {
            switch (mode)
            {
                case CareerMatchExecutionMode.Fixture:
                    return MatchExecutionModeV2.Fixture;
                case CareerMatchExecutionMode.Direct:
                    return MatchExecutionModeV2.Direct;
                case CareerMatchExecutionMode.QuickSimulation:
                    return MatchExecutionModeV2.QuickSimulation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported execution mode.");
            }
        }

        private static PreMatchPriorityV2 ToPriority(CareerPreMatchPriority priority)
        {
            switch (priority)
            {
                case CareerPreMatchPriority.AttackFirst:
                    return PreMatchPriorityV2.AttackFirst;
                case CareerPreMatchPriority.FirstContactSecurity:
                    return PreMatchPriorityV2.FirstContactSecurity;
                case CareerPreMatchPriority.StaminaControl:
                    return PreMatchPriorityV2.StaminaControl;
                default:
                    throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unsupported priority.");
            }
        }

        private static TeamSideV2 ToSide(CareerMatchTeamSide side)
        {
            switch (side)
            {
                case CareerMatchTeamSide.Home:
                    return TeamSideV2.Home;
                case CareerMatchTeamSide.Away:
                    return TeamSideV2.Away;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, "Unsupported team side.");
            }
        }

        private static PlayerPositionV2 ToPosition(CareerMatchPlayerPosition position)
        {
            switch (position)
            {
                case CareerMatchPlayerPosition.Setter:
                    return PlayerPositionV2.Setter;
                case CareerMatchPlayerPosition.OutsideHitter:
                    return PlayerPositionV2.OutsideHitter;
                case CareerMatchPlayerPosition.MiddleBlocker:
                    return PlayerPositionV2.MiddleBlocker;
                case CareerMatchPlayerPosition.Opposite:
                    return PlayerPositionV2.Opposite;
                case CareerMatchPlayerPosition.Libero:
                    return PlayerPositionV2.Libero;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(position), position, "Unsupported player position.");
            }
        }

        private static CareerMatchResultStatus ToStatus(MatchStatusV2 status)
        {
            switch (status)
            {
                case MatchStatusV2.Completed:
                    return CareerMatchResultStatus.Completed;
                case MatchStatusV2.Abandoned:
                    return CareerMatchResultStatus.Abandoned;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported result status.");
            }
        }
    }
}

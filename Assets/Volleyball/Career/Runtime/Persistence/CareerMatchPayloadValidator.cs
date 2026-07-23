using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.Persistence
{
    internal static class CareerMatchPayloadValidator
    {
        public static void Validate(CareerSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            try
            {
                if (snapshot.PendingMatch != null)
                {
                    ValidatePending(snapshot, snapshot.PendingMatch);
                }

                for (var index = 0; index < snapshot.MatchHistory.Count; index++)
                {
                    ValidateHistory(snapshot, snapshot.MatchHistory[index], index);
                }
            }
            catch (MatchV2ContractException exception)
            {
                throw new ArgumentException(
                    "Career match payload failed the Shared.MatchV2 contract.",
                    nameof(snapshot),
                    exception);
            }
        }

        private static void ValidatePending(
            CareerSaveSnapshot snapshot,
            PendingCareerMatch pending)
        {
            var bytes = pending.CanonicalContextUtf8;
            var context = MatchContractV2Json.DeserializeContext(bytes);
            RequireSameBytes(
                bytes,
                MatchContractV2Json.SerializeContext(context),
                "pendingMatch.canonicalContextUtf8");
            RequireEqual(context.ContextHash, pending.ContextDigest.Value, "pendingMatch.contextHash");
            RequireEqual(context.SessionId, pending.SessionId, "pendingMatch.sessionId");
            RequireVersions(context.Versions, pending.Versions, "pendingMatch.versions");
            RequireEqual(
                context.Versions.ContractVersion,
                snapshot.Versions.ContractVersion,
                "pendingMatch.versions.contractVersion");
            RequireEqual(
                context.Versions.ContentVersion,
                snapshot.Versions.ContentVersion,
                "pendingMatch.versions.contentVersion");
            RequireEqual(
                context.Versions.RulesetVersion,
                snapshot.Versions.RulesetVersion,
                "pendingMatch.versions.rulesetVersion");
            RequireEqual(
                context.Versions.CareerRandomAlgorithmVersion,
                snapshot.Versions.CareerRandomAlgorithmVersion,
                "pendingMatch.versions.careerRandomAlgorithmVersion");
            RequireEqual(context.ExecutionMode, ToShared(pending.ExecutionMode), "pendingMatch.executionMode");
            RequireEqual(context.FixtureId, pending.FixtureId, "pendingMatch.fixtureId");
            RequireEqual(context.FixtureVersion, pending.FixtureVersion, "pendingMatch.fixtureVersion");
            RequireEqual(context.MatchSeed, pending.MatchSeed, "pendingMatch.matchSeed");
            RequireEqual(context.CompetitionId, pending.CompetitionId, "pendingMatch.competitionId");
            RequireEqual(context.ScheduleItemId, pending.ScheduleItemId, "pendingMatch.scheduleItemId");
            RequireEqual(
                context.PreMatchPriority,
                ToShared(pending.PreMatchPriority),
                "pendingMatch.preMatchPriority");
            RequireRoster(context, pending, snapshot);
            RequireCurrentPlayer(context, snapshot, compareFitness: true, "pendingMatch");
        }

        private static void ValidateHistory(
            CareerSaveSnapshot snapshot,
            CareerMatchHistoryEntry history,
            int index)
        {
            var path = "matchHistory[" + index + "]";
            var contextBytes = history.CanonicalContextUtf8;
            var context = MatchContractV2Json.DeserializeContext(contextBytes);
            RequireSameBytes(
                contextBytes,
                MatchContractV2Json.SerializeContext(context),
                path + ".canonicalContextUtf8");

            var resultBytes = history.CanonicalResultUtf8;
            var result = MatchContractV2Json.DeserializeResult(resultBytes, context);
            RequireSameBytes(
                resultBytes,
                MatchContractV2Json.SerializeResult(result),
                path + ".canonicalResultUtf8");
            RequireEqual(result.Status, MatchStatusV2.Completed, path + ".status");
            RequireEqual(context.SessionId, history.SessionId, path + ".sessionId");
            RequireEqual(context.ScheduleItemId, history.ScheduleItemId, path + ".scheduleItemId");
            RequireEqual(context.ContextHash, history.ContextDigest.Value, path + ".contextHash");
            RequireEqual(result.SessionId, history.SessionId, path + ".result.sessionId");
            RequireEqual(result.ContextHash, history.ContextDigest.Value, path + ".result.contextHash");
            RequireEqual(result.ResultHash, history.ResultDigest.Value, path + ".resultHash");
            RequireEqual(
                context.PreMatchPriority,
                ToShared(history.SettlementSummary.SelectedPriority),
                path + ".settlementSummary.selectedPriority");
            RequireSets(result, history.SettlementSummary, path);
            RequireFacts(result, snapshot.Player.PlayerId.Value, history.SettlementSummary, path);
            RequireEqual(
                result.WinnerTeamId.HasValue &&
                result.WinnerTeamId.Value.Value == snapshot.TeamId.Value.Value,
                history.SettlementSummary.Won,
                path + ".settlementSummary.won");
            RequireCurrentPlayer(context, snapshot, compareFitness: false, path);
        }

        private static void RequireRoster(
            MatchContextV2 context,
            PendingCareerMatch pending,
            CareerSaveSnapshot snapshot)
        {
            RequireEqual(context.Teams.Count, 2, "pendingMatch.teams.count");
            RequireEqual(context.Teams[0].Side, TeamSideV2.Home, "pendingMatch.teams[0].side");
            RequireEqual(context.Teams[1].Side, TeamSideV2.Away, "pendingMatch.teams[1].side");
            RequireEqual(context.Teams[0].TeamId.Value, pending.HomeTeamId.Value, "pendingMatch.homeTeamId");
            RequireEqual(context.Teams[1].TeamId.Value, pending.AwayTeamId.Value, "pendingMatch.awayTeamId");

            var flattened = FlattenPlayers(context);
            RequireEqual(flattened.Count, pending.OrderedPlayerIds.Count, "pendingMatch.orderedPlayerIds.count");
            for (var index = 0; index < flattened.Count; index++)
            {
                RequireEqual(
                    flattened[index].PlayerId.Value,
                    pending.OrderedPlayerIds[index].Value,
                    "pendingMatch.orderedPlayerIds[" + index + "]");
            }

            RequireEqual(
                pending.ProtagonistPlayerId.Value,
                snapshot.Player.PlayerId.Value,
                "pendingMatch.protagonistPlayerId");
        }

        private static void RequireCurrentPlayer(
            MatchContextV2 context,
            CareerSaveSnapshot snapshot,
            bool compareFitness,
            string path)
        {
            if (snapshot.Player == null || !snapshot.TeamId.HasValue)
            {
                throw Contradiction(path + " requires the current Career player and team.");
            }

            MatchTeamSnapshotV2 careerTeam = null;
            for (var teamIndex = 0; teamIndex < context.Teams.Count; teamIndex++)
            {
                if (context.Teams[teamIndex].TeamId.Value == snapshot.TeamId.Value.Value)
                {
                    careerTeam = context.Teams[teamIndex];
                    break;
                }
            }

            if (careerTeam == null)
            {
                throw Contradiction(path + " current Career team is absent from context.");
            }

            MatchPlayerSnapshotV2 protagonist = null;
            for (var playerIndex = 0; playerIndex < careerTeam.Players.Count; playerIndex++)
            {
                if (careerTeam.Players[playerIndex].PlayerId.Value == snapshot.Player.PlayerId.Value)
                {
                    protagonist = careerTeam.Players[playerIndex];
                    break;
                }
            }

            if (protagonist == null)
            {
                throw Contradiction(path + " current Career player is absent from its team segment.");
            }

            RequireEqual(protagonist.JerseyNumber, snapshot.Player.JerseyNumber, path + ".protagonist.jerseyNumber");
            var abilities = protagonist.Abilities;
            var career = snapshot.Player.Attributes;
            RequireEqual(abilities.SpikeBasisPoints, career.Spike.AbilityBasisPoints, path + ".protagonist.abilities.spike");
            RequireEqual(abilities.ServeBasisPoints, career.Serve.AbilityBasisPoints, path + ".protagonist.abilities.serve");
            RequireEqual(abilities.ReceptionBasisPoints, career.Reception.AbilityBasisPoints, path + ".protagonist.abilities.reception");
            RequireEqual(abilities.DefenseBasisPoints, career.Defense.AbilityBasisPoints, path + ".protagonist.abilities.defense");
            RequireEqual(abilities.BlockBasisPoints, career.Block.AbilityBasisPoints, path + ".protagonist.abilities.block");
            RequireEqual(abilities.MovementBasisPoints, career.Movement.AbilityBasisPoints, path + ".protagonist.abilities.movement");
            RequireEqual(abilities.JumpBasisPoints, career.Jump.AbilityBasisPoints, path + ".protagonist.abilities.jump");
            RequireEqual(abilities.StaminaBasisPoints, career.Stamina.AbilityBasisPoints, path + ".protagonist.abilities.stamina");
            if (compareFitness)
            {
                var expectedFitness = Math.Max(0, Math.Min(10000, 10000 - snapshot.Fatigue.Value * 100));
                RequireEqual(protagonist.FitnessBasisPoints, expectedFitness, path + ".protagonist.fitnessBasisPoints");
            }
        }

        private static List<MatchPlayerSnapshotV2> FlattenPlayers(MatchContextV2 context)
        {
            var players = new List<MatchPlayerSnapshotV2>(12);
            for (var teamIndex = 0; teamIndex < context.Teams.Count; teamIndex++)
            {
                for (var playerIndex = 0; playerIndex < context.Teams[teamIndex].Players.Count; playerIndex++)
                {
                    players.Add(context.Teams[teamIndex].Players[playerIndex]);
                }
            }

            return players;
        }

        private static void RequireSets(
            MatchResultV2 result,
            CareerSettlementSummary summary,
            string path)
        {
            RequireEqual(result.Sets.Count, summary.Sets.Count, path + ".settlementSummary.sets.count");
            for (var index = 0; index < result.Sets.Count; index++)
            {
                var actual = result.Sets[index];
                var expected = summary.Sets[index];
                RequireEqual(actual.SetNumber, expected.SetNumber, path + ".settlementSummary.sets[" + index + "].setNumber");
                RequireEqual(actual.HomePoints, expected.HomePoints, path + ".settlementSummary.sets[" + index + "].homePoints");
                RequireEqual(actual.AwayPoints, expected.AwayPoints, path + ".settlementSummary.sets[" + index + "].awayPoints");
                RequireEqual(actual.IsComplete, expected.IsComplete, path + ".settlementSummary.sets[" + index + "].isComplete");
            }
        }

        private static void RequireFacts(
            MatchResultV2 result,
            string playerId,
            CareerSettlementSummary summary,
            string path)
        {
            MatchPlayerFactsV2 actual = null;
            for (var index = 0; index < result.PlayerFacts.Count; index++)
            {
                if (result.PlayerFacts[index].PlayerId.Value == playerId)
                {
                    actual = result.PlayerFacts[index];
                    break;
                }
            }

            if (actual == null)
            {
                throw Contradiction(path + " result omits the current Career player's facts.");
            }

            var expected = summary.ProtagonistFacts;
            RequireEqual(actual.Spike.Attempts, expected.Spike.Attempts, path + ".facts.spike.attempts");
            RequireEqual(actual.Spike.Points, expected.Spike.Points, path + ".facts.spike.points");
            RequireEqual(actual.Spike.Errors, expected.Spike.Errors, path + ".facts.spike.errors");
            RequireEqual(actual.Serve.Attempts, expected.Serve.Attempts, path + ".facts.serve.attempts");
            RequireEqual(actual.Serve.Aces, expected.Serve.Aces, path + ".facts.serve.aces");
            RequireEqual(actual.Serve.Errors, expected.Serve.Errors, path + ".facts.serve.errors");
            RequireEqual(actual.Reception.Attempts, expected.Reception.Attempts, path + ".facts.reception.attempts");
            RequireEqual(actual.Reception.Perfect, expected.Reception.Perfect, path + ".facts.reception.perfect");
            RequireEqual(actual.Reception.Positive, expected.Reception.Positive, path + ".facts.reception.positive");
            RequireEqual(actual.Reception.Neutral, expected.Reception.Neutral, path + ".facts.reception.neutral");
            RequireEqual(actual.Reception.Negative, expected.Reception.Negative, path + ".facts.reception.negative");
            RequireEqual(actual.Reception.Errors, expected.Reception.Errors, path + ".facts.reception.errors");
            RequireEqual(actual.Defense.Attempts, expected.Defense.Attempts, path + ".facts.defense.attempts");
            RequireEqual(actual.Defense.Successes, expected.Defense.Successes, path + ".facts.defense.successes");
            RequireEqual(actual.Block.Attempts, expected.Block.Attempts, path + ".facts.block.attempts");
            RequireEqual(actual.Block.EffectiveTouches, expected.Block.EffectiveTouches, path + ".facts.block.effectiveTouches");
            RequireEqual(actual.Block.Points, expected.Block.Points, path + ".facts.block.points");
            RequireEqual(actual.Load.RalliesPlayed, expected.Load.RalliesPlayed, path + ".facts.load.ralliesPlayed");
            RequireEqual(actual.Load.ActiveDurationMilliseconds, expected.Load.ActiveDurationMilliseconds, path + ".facts.load.activeDurationMilliseconds");
            RequireEqual(actual.Load.MovementDistanceMillimeters, expected.Load.MovementDistanceMillimeters, path + ".facts.load.movementDistanceMillimeters");
            RequireEqual(actual.Load.JumpCount, expected.Load.JumpCount, path + ".facts.load.jumpCount");
            RequireEqual(actual.Load.HighLoadJumpCount, expected.Load.HighLoadJumpCount, path + ".facts.load.highLoadJumpCount");
            RequireEqual(actual.Load.LandingLoadBasisPoints, expected.Load.LandingLoadBasisPoints, path + ".facts.load.landingLoadBasisPoints");
            RequireEqual(actual.Load.TotalWorkloadBasisPoints, expected.Load.TotalWorkloadBasisPoints, path + ".facts.load.totalWorkloadBasisPoints");
            RequireEqual(actual.Stability.CriticalActions, expected.Stability.CriticalActions, path + ".facts.stability.criticalActions");
            RequireEqual(actual.Stability.CriticalSuccesses, expected.Stability.CriticalSuccesses, path + ".facts.stability.criticalSuccesses");
            RequireEqual(actual.Stability.CriticalErrors, expected.Stability.CriticalErrors, path + ".facts.stability.criticalErrors");
            RequireEqual(actual.Stability.ErrorStreakEpisodes, expected.Stability.ErrorStreakEpisodes, path + ".facts.stability.errorStreakEpisodes");
            RequireEqual(actual.Stability.LongestErrorStreak, expected.Stability.LongestErrorStreak, path + ".facts.stability.longestErrorStreak");
        }

        private static void RequireVersions(
            MatchVersionSetV2 actual,
            CareerMatchLifecycleVersions expected,
            string path)
        {
            RequireEqual(actual.ContractVersion, expected.ContractVersion, path + ".contractVersion");
            RequireEqual(actual.ContentVersion, expected.ContentVersion, path + ".contentVersion");
            RequireEqual(actual.RulesetVersion, expected.RulesetVersion, path + ".rulesetVersion");
            RequireEqual(actual.CareerRandomAlgorithmVersion, expected.CareerRandomAlgorithmVersion, path + ".careerRandomAlgorithmVersion");
            RequireEqual(actual.MatchSimulationVersion, expected.MatchSimulationVersion, path + ".matchSimulationVersion");
            RequireEqual(actual.MatchRandomAlgorithmVersion, expected.MatchRandomAlgorithmVersion, path + ".matchRandomAlgorithmVersion");
        }

        private static MatchExecutionModeV2 ToShared(CareerMatchLifecycleExecutionMode value)
        {
            switch (value)
            {
                case CareerMatchLifecycleExecutionMode.Fixture: return MatchExecutionModeV2.Fixture;
                case CareerMatchLifecycleExecutionMode.Direct: return MatchExecutionModeV2.Direct;
                case CareerMatchLifecycleExecutionMode.QuickSimulation: return MatchExecutionModeV2.QuickSimulation;
                default: throw Contradiction("Unsupported Career match execution mode.");
            }
        }

        private static PreMatchPriorityV2 ToShared(CareerMatchPriority value)
        {
            switch (value)
            {
                case CareerMatchPriority.AttackFirst: return PreMatchPriorityV2.AttackFirst;
                case CareerMatchPriority.FirstContactSecurity: return PreMatchPriorityV2.FirstContactSecurity;
                case CareerMatchPriority.StaminaControl: return PreMatchPriorityV2.StaminaControl;
                default: throw Contradiction("Unsupported Career match priority.");
            }
        }

        private static void RequireSameBytes(byte[] actual, byte[] expected, string path)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                throw Contradiction(path + " does not round-trip byte-for-byte.");
            }

            for (var index = 0; index < actual.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw Contradiction(path + " does not round-trip byte-for-byte.");
                }
            }
        }

        private static void RequireEqual<T>(T actual, T expected, string path)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                throw Contradiction(path + " contradicts the canonical Shared.MatchV2 payload.");
            }
        }

        private static ArgumentException Contradiction(string message)
        {
            return new ArgumentException(message, "snapshot");
        }
    }
}

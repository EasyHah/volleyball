using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public static class CareerMatchSettlementRulesV1
    {
        private const long MaximumSafeInteger = 9007199254740991L;
        private const long BasisPointScale = 10000L;
        private const int SupportedContentVersion = 1;
        private const int SupportedRulesetVersion = 1;

        private static readonly string[] GrowthReasonIds =
        {
            "reason.match.growth.spike",
            "reason.match.growth.serve",
            "reason.match.growth.reception",
            "reason.match.growth.defense",
            "reason.match.growth.block",
            "reason.match.growth.movement",
            "reason.match.growth.jump",
            "reason.match.growth.stamina"
        };

        public static CareerSettlementSummary Calculate(
            PendingCareerMatch pendingMatch,
            CareerMatchFacts completedFacts,
            CareerPlayerRecord currentPlayer,
            PotentialGrade potentialGrade,
            int currentFatigue,
            int currentMindset,
            int currentCoachTrust)
        {
            if (pendingMatch == null)
            {
                throw new ArgumentNullException(nameof(pendingMatch));
            }

            if (completedFacts == null)
            {
                throw new ArgumentNullException(nameof(completedFacts));
            }

            if (currentPlayer == null)
            {
                throw new ArgumentNullException(nameof(currentPlayer));
            }

            if (currentPlayer.Attributes == null)
            {
                throw new ArgumentNullException(
                    nameof(currentPlayer),
                    "The current Career player must have attributes.");
            }

            ValidateStatus(currentFatigue, nameof(currentFatigue));
            ValidateStatus(currentMindset, nameof(currentMindset));
            ValidateStatus(currentCoachTrust, nameof(currentCoachTrust));
            var potentialMultiplierBasisPoints =
                CareerWeekActionRulesV1.PotentialMultiplier(potentialGrade);

            ValidateVersions(pendingMatch, completedFacts);
            ValidateCompletedFacts(pendingMatch, completedFacts, currentPlayer);

            var protagonistIndex = FindProtagonistIndex(pendingMatch);
            var protagonistFacts = completedFacts.PlayerFacts[protagonistIndex];
            var protagonistTeamId = protagonistIndex < 6
                ? pendingMatch.HomeTeamId
                : pendingMatch.AwayTeamId;
            var won = completedFacts.WinnerTeamId.Value.Equals(protagonistTeamId);

            var baseExperience = CalculateBaseExperience(protagonistFacts);
            var growthChanges = CalculateGrowthChanges(
                pendingMatch,
                currentPlayer.Attributes,
                baseExperience,
                potentialMultiplierBasisPoints);
            var priorityExecuted = IsPriorityExecuted(
                pendingMatch.PreMatchPriority,
                protagonistFacts);

            var requestedFatigue = CalculateRequestedFatigue(
                pendingMatch.PreMatchPriority,
                protagonistFacts.Load,
                currentPlayer.Attributes.Stamina.AbilityBasisPoints);
            var requestedMindset = CalculateRequestedMindset(
                won,
                protagonistFacts.Stability);
            var requestedCoachTrust = CalculateRequestedCoachTrust(
                won,
                priorityExecuted,
                protagonistFacts.Stability);

            var fatigueChange = StatusChange(
                "reason.match.fatigue.workload",
                currentFatigue,
                requestedFatigue);
            var mindsetChange = StatusChange(
                "reason.match.mindset.result_stability",
                currentMindset,
                requestedMindset);
            var coachTrustChange = StatusChange(
                "reason.match.coach_trust.priority_stability_result",
                currentCoachTrust,
                requestedCoachTrust);

            return new CareerSettlementSummary(
                CopySets(completedFacts),
                CopyProtagonistFacts(protagonistFacts),
                pendingMatch.PreMatchPriority,
                priorityExecuted,
                won,
                growthChanges,
                fatigueChange,
                mindsetChange,
                coachTrustChange,
                StatusChange(
                    "reason.weekend.no_numeric_change",
                    fatigueChange.NewValue,
                    0),
                StatusChange(
                    "reason.weekend.no_numeric_change",
                    mindsetChange.NewValue,
                    0),
                StatusChange(
                    "reason.weekend.no_numeric_change",
                    coachTrustChange.NewValue,
                    0));
        }

        private static void ValidateVersions(
            PendingCareerMatch pendingMatch,
            CareerMatchFacts completedFacts)
        {
            var pending = pendingMatch.Versions;
            if (pending.ContractVersion != CareerMatchLifecycleVersions.ContractV2 ||
                pending.ContentVersion != SupportedContentVersion ||
                pending.RulesetVersion != SupportedRulesetVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pendingMatch),
                    "Match settlement V1 requires contract 2, content 1 and ruleset 1.");
            }

            var facts = completedFacts.Versions;
            if (facts.ContractVersion != pending.ContractVersion ||
                facts.ContentVersion != pending.ContentVersion ||
                facts.RulesetVersion != pending.RulesetVersion ||
                facts.CareerRandomAlgorithmVersion != pending.CareerRandomAlgorithmVersion ||
                facts.MatchSimulationVersion != pending.MatchSimulationVersion ||
                facts.MatchRandomAlgorithmVersion != pending.MatchRandomAlgorithmVersion)
            {
                throw new ArgumentException(
                    "Match facts versions must exactly match the frozen PendingMatch versions.",
                    nameof(completedFacts));
            }
        }

        private static void ValidateCompletedFacts(
            PendingCareerMatch pendingMatch,
            CareerMatchFacts completedFacts,
            CareerPlayerRecord currentPlayer)
        {
            if (completedFacts.Status != CareerMatchResultStatus.Completed)
            {
                throw new ArgumentException(
                    "Only completed match facts can produce settlement consequences.",
                    nameof(completedFacts));
            }

            if (completedFacts.SessionId != pendingMatch.SessionId ||
                !completedFacts.ContextDigest.Equals(pendingMatch.ContextDigest))
            {
                throw new ArgumentException(
                    "Match facts must match the frozen session and context digest.",
                    nameof(completedFacts));
            }

            if (!currentPlayer.PlayerId.Equals(pendingMatch.ProtagonistPlayerId))
            {
                throw new ArgumentException(
                    "The current Career player must be the frozen protagonist.",
                    nameof(currentPlayer));
            }

            if (completedFacts.PlayerFacts.Count != pendingMatch.OrderedPlayerIds.Count)
            {
                throw new ArgumentException(
                    "Match facts must contain the frozen ordered roster.",
                    nameof(completedFacts));
            }

            for (var index = 0; index < completedFacts.PlayerFacts.Count; index++)
            {
                if (!completedFacts.PlayerFacts[index].PlayerId.Equals(
                        pendingMatch.OrderedPlayerIds[index]))
                {
                    throw new ArgumentException(
                        "Match fact player IDs must exactly match the frozen roster order.",
                        nameof(completedFacts));
                }
            }

            var winner = completedFacts.WinnerTeamId.Value;
            if (!winner.Equals(pendingMatch.HomeTeamId) &&
                !winner.Equals(pendingMatch.AwayTeamId))
            {
                throw new ArgumentException(
                    "The completed winner must be one of the frozen teams.",
                    nameof(completedFacts));
            }
        }

        private static int FindProtagonistIndex(PendingCareerMatch pendingMatch)
        {
            var found = -1;
            for (var index = 0; index < pendingMatch.OrderedPlayerIds.Count; index++)
            {
                if (!pendingMatch.OrderedPlayerIds[index].Equals(
                        pendingMatch.ProtagonistPlayerId))
                {
                    continue;
                }

                if (found >= 0)
                {
                    throw new ArgumentException(
                        "The frozen roster must contain one protagonist.",
                        nameof(pendingMatch));
                }

                found = index;
            }

            if (found < 0)
            {
                throw new ArgumentException(
                    "The frozen roster must contain one protagonist.",
                    nameof(pendingMatch));
            }

            return found;
        }

        private static long[] CalculateBaseExperience(CareerMatchPlayerFacts facts)
        {
            var result = new long[8];
            checked
            {
                result[(int)CareerAttributeKind.Spike] =
                    (long)facts.Spike.Attempts * 2L +
                    (long)facts.Spike.Points * 6L +
                    facts.Spike.Errors;
                result[(int)CareerAttributeKind.Serve] =
                    (long)facts.Serve.Attempts * 2L +
                    (long)facts.Serve.Aces * 8L +
                    facts.Serve.Errors;
                result[(int)CareerAttributeKind.Reception] =
                    (long)facts.Reception.Perfect * 6L +
                    (long)facts.Reception.Positive * 4L +
                    (long)facts.Reception.Neutral * 2L +
                    facts.Reception.Negative +
                    facts.Reception.Errors;
                result[(int)CareerAttributeKind.Defense] =
                    facts.Defense.Attempts +
                    (long)facts.Defense.Successes * 4L;
                result[(int)CareerAttributeKind.Block] =
                    facts.Block.Attempts +
                    (long)facts.Block.EffectiveTouches * 3L +
                    (long)facts.Block.Points * 6L;
                result[(int)CareerAttributeKind.Movement] =
                    CeilDiv(facts.Load.MovementDistanceMillimeters, 10000L);
                result[(int)CareerAttributeKind.Jump] =
                    facts.Load.JumpCount +
                    (long)facts.Load.HighLoadJumpCount * 2L +
                    CeilDiv(facts.Load.LandingLoadBasisPoints, 1000L);
                result[(int)CareerAttributeKind.Stamina] =
                    facts.Load.RalliesPlayed +
                    CeilDiv(facts.Load.TotalWorkloadBasisPoints, 250L) +
                    CeilDiv(facts.Load.ActiveDurationMilliseconds, 60000L);
            }

            for (var index = 0; index < result.Length; index++)
            {
                RequireSafeNonNegative(result[index], "baseExperience");
            }

            return result;
        }

        private static CareerAttributeGrowthChange[] CalculateGrowthChanges(
            PendingCareerMatch pendingMatch,
            CareerPlayerAttributes currentAttributes,
            IReadOnlyList<long> baseExperience,
            int potentialMultiplierBasisPoints)
        {
            var changes = new CareerAttributeGrowthChange[8];
            for (var index = 0; index < changes.Length; index++)
            {
                var attribute = (CareerAttributeKind)index;
                var direction = DirectionFor(attribute);
                var afterPotential = FloorMultiplyDivide(
                    baseExperience[index],
                    potentialMultiplierBasisPoints,
                    BasisPointScale);
                var bonusBasisPoints = checked(
                    FrozenEmphasisBasisPoints(pendingMatch, direction) +
                    PriorityGrowthBasisPoints(pendingMatch.PreMatchPriority, attribute));
                var requested = FloorMultiplyDivide(
                    afterPotential,
                    checked(BasisPointScale + bonusBasisPoints),
                    BasisPointScale);
                RequireSafeNonNegative(afterPotential, nameof(afterPotential));
                RequireSafeNonNegative(requested, nameof(requested));

                var before = currentAttributes.Get(attribute);
                var available = CareerAttributeProgress.MaximumGrowthExperience -
                                before.GrowthExperience;
                var actual = Math.Min(requested, available);
                var after = new CareerAttributeProgress(
                    before.AbilityBasisPoints,
                    checked(before.GrowthExperience + actual));
                changes[index] = new CareerAttributeGrowthChange(
                    attribute,
                    GrowthReasonIds[index],
                    before,
                    requested,
                    actual,
                    after);
            }

            return changes;
        }

        private static CareerTrainingDirection DirectionFor(CareerAttributeKind attribute)
        {
            switch (attribute)
            {
                case CareerAttributeKind.Spike: return CareerTrainingDirection.Spike;
                case CareerAttributeKind.Serve: return CareerTrainingDirection.Serve;
                case CareerAttributeKind.Reception: return CareerTrainingDirection.Reception;
                case CareerAttributeKind.Defense: return CareerTrainingDirection.Defense;
                case CareerAttributeKind.Block: return CareerTrainingDirection.Block;
                case CareerAttributeKind.Movement: return CareerTrainingDirection.Movement;
                case CareerAttributeKind.Jump: return CareerTrainingDirection.Jump;
                case CareerAttributeKind.Stamina: return CareerTrainingDirection.Stamina;
                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null);
            }
        }

        private static int FrozenEmphasisBasisPoints(
            PendingCareerMatch pendingMatch,
            CareerTrainingDirection direction)
        {
            for (var index = 0; index < pendingMatch.FrozenTrainingEmphases.Count; index++)
            {
                var emphasis = pendingMatch.FrozenTrainingEmphases[index];
                if (emphasis.Direction == direction)
                {
                    return emphasis.TotalBonusBasisPoints;
                }
            }

            return 0;
        }

        private static int PriorityGrowthBasisPoints(
            CareerMatchPriority priority,
            CareerAttributeKind attribute)
        {
            switch (priority)
            {
                case CareerMatchPriority.AttackFirst:
                    return attribute == CareerAttributeKind.Spike ||
                           attribute == CareerAttributeKind.Serve
                        ? 1000
                        : 0;
                case CareerMatchPriority.FirstContactSecurity:
                    return attribute == CareerAttributeKind.Reception ||
                           attribute == CareerAttributeKind.Defense
                        ? 1000
                        : 0;
                case CareerMatchPriority.StaminaControl:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(priority), priority, null);
            }
        }

        private static int CalculateRequestedFatigue(
            CareerMatchPriority priority,
            CareerMatchLoadFacts load,
            int staminaAbilityBasisPoints)
        {
            var staminaFactor = checked(20000L - staminaAbilityBasisPoints);
            var effectiveWorkload = CeilMultiplyDivide(
                load.TotalWorkloadBasisPoints,
                staminaFactor,
                BasisPointScale);
            var requested = CeilDiv(effectiveWorkload, 1000L);
            if (priority == CareerMatchPriority.StaminaControl)
            {
                requested = CeilMultiplyDivide(requested, 8000L, BasisPointScale);
            }

            return checked((int)requested);
        }

        private static int CalculateRequestedMindset(
            bool won,
            CareerStabilityFacts stability)
        {
            var difference = checked(
                (long)stability.CriticalSuccesses - stability.CriticalErrors);
            var criticalTerm = (int)Math.Max(-2L, Math.Min(2L, difference));
            var streakPenalty = checked(
                Math.Min(2, stability.ErrorStreakEpisodes) +
                (stability.LongestErrorStreak >= 3 ? 1 : 0));
            return checked((won ? 4 : -3) + criticalTerm - streakPenalty);
        }

        private static bool IsPriorityExecuted(
            CareerMatchPriority priority,
            CareerMatchPlayerFacts facts)
        {
            checked
            {
                switch (priority)
                {
                    case CareerMatchPriority.AttackFirst:
                        return (long)facts.Spike.Points + facts.Serve.Aces >
                               (long)facts.Spike.Errors + facts.Serve.Errors;
                    case CareerMatchPriority.FirstContactSecurity:
                        return (long)facts.Reception.Perfect +
                               facts.Reception.Positive +
                               facts.Defense.Successes >=
                               (long)facts.Reception.Negative +
                               facts.Reception.Errors +
                               (facts.Defense.Attempts - facts.Defense.Successes);
                    case CareerMatchPriority.StaminaControl:
                        return facts.Load.TotalWorkloadBasisPoints <= 7500;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(priority), priority, null);
                }
            }
        }

        private static int CalculateRequestedCoachTrust(
            bool won,
            bool priorityExecuted,
            CareerStabilityFacts stability)
        {
            var stabilityTerm = stability.CriticalSuccesses > stability.CriticalErrors
                ? 1
                : stability.CriticalSuccesses < stability.CriticalErrors ? -1 : 0;
            return checked(
                (priorityExecuted ? 3 : -2) +
                stabilityTerm +
                (won ? 1 : 0));
        }

        private static CareerReasonedIntegerChange StatusChange(
            string reasonId,
            int oldValue,
            int requestedDelta)
        {
            var unclamped = checked(oldValue + requestedDelta);
            var newValue = Math.Max(0, Math.Min(100, unclamped));
            return new CareerReasonedIntegerChange(
                reasonId,
                oldValue,
                requestedDelta,
                newValue - oldValue,
                newValue);
        }

        private static CareerMatchSetScoreSummary[] CopySets(CareerMatchFacts facts)
        {
            var result = new CareerMatchSetScoreSummary[facts.Sets.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var set = facts.Sets[index];
                result[index] = new CareerMatchSetScoreSummary(
                    set.SetNumber,
                    set.HomePoints,
                    set.AwayPoints,
                    set.IsComplete);
            }

            return result;
        }

        private static CareerProtagonistMatchFacts CopyProtagonistFacts(
            CareerMatchPlayerFacts facts)
        {
            return new CareerProtagonistMatchFacts(
                new CareerSpikeFactSummary(
                    facts.Spike.Attempts,
                    facts.Spike.Points,
                    facts.Spike.Errors),
                new CareerServeFactSummary(
                    facts.Serve.Attempts,
                    facts.Serve.Aces,
                    facts.Serve.Errors),
                new CareerReceptionFactSummary(
                    facts.Reception.Attempts,
                    facts.Reception.Perfect,
                    facts.Reception.Positive,
                    facts.Reception.Neutral,
                    facts.Reception.Negative,
                    facts.Reception.Errors),
                new CareerDefenseFactSummary(
                    facts.Defense.Attempts,
                    facts.Defense.Successes),
                new CareerBlockFactSummary(
                    facts.Block.Attempts,
                    facts.Block.EffectiveTouches,
                    facts.Block.Points),
                new CareerMatchLoadFactSummary(
                    facts.Load.RalliesPlayed,
                    facts.Load.ActiveDurationMilliseconds,
                    facts.Load.MovementDistanceMillimeters,
                    facts.Load.JumpCount,
                    facts.Load.HighLoadJumpCount,
                    facts.Load.LandingLoadBasisPoints,
                    facts.Load.TotalWorkloadBasisPoints),
                new CareerStabilityFactSummary(
                    facts.Stability.CriticalActions,
                    facts.Stability.CriticalSuccesses,
                    facts.Stability.CriticalErrors,
                    facts.Stability.ErrorStreakEpisodes,
                    facts.Stability.LongestErrorStreak));
        }

        private static long FloorMultiplyDivide(
            long value,
            long numerator,
            long denominator)
        {
            RequireScaleInputs(value, numerator, denominator);
            checked
            {
                return (value / denominator) * numerator +
                       ((value % denominator) * numerator) / denominator;
            }
        }

        private static long CeilMultiplyDivide(
            long value,
            long numerator,
            long denominator)
        {
            RequireScaleInputs(value, numerator, denominator);
            checked
            {
                return (value / denominator) * numerator +
                       CeilDiv((value % denominator) * numerator, denominator);
            }
        }

        private static long CeilDiv(long value, long denominator)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }

            if (denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator), denominator, null);
            }

            return value == 0 ? 0 : checked(1L + (value - 1L) / denominator);
        }

        private static void RequireScaleInputs(
            long value,
            long numerator,
            long denominator)
        {
            RequireSafeNonNegative(value, nameof(value));
            if (numerator < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numerator), numerator, null);
            }

            if (denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator), denominator, null);
            }
        }

        private static void RequireSafeNonNegative(long value, string parameterName)
        {
            if (value < 0 || value > MaximumSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "The value must be a non-negative I-JSON safe integer.");
            }
        }

        private static void ValidateStatus(int value, string parameterName)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Career status must be in [0, 100].");
            }
        }
    }
}

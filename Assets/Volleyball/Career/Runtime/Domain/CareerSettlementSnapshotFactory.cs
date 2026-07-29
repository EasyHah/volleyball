using System;
using System.Collections.Generic;

namespace Volleyball.Career.Domain
{
    public static class CareerSettlementSnapshotFactory
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";

        public static CareerSaveSnapshot SettleFirstMatch(
            CareerSaveSnapshot prior,
            long settledAtUtcMs,
            WeekPlanId weekTwoPlanId,
            Sha256Digest resultDigest,
            byte[] canonicalResultUtf8,
            CareerSettlementSummary summary)
        {
            if (prior == null)
            {
                throw new ArgumentNullException(nameof(prior));
            }

            if (prior.PendingMatch == null)
            {
                throw new ArgumentException(
                    "An active PendingMatch is required.",
                    nameof(prior));
            }

            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            var pending = prior.PendingMatch;
            var revision = checked(prior.Identity.Revision + 1);
            var identity = new CareerSaveIdentity(
                prior.Identity.ProfileId,
                prior.Identity.SaveId,
                prior.Identity.LineageId,
                revision,
                prior.Identity.CreatedAtUtcMs,
                settledAtUtcMs,
                new Sha256Digest(ZeroHash),
                prior.Identity.RestoredFromVersionToken);
            var historyEntry = new CareerMatchHistoryEntry(
                pending.SessionId,
                pending.ScheduleItemId,
                pending.SourceWeekPlanId,
                pending.SourceSlotActionId,
                pending.ContextDigest,
                resultDigest,
                pending.CanonicalContextUtf8,
                canonicalResultUtf8,
                identity.LineageId,
                identity.Revision,
                settledAtUtcMs,
                summary);
            var settlementReceipt = new CareerSettlementReceipt(
                historyEntry.SessionId,
                historyEntry.ContextDigest,
                historyEntry.ResultDigest,
                historyEntry.AppliedLineageId,
                historyEntry.AppliedRevision,
                historyEntry.SettledAtUtcMs,
                summary);
            var history = new List<CareerMatchHistoryEntry>(prior.MatchHistory)
            {
                historyEntry
            };
            var receipts = new List<CareerSettlementReceipt>(prior.SettlementReceipts)
            {
                settlementReceipt
            };
            var weekTwo = new CareerWeekPlanState(
                weekTwoPlanId,
                1,
                2,
                new CareerWeekActionState[] { null, null, null },
                false);
            var player = new CareerPlayerRecord(
                prior.Player.PlayerId,
                prior.Player.DisplayName,
                prior.Player.JerseyNumber,
                summary.AfterAttributes);

            return new CareerSaveSnapshot(
                prior.Versions,
                identity,
                prior.CareerSeed,
                prior.CareerName,
                prior.PlayerDraft,
                prior.Onboarding,
                CareerProgressionState.Planning(weekTwo),
                TrainingEmphasisLedger.Empty,
                player,
                prior.TeamId,
                prior.PotentialGrade,
                summary.WeekendFatigueChange.NewValue,
                summary.WeekendMindsetChange.NewValue,
                summary.WeekendCoachTrustChange.NewValue,
                prior.OperationReceipts,
                null,
                history,
                receipts);
        }
    }
}

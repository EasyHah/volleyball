using System;
using System.Collections.Generic;

namespace Volleyball.Career.Domain
{
    public static class CareerWeekSnapshotFactory
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";

        public static CareerSaveSnapshot Advance(
            CareerSaveSnapshot prior,
            long updatedAtUtcMs,
            CareerProgressionState progression,
            TrainingEmphasisLedger trainingEmphases,
            CareerPlayerAttributes playerAttributes,
            int fatigue,
            int mindset,
            int coachTrust,
            IEnumerable<OperationReceipt> receipts)
        {
            return Advance(
                prior,
                updatedAtUtcMs,
                progression,
                trainingEmphases,
                playerAttributes,
                fatigue,
                mindset,
                coachTrust,
                receipts,
                prior == null ? null : prior.PendingMatch);
        }

        public static CareerSaveSnapshot Advance(
            CareerSaveSnapshot prior,
            long updatedAtUtcMs,
            CareerProgressionState progression,
            TrainingEmphasisLedger trainingEmphases,
            CareerPlayerAttributes playerAttributes,
            int fatigue,
            int mindset,
            int coachTrust,
            IEnumerable<OperationReceipt> receipts,
            PendingCareerMatch pendingMatch)
        {
            if (prior == null)
            {
                throw new ArgumentNullException(nameof(prior));
            }

            if (playerAttributes == null)
            {
                throw new ArgumentNullException(nameof(playerAttributes));
            }

            var player = new CareerPlayerRecord(
                prior.Player.PlayerId,
                prior.Player.DisplayName,
                prior.Player.JerseyNumber,
                playerAttributes);
            return new CareerSaveSnapshot(
                prior.Versions,
                new CareerSaveIdentity(
                    prior.Identity.ProfileId,
                    prior.Identity.SaveId,
                    prior.Identity.LineageId,
                    checked(prior.Identity.Revision + 1),
                    prior.Identity.CreatedAtUtcMs,
                    updatedAtUtcMs,
                    new Sha256Digest(ZeroHash),
                    prior.Identity.RestoredFromVersionToken),
                prior.CareerSeed,
                prior.CareerName,
                prior.PlayerDraft,
                prior.Onboarding,
                progression,
                trainingEmphases,
                player,
                prior.TeamId,
                prior.PotentialGrade,
                fatigue,
                mindset,
                coachTrust,
                receipts,
                pendingMatch,
                prior.MatchHistory,
                prior.SettlementReceipts);
        }
    }
}

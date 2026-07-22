using System;
using System.Collections.Generic;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    public static class CareerOnboardingSnapshotFactory
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";

        public static CareerSaveSnapshot CreateInitial(
            ProfileId profileId,
            SaveId saveId,
            LineageId lineageId,
            CareerSeed seed,
            string careerName,
            string playerStableId,
            string playerDisplayName,
            int jerseyNumber,
            TryoutOnboardingState onboarding,
            OperationReceipt createReceipt,
            long completedAtUtcMs)
        {
            if (createReceipt == null)
            {
                throw new ArgumentNullException(nameof(createReceipt));
            }

            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    profileId,
                    saveId,
                    lineageId,
                    1,
                    completedAtUtcMs,
                    completedAtUtcMs,
                    new Sha256Digest(ZeroHash)),
                seed,
                careerName,
                new CareerPlayerDraft(
                    new PlayerId(playerStableId),
                    playerDisplayName,
                    jerseyNumber),
                onboarding,
                CareerProgressionState.Created(),
                TrainingEmphasisLedger.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                new[] { createReceipt });
        }

        public static CareerSaveSnapshot Advance(
            CareerSaveSnapshot prior,
            long updatedAtUtcMs,
            TryoutOnboardingState onboarding,
            CareerProgressionState progression,
            CareerPlayerAttributes completeAttributes,
            string teamStableId,
            PotentialGrade? potentialGrade,
            int? fatigue,
            int? mindset,
            int? coachTrust,
            IEnumerable<OperationReceipt> receipts)
        {
            if (prior == null)
            {
                throw new ArgumentNullException(nameof(prior));
            }

            var player = completeAttributes == null
                ? null
                : new CareerPlayerRecord(
                    prior.PlayerDraft.PlayerId,
                    prior.PlayerDraft.DisplayName,
                    prior.PlayerDraft.JerseyNumber,
                    completeAttributes);
            TeamId? teamId = teamStableId == null
                ? (TeamId?)null
                : new TeamId(teamStableId);
            return new CareerSaveSnapshot(
                prior.Versions,
                new CareerSaveIdentity(
                    prior.Identity.ProfileId,
                    prior.Identity.SaveId,
                    prior.Identity.LineageId,
                    prior.Identity.Revision + 1,
                    prior.Identity.CreatedAtUtcMs,
                    updatedAtUtcMs,
                    new Sha256Digest(ZeroHash),
                    prior.Identity.RestoredFromVersionToken),
                prior.CareerSeed,
                prior.CareerName,
                prior.PlayerDraft,
                onboarding,
                progression,
                prior.TrainingEmphases,
                player,
                teamId,
                potentialGrade,
                fatigue,
                mindset,
                coachTrust,
                receipts);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Presentation
{
    public sealed class CareerUiPreMatchPlayer
    {
        public CareerUiPreMatchPlayer(
            int jerseyNumber,
            CareerMatchPlayerPosition position,
            bool isProtagonist)
        {
            JerseyNumber = jerseyNumber;
            Position = position;
            IsProtagonist = isProtagonist;
        }

        public int JerseyNumber { get; }
        public CareerMatchPlayerPosition Position { get; }
        public bool IsProtagonist { get; }
    }

    public sealed class CareerUiPreMatchPreview
    {
        private readonly ReadOnlyCollection<CareerUiPreMatchPlayer> _homePlayers;
        private readonly ReadOnlyCollection<CareerUiPreMatchPlayer> _awayPlayers;

        public CareerUiPreMatchPreview(
            string homeTeamId,
            string awayTeamId,
            IEnumerable<CareerUiPreMatchPlayer> homePlayers,
            IEnumerable<CareerUiPreMatchPlayer> awayPlayers)
        {
            HomeTeamId = string.IsNullOrWhiteSpace(homeTeamId) ? "unknown.home" : homeTeamId;
            AwayTeamId = string.IsNullOrWhiteSpace(awayTeamId) ? "unknown.away" : awayTeamId;
            _homePlayers = Array.AsReadOnly(new List<CareerUiPreMatchPlayer>(
                homePlayers ?? Array.Empty<CareerUiPreMatchPlayer>()).ToArray());
            _awayPlayers = Array.AsReadOnly(new List<CareerUiPreMatchPlayer>(
                awayPlayers ?? Array.Empty<CareerUiPreMatchPlayer>()).ToArray());
        }

        public string HomeTeamId { get; }
        public string AwayTeamId { get; }
        public IReadOnlyList<CareerUiPreMatchPlayer> HomePlayers => _homePlayers;
        public IReadOnlyList<CareerUiPreMatchPlayer> AwayPlayers => _awayPlayers;
    }

    public sealed class CareerUiUseCaseResult
    {
        private readonly ReadOnlyCollection<LocalProfileCatalogEntry> _profiles;

        private CareerUiUseCaseResult(
            bool succeeded,
            string code,
            IEnumerable<LocalProfileCatalogEntry> profiles,
            LocalPlayerProfile profile,
            CareerSaveSnapshot snapshot,
            CareerSettlementReceipt settlementReceipt)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? "unknown" : code;
            _profiles = Array.AsReadOnly(profiles == null
                ? Array.Empty<LocalProfileCatalogEntry>()
                : new List<LocalProfileCatalogEntry>(profiles).ToArray());
            Profile = profile;
            Snapshot = snapshot;
            SettlementReceipt = settlementReceipt;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public IReadOnlyList<LocalProfileCatalogEntry> Profiles => _profiles;
        public LocalPlayerProfile Profile { get; }
        public CareerSaveSnapshot Snapshot { get; }
        public CareerSettlementReceipt SettlementReceipt { get; }

        public static CareerUiUseCaseResult ForProfiles(
            IEnumerable<LocalProfileCatalogEntry> profiles,
            string code = "loaded") =>
            new CareerUiUseCaseResult(true, code, profiles, null, null, null);

        public static CareerUiUseCaseResult ForProfile(
            LocalPlayerProfile profile,
            IEnumerable<LocalProfileCatalogEntry> profiles = null,
            string code = "loaded") =>
            new CareerUiUseCaseResult(
                profile != null,
                profile == null ? "missing_profile" : code,
                profiles,
                profile,
                null,
                null);

        public static CareerUiUseCaseResult ForCareer(
            CareerSaveSnapshot snapshot,
            string code = "loaded") =>
            new CareerUiUseCaseResult(
                snapshot != null,
                snapshot == null ? "missing_career" : code,
                null,
                null,
                snapshot,
                null);

        public static CareerUiUseCaseResult ForSettlement(
            CareerSaveSnapshot snapshot,
            CareerSettlementReceipt receipt,
            string code = "settled") =>
            new CareerUiUseCaseResult(
                snapshot != null && receipt != null,
                snapshot == null || receipt == null ? "missing_settlement" : code,
                null,
                null,
                snapshot,
                receipt);

        public static CareerUiUseCaseResult Failure(
            string code,
            CareerSaveSnapshot snapshot = null) =>
            new CareerUiUseCaseResult(false, code, null, null, snapshot, null);
    }

    public interface ICareerUiUseCases
    {
        CareerUiUseCaseResult LoadProfiles();
        CareerUiUseCaseResult CreateProfile(string displayName);
        CareerUiUseCaseResult LoadProfile(ProfileId profileId);
        CareerUiUseCaseResult LoadCareer(ProfileId profileId, SaveId saveId);
        CareerUiUseCaseResult CreateCareer(
            ProfileId profileId,
            string careerName,
            string playerName,
            int jerseyNumber);
        CareerUiUseCaseResult ConfirmTryout(
            CareerSaveSnapshot snapshot,
            string choiceId);
        CareerUiUseCaseResult ConfirmWeekPlan(
            CareerSaveSnapshot snapshot,
            string firstContentId,
            string secondContentId);
        CareerUiUseCaseResult ExecuteNextAction(CareerSaveSnapshot snapshot);
        CareerUiUseCaseResult ResolveEvent(
            CareerSaveSnapshot snapshot,
            string optionId);
        CareerUiPreMatchPreview GetPreMatchPreview(CareerSaveSnapshot snapshot);
        Task<CareerUiUseCaseResult> PlayAndSettleAsync(
            CareerSaveSnapshot snapshot,
            CareerMatchPriority priority,
            CancellationToken cancellationToken);
        CareerUiUseCaseResult SaveNow(CareerSaveSnapshot snapshot);
    }
}

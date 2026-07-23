using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Presentation;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerUiSessionControllerTests
    {
        private static readonly ProfileId Profile =
            new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        private static readonly SaveId Save =
            new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        [Test]
        public void AuthorityRoutesPendingMatchAndBlocksBack()
        {
            var useCases = new StubUseCases
            {
                LoadCareerResult = CareerUiUseCaseResult.ForCareer(
                    CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot())
            };
            var controller = new CareerUiSessionController(useCases);

            controller.SelectCareer(Profile, Save);

            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.PreMatch));
            Assert.That(controller.Back(), Is.False);
            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.PreMatch));
            Assert.That(controller.FeedbackCode, Is.EqualTo("pending_match_requires_retry"));
        }

        [Test]
        public async Task SuccessfulMatchUsesSummaryAndNoticeAsNavigationOnly()
        {
            var matchReady = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var settled = CareerSaveV2LifecycleTestData.SettledSnapshot();
            var useCases = new StubUseCases
            {
                LoadCareerResult = CareerUiUseCaseResult.ForCareer(matchReady),
                MatchResult = CareerUiUseCaseResult.ForSettlement(
                    settled,
                    settled.SettlementReceipts[0])
            };
            var controller = new CareerUiSessionController(useCases);
            controller.SelectCareer(Profile, Save);
            controller.OpenPreMatch();

            await controller.PlayAndSettleAsync(
                CareerMatchPriority.AttackFirst,
                CancellationToken.None);

            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.MatchSummary));
            Assert.That(controller.Snapshot.Identity.Revision,
                Is.EqualTo(settled.Identity.Revision));
            Assert.That(controller.ContinueFromMatchSummary(), Is.True);
            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.WeekendNotice));
            Assert.That(controller.CloseWeekendNotice(), Is.True);
            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.WeekHome));
            Assert.That(useCases.PlayCount, Is.EqualTo(1));
            Assert.That(useCases.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousWriteExceptionReleasesBusyState()
        {
            var useCases = new StubUseCases
            {
                LoadCareerResult = CareerUiUseCaseResult.ForCareer(
                    CareerSaveV2LifecycleTestData.MatchReadySnapshot()),
                SaveThrows = true
            };
            var controller = new CareerUiSessionController(useCases);
            controller.SelectCareer(Profile, Save);

            Assert.That(controller.SaveNow(), Is.False);

            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.SaveState, Is.EqualTo(CareerUiSaveState.Failed));
            Assert.That(controller.FeedbackCode, Does.StartWith("save_now_exception_"));
        }

        [Test]
        public void FailedWriteAdoptsReturnedAuthoritativeSnapshot()
        {
            var initial = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var authoritative = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var useCases = new StubUseCases
            {
                LoadCareerResult = CareerUiUseCaseResult.ForCareer(initial),
                SaveResult = CareerUiUseCaseResult.Failure(
                    "version_conflict",
                    authoritative)
            };
            var controller = new CareerUiSessionController(useCases);
            controller.SelectCareer(Profile, Save);

            Assert.That(controller.SaveNow(), Is.False);

            Assert.That(controller.Snapshot.Identity.VersionToken,
                Is.EqualTo(authoritative.Identity.VersionToken));
            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.PreMatch));
            Assert.That(controller.SaveState, Is.EqualTo(CareerUiSaveState.Failed));
        }

        [Test]
        public async Task MatchExceptionReloadsCommittedSettlementAuthority()
        {
            var initial = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var settled = CareerSaveV2LifecycleTestData.SettledSnapshot();
            var useCases = new StubUseCases
            {
                LoadCareerResult = CareerUiUseCaseResult.ForCareer(initial),
                MatchThrows = true
            };
            var controller = new CareerUiSessionController(useCases);
            controller.SelectCareer(Profile, Save);
            controller.OpenPreMatch();
            useCases.LoadCareerResult = CareerUiUseCaseResult.ForCareer(settled);

            Assert.That(await controller.PlayAndSettleAsync(
                CareerMatchPriority.AttackFirst,
                CancellationToken.None), Is.False);

            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.Identity.VersionToken,
                Is.EqualTo(settled.Identity.VersionToken));
            Assert.That(controller.SettlementReceipt, Is.Not.Null);
            Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.MatchSummary));
            Assert.That(controller.SaveState, Is.EqualTo(CareerUiSaveState.Failed));
        }

        private sealed class StubUseCases : ICareerUiUseCases
        {
            public CareerUiUseCaseResult LoadCareerResult { get; set; }
            public CareerUiUseCaseResult MatchResult { get; set; }
            public int PlayCount { get; private set; }
            public int WriteCount { get; private set; }
            public bool SaveThrows { get; set; }
            public bool MatchThrows { get; set; }
            public CareerUiUseCaseResult SaveResult { get; set; }

            public CareerUiUseCaseResult LoadProfiles() =>
                CareerUiUseCaseResult.ForProfiles(Array.Empty<LocalProfileCatalogEntry>());

            public CareerUiUseCaseResult CreateProfile(string displayName)
            {
                WriteCount++;
                return CareerUiUseCaseResult.Failure("unused");
            }

            public CareerUiUseCaseResult LoadProfile(ProfileId profileId) =>
                CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult LoadCareer(ProfileId profileId, SaveId saveId) =>
                LoadCareerResult;

            public CareerUiUseCaseResult CreateCareer(
                ProfileId profileId,
                string careerName,
                string playerName,
                int jerseyNumber)
            {
                WriteCount++;
                return CareerUiUseCaseResult.Failure("unused");
            }

            public CareerUiUseCaseResult ConfirmTryout(
                CareerSaveSnapshot snapshot,
                string choiceId)
            {
                WriteCount++;
                return CareerUiUseCaseResult.Failure("unused");
            }

            public CareerUiUseCaseResult ConfirmWeekPlan(
                CareerSaveSnapshot snapshot,
                string firstContentId,
                string secondContentId)
            {
                WriteCount++;
                return CareerUiUseCaseResult.Failure("unused");
            }

            public CareerUiUseCaseResult ExecuteNextAction(CareerSaveSnapshot snapshot)
            {
                WriteCount++;
                return CareerUiUseCaseResult.Failure("unused");
            }

            public CareerUiUseCaseResult ResolveEvent(
                CareerSaveSnapshot snapshot,
                string optionId)
            {
                WriteCount++;
                return CareerUiUseCaseResult.Failure("unused");
            }

            public CareerUiPreMatchPreview GetPreMatchPreview(
                CareerSaveSnapshot snapshot) =>
                new CareerUiPreMatchPreview(
                    "team.university.player",
                    "team.university.rival",
                    Array.Empty<CareerUiPreMatchPlayer>(),
                    Array.Empty<CareerUiPreMatchPlayer>());

            public Task<CareerUiUseCaseResult> PlayAndSettleAsync(
                CareerSaveSnapshot snapshot,
                CareerMatchPriority priority,
                CancellationToken cancellationToken)
            {
                PlayCount++;
                WriteCount++;
                if (MatchThrows)
                {
                    throw new InvalidOperationException("simulated after commit");
                }

                return Task.FromResult(MatchResult);
            }

            public CareerUiUseCaseResult SaveNow(CareerSaveSnapshot snapshot)
            {
                WriteCount++;
                if (SaveThrows)
                {
                    throw new InvalidOperationException("simulated");
                }

                return SaveResult ??
                       CareerUiUseCaseResult.ForCareer(snapshot, "up_to_date");
            }
        }
    }
}

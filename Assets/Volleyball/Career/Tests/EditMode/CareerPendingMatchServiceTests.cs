using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerPendingMatchServiceTests
    {
        [Test]
        public async Task Create_CommitsPendingBeforeExecutingPersistedBytes()
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var random = new SpyRandom(calls, 123456789);
            var factory = new SpyLaunchFactory(calls);
            var executor = new SpyExecutor(calls);
            var service = new CareerPendingMatchService(repository, random, factory, executor);
            var command = Command(snapshot);

            var result = await service.CreateAndExecuteAsync(command, CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(CareerPendingMatchFlowStatus.AwaitingSettlement));
            Assert.That(result.CreationDisposition, Is.EqualTo(CareerPendingCreationDisposition.Created));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(9));
            Assert.That(result.Snapshot.Progression.Kind, Is.EqualTo(CareerProgressionKind.AwaitingMatch));
            Assert.That(result.Snapshot.PendingMatch.SessionId, Is.EqualTo(command.SessionId));
            Assert.That(result.Snapshot.PendingMatch.MatchSeed, Is.EqualTo(123456789u));
            Assert.That(result.Snapshot.PendingMatch.ContextDigest,
                Is.EqualTo(executor.EncodedContext.ContextDigest));
            Assert.That(result.Snapshot.PendingMatch.CanonicalContextUtf8,
                Is.EqualTo(executor.EncodedContext.CanonicalContextUtf8));
            Assert.That(result.Snapshot.PendingMatch.SourceWeekPlanId, Is.EqualTo(command.WeekPlanId));
            Assert.That(result.Snapshot.PendingMatch.SourceSlotActionId, Is.EqualTo(command.SlotActionId));
            Assert.That(result.Snapshot.PendingMatch.SourceActionOccurrenceId,
                Is.EqualTo(command.ActionOccurrenceId));
            Assert.That(result.Snapshot.PendingMatch.PreMatchPriority,
                Is.EqualTo(command.PreMatchPriority));
            Assert.That(result.Snapshot.PendingMatch.FrozenTrainingEmphases.Count, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(new[] { "load", "random", "catalog", "encode", "commit", "execute" }));
            Assert.That(executor.ExecutedContext.CanonicalContextUtf8,
                Is.EqualTo(repository.Committed.PendingMatch.CanonicalContextUtf8));
            Assert.That(executor.ExecutedContext, Is.Not.SameAs(executor.EncodedContext));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(executor.EncodeCount, Is.EqualTo(1));
            Assert.That(executor.ExecuteCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Retry_UsesOnlyPersistedBytesAndNeverMutates()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var random = new SpyRandom(calls, 1);
            var factory = new SpyLaunchFactory(calls);
            var executor = new SpyExecutor(calls);
            var service = new CareerPendingMatchService(repository, random, factory, executor);

            var result = await service.RetryExecutionAsync(
                new RetryPendingMatchExecutionCommand(
                    snapshot.Identity.ProfileId,
                    snapshot.Identity.SaveId,
                    snapshot.PendingMatch.SessionId),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(CareerPendingMatchFlowStatus.AwaitingSettlement));
            Assert.That(calls, Is.EqualTo(new[] { "load", "execute" }));
            Assert.That(executor.ExecutedContext.CanonicalContextUtf8,
                Is.EqualTo(snapshot.PendingMatch.CanonicalContextUtf8));
            Assert.That(repository.CommitCount, Is.Zero);
            Assert.That(random.Count, Is.Zero);
            Assert.That(factory.Count, Is.Zero);
            Assert.That(executor.EncodeCount, Is.Zero);
        }

        [Test]
        public async Task ExactOperationRetry_ReturnsExistingBeforeVersionCheckOrExecution()
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var random = new SpyRandom(calls, 99);
            var factory = new SpyLaunchFactory(calls);
            var executor = new SpyExecutor(calls);
            var service = new CareerPendingMatchService(repository, random, factory, executor);
            var command = Command(snapshot);
            var first = await service.CreateAndExecuteAsync(command, CancellationToken.None);
            calls.Clear();
            repository.Current = first.Snapshot;
            var staleSameRequest = new CreatePendingMatchCommand(
                command.ProfileId,
                command.SaveId,
                command.ExpectedVersionToken,
                command.OperationId,
                command.CompletedAtUtcMs + 1,
                command.SessionId,
                command.WeekPlanId,
                command.SlotActionId,
                command.ActionOccurrenceId,
                command.PreMatchPriority);

            var result = await service.CreateAndExecuteAsync(staleSameRequest, CancellationToken.None);

            Assert.That(result.CreationDisposition, Is.EqualTo(CareerPendingCreationDisposition.Existing));
            Assert.That(calls, Is.EqualTo(new[] { "load" }));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(executor.ExecuteCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CommitFailure_DoesNotExecuteOrReportCommittedRevision()
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls)
            {
                CommitFailureKind = PersistenceResultKind.NotCommitted
            };
            var executor = new SpyExecutor(calls);
            var service = new CareerPendingMatchService(
                repository,
                new SpyRandom(calls, 123),
                new SpyLaunchFactory(calls),
                executor);

            var result = await service.CreateAndExecuteAsync(
                Command(snapshot),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(CareerPendingMatchFlowStatus.ValidationFailed));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.NotCommitted));
            Assert.That(result.CommittedRevision, Is.Null);
            Assert.That(repository.Current, Is.SameAs(snapshot));
            Assert.That(repository.Current.PendingMatch, Is.Null);
            Assert.That(executor.ExecuteCount, Is.Zero);
            Assert.That(calls, Is.EqualTo(
                new[] { "load", "random", "catalog", "encode", "commit" }));
        }

        [TestCase(false, CareerPendingMatchFlowStatus.ExecutionFailed)]
        [TestCase(true, CareerPendingMatchFlowStatus.Cancelled)]
        public async Task ExecutionFailureOrCancellation_PreservesRetryablePending(
            bool cancel,
            CareerPendingMatchFlowStatus expectedStatus)
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var executor = new SpyExecutor(calls)
            {
                ExecutionException = cancel
                    ? (Exception)new OperationCanceledException()
                    : new InvalidOperationException("runner failed")
            };
            var service = new CareerPendingMatchService(
                repository,
                new SpyRandom(calls, 123),
                new SpyLaunchFactory(calls),
                executor);
            var command = Command(snapshot);

            var result = await service.CreateAndExecuteAsync(command, CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.CreationDisposition,
                Is.EqualTo(CareerPendingCreationDisposition.Created));
            Assert.That(result.Snapshot.PendingMatch.SessionId, Is.EqualTo(command.SessionId));
            Assert.That(result.CommittedRevision, Is.EqualTo(9));
            Assert.That(repository.Current.PendingMatch.SessionId, Is.EqualTo(command.SessionId));
            Assert.That(repository.CommitCount, Is.EqualTo(1));

            executor.ExecutionException = null;
            calls.Clear();
            var retry = await service.RetryExecutionAsync(
                new RetryPendingMatchExecutionCommand(
                    command.ProfileId,
                    command.SaveId,
                    command.SessionId),
                CancellationToken.None);

            Assert.That(retry.Status, Is.EqualTo(CareerPendingMatchFlowStatus.AwaitingSettlement));
            Assert.That(calls, Is.EqualTo(new[] { "load", "execute" }));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [TestCase(CasConflictMode.Exact, CareerPendingMatchFlowStatus.AwaitingSettlement)]
        [TestCase(CasConflictMode.Conflict, CareerPendingMatchFlowStatus.OperationConflict)]
        [TestCase(CasConflictMode.Missing, CareerPendingMatchFlowStatus.RevisionConflict)]
        public async Task CasConflict_ReloadsReceiptOnlyAndNeverExecutes(
            CasConflictMode mode,
            CareerPendingMatchFlowStatus expectedStatus)
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls)
            {
                CasConflict = mode
            };
            var executor = new SpyExecutor(calls);
            var random = new SpyRandom(calls, 456);
            var factory = new SpyLaunchFactory(calls);
            var service = new CareerPendingMatchService(repository, random, factory, executor);

            var result = await service.CreateAndExecuteAsync(
                Command(snapshot),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(calls, Is.EqualTo(
                new[] { "load", "random", "catalog", "encode", "commit", "load" }));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Count, Is.EqualTo(1));
            Assert.That(factory.Count, Is.EqualTo(1));
            Assert.That(executor.EncodeCount, Is.EqualTo(1));
            Assert.That(executor.ExecuteCount, Is.Zero);
            if (mode == CasConflictMode.Exact)
            {
                Assert.That(result.CreationDisposition,
                    Is.EqualTo(CareerPendingCreationDisposition.Existing));
                Assert.That(result.CommittedRevision, Is.EqualTo(9));
            }
            else
            {
                Assert.That(result.CommittedRevision, Is.Null);
            }
        }

        [Test]
        public void Fingerprint_IncludesTokenSourceSessionAndPriorityButNotAuditTimestamp()
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var command = Command(snapshot);
            var timestampOnly = Copy(command, completedAtUtcMs: command.CompletedAtUtcMs + 1);

            Assert.That(CareerOperationFingerprintV2.Hash(timestampOnly),
                Is.EqualTo(CareerOperationFingerprintV2.Hash(command)));
            Assert.That(CareerOperationFingerprintV2.Hash(Copy(
                    command, priority: CareerMatchPriority.StaminaControl)),
                Is.Not.EqualTo(CareerOperationFingerprintV2.Hash(command)));
            Assert.That(CareerOperationFingerprintV2.Hash(Copy(
                    command, sessionId: Guid.Parse("99999999-9999-9999-9999-999999999999"))),
                Is.Not.EqualTo(CareerOperationFingerprintV2.Hash(command)));
        }

        [TestCase(1, new[] { 2, 1, 3, 4, 5, 6 })]
        [TestCase(2, new[] { 1, 2, 3, 4, 5, 6 })]
        [TestCase(6, new[] { 1, 6, 2, 3, 4, 5 })]
        [TestCase(99, new[] { 1, 99, 2, 3, 4, 5 })]
        public void FirstMatchFactory_AssignsDeterministicUniqueJerseys(
            int protagonistJersey,
            int[] expectedHomeJerseys)
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var launch = new CareerFirstMatchLaunchFactoryV1().Create(
                new CareerFirstMatchLaunchRequest(
                    CareerMatchTestData.Versions(),
                    CareerMatchTestData.SessionId,
                    CareerMatchTestData.MatchSeed,
                    snapshot.TeamId.Value,
                    snapshot.Player.PlayerId,
                    protagonistJersey,
                    snapshot.Fatigue.Value,
                    snapshot.Player.Attributes,
                    CareerMatchPriority.AttackFirst));

            Assert.That(
                launch.Teams[0].Players.Select(player => player.JerseyNumber),
                Is.EqualTo(expectedHomeJerseys));
            Assert.That(
                launch.Teams[1].Players.Select(player => player.JerseyNumber),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(launch.Teams[0].Players.Select(player => player.JerseyNumber).Distinct().Count(),
                Is.EqualTo(6));
        }

        [TestCase("dynamic.home.opposite")]
        [TestCase("dynamic.away.libero")]
        public void FirstMatchFactory_RebindsNpcIdWhenProtagonistUsesFixtureNamespace(
            string protagonistId)
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var request = new CareerFirstMatchLaunchRequest(
                CareerMatchTestData.Versions(),
                CareerMatchTestData.SessionId,
                CareerMatchTestData.MatchSeed,
                snapshot.TeamId.Value,
                new PlayerId(protagonistId),
                snapshot.Player.JerseyNumber,
                snapshot.Fatigue.Value,
                snapshot.Player.Attributes,
                CareerMatchPriority.AttackFirst);
            var factory = new CareerFirstMatchLaunchFactoryV1();

            var first = factory.Create(request);
            var second = factory.Create(request);
            var firstIds = first.Teams.SelectMany(team => team.Players)
                .Select(player => player.PlayerId.Value)
                .ToArray();

            Assert.That(firstIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(12));
            Assert.That(firstIds.Count(id => string.Equals(
                id,
                protagonistId,
                StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(
                second.Teams.SelectMany(team => team.Players)
                    .Select(player => player.PlayerId.Value),
                Is.EqualTo(firstIds));
        }

        [Test]
        public async Task ConcreteFirstMatchPipeline_ExecutesCommittedFixtureContext()
        {
            var snapshot = CareerSaveV2LifecycleTestData.MatchReadySnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var executor = new CareerMatchExecutorV4(
                new DeterministicFixtureMatchRunnerV4());
            var service = new CareerPendingMatchService(
                repository,
                new SpyRandom(calls, CareerMatchTestData.MatchSeed),
                new CareerFirstMatchLaunchFactoryV1(),
                executor);

            var result = await service.CreateAndExecuteAsync(
                Command(snapshot),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(CareerPendingMatchFlowStatus.AwaitingSettlement));
            Assert.That(result.CreationDisposition,
                Is.EqualTo(CareerPendingCreationDisposition.Created));
            Assert.That(result.Snapshot.PendingMatch.CanonicalContextUtf8,
                Is.EqualTo(result.CanonicalContextUtf8));
            Assert.That(result.CanonicalResultUtf8, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ResultDigest, Is.Not.Null);
        }

        private static CreatePendingMatchCommand Command(CareerSaveSnapshot snapshot)
        {
            var plan = snapshot.Progression.WeekPlan;
            var action = plan.Slots[2];
            return new CreatePendingMatchCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                new OperationId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                100,
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                plan.PlanId,
                action.SlotActionId,
                action.OccurrenceId,
                CareerMatchPriority.AttackFirst);
        }

        private static CreatePendingMatchCommand Copy(
            CreatePendingMatchCommand source,
            long? completedAtUtcMs = null,
            Guid? sessionId = null,
            CareerMatchPriority? priority = null)
        {
            return new CreatePendingMatchCommand(
                source.ProfileId,
                source.SaveId,
                source.ExpectedVersionToken,
                source.OperationId,
                completedAtUtcMs ?? source.CompletedAtUtcMs,
                sessionId ?? source.SessionId,
                source.WeekPlanId,
                source.SlotActionId,
                source.ActionOccurrenceId,
                priority ?? source.PreMatchPriority);
        }

        public enum CasConflictMode
        {
            None = 0,
            Exact = 1,
            Conflict = 2,
            Missing = 3
        }

        private sealed class MemoryRepository : ICareerSaveRepository
        {
            private readonly IList<string> _calls;

            public MemoryRepository(CareerSaveSnapshot current, IList<string> calls)
            {
                Current = current;
                _calls = calls;
            }

            public CareerSaveSnapshot Current { get; set; }
            public CareerSaveSnapshot Committed { get; private set; }
            public int CommitCount { get; private set; }
            public PersistenceResultKind? CommitFailureKind { get; set; }
            public CasConflictMode CasConflict { get; set; }

            public CareerPersistenceResult Load(ProfileId profileId, SaveId saveId)
            {
                _calls.Add("load");
                return new CareerPersistenceResult(PersistenceResultKind.Loaded, Current);
            }

            public CareerPersistenceResult Commit(
                ProfileId profileId,
                SaveId saveId,
                CareerVersionToken expectedVersionToken,
                CareerSaveSnapshot nextSnapshot,
                OperationId operationId)
            {
                _calls.Add("commit");
                CommitCount++;
                Committed = nextSnapshot;
                if (CommitFailureKind.HasValue)
                {
                    return new CareerPersistenceResult(CommitFailureKind.Value);
                }

                if (CasConflict != CasConflictMode.None)
                {
                    if (CasConflict == CasConflictMode.Exact)
                    {
                        Current = nextSnapshot;
                    }
                    else if (CasConflict == CasConflictMode.Conflict)
                    {
                        Current = WithConflictingReceipt(nextSnapshot);
                    }

                    return new CareerPersistenceResult(PersistenceResultKind.VersionConflict);
                }

                Current = nextSnapshot;
                return new CareerPersistenceResult(PersistenceResultKind.Committed, nextSnapshot);
            }

            private static CareerSaveSnapshot WithConflictingReceipt(CareerSaveSnapshot snapshot)
            {
                var receipts = snapshot.OperationReceipts.ToArray();
                var source = receipts[receipts.Length - 1];
                receipts[receipts.Length - 1] = new OperationReceipt(
                    source.OperationId,
                    source.OperationKind,
                    source.Target,
                    new Sha256Digest(new string('c', 64)),
                    source.AppliedLineageId,
                    source.AppliedRevision,
                    source.CompletedAtUtcMs,
                    source.OutcomeKind,
                    source.OutcomeSummary);
                return new CareerSaveSnapshot(
                    snapshot.Versions,
                    snapshot.Identity,
                    snapshot.CareerSeed,
                    snapshot.CareerName,
                    snapshot.PlayerDraft,
                    snapshot.Onboarding,
                    snapshot.Progression,
                    snapshot.TrainingEmphases,
                    snapshot.Player,
                    snapshot.TeamId,
                    snapshot.PotentialGrade,
                    snapshot.Fatigue,
                    snapshot.Mindset,
                    snapshot.CoachTrust,
                    receipts,
                    snapshot.PendingMatch,
                    snapshot.MatchHistory,
                    snapshot.SettlementReceipts);
            }

            public CareerPersistenceResult Create(CareerSaveSnapshot initialSnapshot, OperationId operationId)
            {
                throw new NotSupportedException();
            }

            public CareerPersistenceResult RecoverFromBackup(
                ProfileId profileId,
                SaveId saveId,
                CareerVersionToken confirmedBackupVersionToken,
                Sha256Digest? confirmedCorruptMainFingerprint,
                OperationId operationId,
                long recoveredAtUtcMs,
                LineageId newLineageId)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class SpyRandom : IDeterministicCareerRandom
        {
            private readonly IList<string> _calls;
            private readonly long _value;

            public SpyRandom(IList<string> calls, long value)
            {
                _calls = calls;
                _value = value;
            }

            public int Count { get; private set; }

            public long NextInt64(CareerRandomRequest request, long minInclusive, long maxExclusive)
            {
                _calls.Add("random");
                Count++;
                Assert.That(request.StreamId, Is.EqualTo("match_seed"));
                Assert.That(request.AlgorithmVersion,
                    Is.EqualTo(CareerSaveVersions.CurrentCareerRandomAlgorithmVersion));
                Assert.That(request.Season, Is.EqualTo(1));
                Assert.That(request.Week, Is.EqualTo(1));
                Assert.That(request.EntityStableId, Is.EqualTo("schedule.u1w1.match.01"));
                Assert.That(request.OccurrenceId.Value, Is.EqualTo(
                    Guid.Parse("66666666-6666-6666-6666-666666666666")));
                Assert.That(minInclusive, Is.Zero);
                Assert.That(maxExclusive, Is.EqualTo(4294967296L));
                Assert.That(request.DrawIndex, Is.Zero);
                return _value;
            }
        }

        private sealed class SpyLaunchFactory : ICareerFirstMatchLaunchFactory
        {
            private readonly IList<string> _calls;

            public SpyLaunchFactory(IList<string> calls)
            {
                _calls = calls;
            }

            public int Count { get; private set; }

            public CareerMatchLaunch Create(CareerFirstMatchLaunchRequest request)
            {
                _calls.Add("catalog");
                Count++;
                var teams = CareerMatchTestData.Teams(
                    request.HomeTeamId.Value,
                    "team.university.rival");
                var home = teams[0].Players.ToArray();
                home[1] = new CareerMatchPlayerLaunch(
                    request.ProtagonistPlayerId,
                    request.ProtagonistJerseyNumber,
                    CareerMatchPlayerPosition.OutsideHitter,
                    2,
                    request.ProtagonistFatigue,
                    request.ProtagonistAttributes);
                teams[0] = new CareerMatchTeamLaunch(
                    request.HomeTeamId,
                    CareerMatchTeamSide.Home,
                    home);
                return CareerMatchTestData.Launch(
                    versions: request.Versions,
                    sessionId: request.SessionId,
                    matchSeed: request.MatchSeed,
                    priority: request.PreMatchPriority == CareerMatchPriority.AttackFirst
                        ? CareerPreMatchPriority.AttackFirst
                        : request.PreMatchPriority == CareerMatchPriority.FirstContactSecurity
                            ? CareerPreMatchPriority.FirstContactSecurity
                            : CareerPreMatchPriority.StaminaControl,
                    teams: teams);
            }
        }

        private sealed class SpyExecutor : ICareerMatchExecutor
        {
            private readonly IList<string> _calls;
            private CareerMatchLaunch _launch;

            public SpyExecutor(IList<string> calls)
            {
                _calls = calls;
            }

            public int EncodeCount { get; private set; }
            public int ExecuteCount { get; private set; }
            public CareerCanonicalMatchContext EncodedContext { get; private set; }
            public CareerCanonicalMatchContext ExecutedContext { get; private set; }
            public Exception ExecutionException { get; set; }

            public CareerCanonicalMatchContext Encode(CareerMatchLaunch launch)
            {
                _calls.Add("encode");
                EncodeCount++;
                _launch = launch;
                EncodedContext = new CareerCanonicalMatchContext(
                    launch.SessionId,
                    new Sha256Digest(new string('a', 64)),
                    Encoding.UTF8.GetBytes("persisted-context"));
                return EncodedContext;
            }

            public Task<CareerMatchExecutionOutcome> ExecuteAsync(
                CareerCanonicalMatchContext context,
                CancellationToken cancellationToken)
            {
                _calls.Add("execute");
                ExecuteCount++;
                ExecutedContext = context;
                if (ExecutionException != null)
                {
                    throw ExecutionException;
                }

                var launch = _launch ?? CareerMatchTestData.Launch(sessionId: context.SessionId);
                var facts = new CareerMatchFacts(
                    launch.Versions,
                    context.SessionId,
                    context.ContextDigest,
                    CareerMatchResultStatus.Completed,
                    launch.Teams[0].TeamId,
                    new[] { new CareerMatchSetScore(1, 25, 21, true) },
                    46,
                    launch.Teams.SelectMany(team => team.Players)
                        .Select(player => CareerMatchTestData.ZeroFacts(player.PlayerId))
                        .ToArray(),
                    new Sha256Digest(new string('b', 64)));
                return Task.FromResult(new CareerMatchExecutionOutcome(
                    context,
                    new Sha256Digest(new string('b', 64)),
                    Encoding.UTF8.GetBytes("persisted-result"),
                    facts));
            }

            public CareerMatchExecutionOutcome DecodeAndValidate(
                byte[] canonicalContextUtf8,
                byte[] canonicalResultUtf8)
            {
                throw new NotSupportedException();
            }
        }
    }
}

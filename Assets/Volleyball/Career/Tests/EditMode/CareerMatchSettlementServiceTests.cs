using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchSettlementServiceTests
    {
        [Test]
        public void InvalidCanonicalPair_FailsBeforeLoadOrRules()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var executor = new SpyExecutor(calls, Outcome(snapshot))
            {
                DecodeException = new InvalidOperationException("tampered pair")
            };
            var repository = new MemoryRepository(snapshot, calls);
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(repository, executor, calculator);

            var result = service.Settle(Command(snapshot));

            Assert.That(result.Status, Is.EqualTo(CareerMatchSettlementStatus.ValidationFailed));
            Assert.That(calls, Is.EqualTo(new[] { "decode" }));
            Assert.That(repository.LoadCount, Is.Zero);
            Assert.That(repository.CommitCount, Is.Zero);
            Assert.That(calculator.Count, Is.Zero);
        }

        [Test]
        public void FirstSettlement_CommitsAllConsequencesAtomically()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var outcome = Outcome(snapshot);
            var repository = new MemoryRepository(snapshot, calls);
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, outcome),
                calculator);
            var command = Command(snapshot);

            var result = service.Settle(command);

            Assert.That(result.Status, Is.EqualTo(CareerMatchSettlementStatus.Settled));
            Assert.That(calls, Is.EqualTo(new[] { "decode", "load", "rules", "commit" }));
            Assert.That(calculator.Count, Is.EqualTo(1));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(repository.CommitOperationId,
                Is.EqualTo(new OperationId(command.SessionId)));
            Assert.That(result.CommittedRevision, Is.EqualTo(10));
            Assert.That(result.Snapshot.PendingMatch, Is.Null);
            Assert.That(result.Snapshot.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planning));
            Assert.That(result.Snapshot.Progression.WeekPlan.Season, Is.EqualTo(1));
            Assert.That(result.Snapshot.Progression.WeekPlan.Week, Is.EqualTo(2));
            Assert.That(result.Snapshot.Progression.WeekPlan.IsConfirmed, Is.False);
            Assert.That(result.Snapshot.Progression.WeekPlan.Slots, Is.All.Null);
            Assert.That(result.Snapshot.TrainingEmphases.Contributions, Is.Empty);
            Assert.That(result.Snapshot.MatchHistory, Has.Count.EqualTo(1));
            Assert.That(result.Snapshot.SettlementReceipts, Has.Count.EqualTo(1));
            Assert.That(result.Snapshot.Player.Attributes,
                Is.EqualTo(calculator.LastSummary.AfterAttributes));
            Assert.That(result.Snapshot.Fatigue,
                Is.EqualTo(calculator.LastSummary.WeekendFatigueChange.NewValue));
            Assert.That(result.Snapshot.Mindset,
                Is.EqualTo(calculator.LastSummary.WeekendMindsetChange.NewValue));
            Assert.That(result.Snapshot.CoachTrust,
                Is.EqualTo(calculator.LastSummary.WeekendCoachTrustChange.NewValue));
            Assert.That(result.Snapshot.SettlementReceipts[0].AppliedRevision, Is.EqualTo(10));
            Assert.That(result.Snapshot.SettlementReceipts[0].SettledAtUtcMs,
                Is.EqualTo(command.SettledAtUtcMs));
            Assert.That(result.Snapshot.SettlementReceipts[0].SettlementSummary,
                Is.EqualTo(calculator.LastSummary));
            Assert.That(result.Snapshot.MatchHistory[0].CanonicalContextUtf8,
                Is.EqualTo(command.CanonicalContextUtf8));
            Assert.That(result.Snapshot.MatchHistory[0].CanonicalResultUtf8,
                Is.EqualTo(command.CanonicalResultUtf8));
            Assert.That(result.Snapshot.OperationReceipts.Select(item => item.OperationId),
                Is.EqualTo(snapshot.OperationReceipts.Select(item => item.OperationId)));
            Assert.That(result.Snapshot.OperationReceipts.Select(item => item.InputFingerprint),
                Is.EqualTo(snapshot.OperationReceipts.Select(item => item.InputFingerprint)));
        }

        [Test]
        public void Command_DefensivelyCopiesCanonicalBytes()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var context = snapshot.PendingMatch.CanonicalContextUtf8;
            var result = Encoding.UTF8.GetBytes("canonical-result");
            var command = new SettleCareerMatchCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                200,
                snapshot.PendingMatch.SessionId,
                context,
                result);

            context[0] ^= 1;
            result[0] ^= 1;
            var firstContext = command.CanonicalContextUtf8;
            var firstResult = command.CanonicalResultUtf8;
            firstContext[0] ^= 1;
            firstResult[0] ^= 1;

            Assert.That(command.CanonicalContextUtf8,
                Is.EqualTo(snapshot.PendingMatch.CanonicalContextUtf8));
            Assert.That(command.CanonicalResultUtf8,
                Is.EqualTo(Encoding.UTF8.GetBytes("canonical-result")));
        }

        [Test]
        public void ExactSettledPair_ReturnsExistingBeforeVersionOrPendingChecks()
        {
            var awaiting = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var settled = CareerSaveV2LifecycleTestData.SettledSnapshot();
            var history = settled.MatchHistory[0];
            var calls = new List<string>();
            var repository = new MemoryRepository(settled, calls);
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, StoredOutcome(settled)),
                calculator);
            var command = new SettleCareerMatchCommand(
                settled.Identity.ProfileId,
                settled.Identity.SaveId,
                awaiting.Identity.VersionToken,
                999,
                history.SessionId,
                history.CanonicalContextUtf8,
                history.CanonicalResultUtf8);

            var result = service.Settle(command);

            Assert.That(result.Status, Is.EqualTo(CareerMatchSettlementStatus.Existing));
            Assert.That(result.CommittedRevision, Is.EqualTo(history.AppliedRevision));
            Assert.That(calls, Is.EqualTo(new[] { "decode", "load" }));
            Assert.That(calculator.Count, Is.Zero);
            Assert.That(repository.CommitCount, Is.Zero);
        }

        [Test]
        public void SettledSessionWithDifferentResult_ReturnsImmutableConflictEvidence()
        {
            var awaiting = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var settled = CareerSaveV2LifecycleTestData.SettledSnapshot();
            var history = settled.MatchHistory[0];
            var calls = new List<string>();
            var incoming = StoredOutcome(
                settled,
                new Sha256Digest(new string('c', 64)),
                Encoding.UTF8.GetBytes("different-canonical-result"));
            var repository = new MemoryRepository(settled, calls);
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, incoming),
                calculator);
            var command = new SettleCareerMatchCommand(
                settled.Identity.ProfileId,
                settled.Identity.SaveId,
                awaiting.Identity.VersionToken,
                999,
                history.SessionId,
                history.CanonicalContextUtf8,
                incoming.CanonicalResultUtf8);

            var result = service.Settle(command);

            Assert.That(result.Status,
                Is.EqualTo(CareerMatchSettlementStatus.SessionResultConflict));
            Assert.That(result.ConflictEvidence.StoredContextDigest,
                Is.EqualTo(history.ContextDigest));
            Assert.That(result.ConflictEvidence.StoredResultDigest,
                Is.EqualTo(history.ResultDigest));
            Assert.That(result.ConflictEvidence.IncomingResultDigest,
                Is.EqualTo(incoming.ResultDigest));
            Assert.That(calls, Is.EqualTo(new[] { "decode", "load" }));
            Assert.That(calculator.Count, Is.Zero);
            Assert.That(repository.CommitCount, Is.Zero);
        }

        [Test]
        public void AbandonedResult_PreservesPendingAndWritesNothing()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, Outcome(
                    snapshot,
                    CareerMatchResultStatus.Abandoned)),
                calculator);

            var result = service.Settle(Command(snapshot));

            Assert.That(result.Status, Is.EqualTo(CareerMatchSettlementStatus.Abandoned));
            Assert.That(result.Snapshot.PendingMatch.SessionId,
                Is.EqualTo(snapshot.PendingMatch.SessionId));
            Assert.That(calls, Is.EqualTo(new[] { "decode", "load" }));
            Assert.That(calculator.Count, Is.Zero);
            Assert.That(repository.CommitCount, Is.Zero);
        }

        [Test]
        public void OrdinaryVersionConflict_WritesNothingAndHasNoOperationConflict()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls);
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, Outcome(snapshot)),
                calculator);
            var command = new SettleCareerMatchCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                new CareerVersionToken(
                    snapshot.Identity.LineageId,
                    snapshot.Identity.Revision - 1,
                    snapshot.Identity.SnapshotHash),
                200,
                snapshot.PendingMatch.SessionId,
                snapshot.PendingMatch.CanonicalContextUtf8,
                Encoding.UTF8.GetBytes("canonical-result"));

            var result = service.Settle(command);

            Assert.That(result.Status,
                Is.EqualTo(CareerMatchSettlementStatus.RevisionConflict));
            Assert.That(calls, Is.EqualTo(new[] { "decode", "load" }));
            Assert.That(calculator.Count, Is.Zero);
            Assert.That(repository.CommitCount, Is.Zero);
        }

        [TestCase(CasMode.Exact, CareerMatchSettlementStatus.Existing)]
        [TestCase(CasMode.Conflict, CareerMatchSettlementStatus.SessionResultConflict)]
        [TestCase(CasMode.Missing, CareerMatchSettlementStatus.RevisionConflict)]
        public void CasConflict_ReloadsSettlementEvidenceOnlyAndNeverRecalculates(
            CasMode casMode,
            CareerMatchSettlementStatus expectedStatus)
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls)
            {
                CasConflict = casMode
            };
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, Outcome(snapshot)),
                calculator);

            var result = service.Settle(Command(snapshot));

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(calls, Is.EqualTo(
                new[] { "decode", "load", "rules", "commit", "load" }));
            Assert.That(calculator.Count, Is.EqualTo(1));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void RepositoryException_ReturnsTypedFailureWithoutLeaking()
        {
            var snapshot = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var calls = new List<string>();
            var repository = new MemoryRepository(snapshot, calls)
            {
                LoadException = new InvalidOperationException("disk unavailable")
            };
            var calculator = new SpyCalculator(calls);
            var service = new CareerMatchSettlementService(
                repository,
                new SpyExecutor(calls, Outcome(snapshot)),
                calculator);

            CareerMatchSettlementResult result = null;
            Assert.DoesNotThrow(() => result = service.Settle(Command(snapshot)));

            Assert.That(result.Status, Is.EqualTo(CareerMatchSettlementStatus.ValidationFailed));
            Assert.That(result.FailureKind,
                Is.EqualTo(CareerMatchSettlementFailureKind.Persistence));
            Assert.That(result.FailureCode, Is.EqualTo("persistence_failed"));
            Assert.That(calls, Is.EqualTo(new[] { "decode", "load" }));
            Assert.That(calculator.Count, Is.Zero);
            Assert.That(repository.CommitCount, Is.Zero);
        }

        private static SettleCareerMatchCommand Command(CareerSaveSnapshot snapshot)
        {
            return new SettleCareerMatchCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                200,
                snapshot.PendingMatch.SessionId,
                snapshot.PendingMatch.CanonicalContextUtf8,
                Encoding.UTF8.GetBytes("canonical-result"));
        }

        private static CareerMatchExecutionOutcome Outcome(
            CareerSaveSnapshot snapshot,
            CareerMatchResultStatus status = CareerMatchResultStatus.Completed)
        {
            var pending = snapshot.PendingMatch;
            var versions = new CareerMatchVersions(
                pending.Versions.ContractVersion,
                pending.Versions.ContentVersion,
                pending.Versions.RulesetVersion,
                pending.Versions.CareerRandomAlgorithmVersion,
                pending.Versions.MatchSimulationVersion,
                pending.Versions.MatchRandomAlgorithmVersion);
            var resultDigest = new Sha256Digest(new string('b', 64));
            var facts = new CareerMatchFacts(
                versions,
                pending.SessionId,
                pending.ContextDigest,
                status,
                status == CareerMatchResultStatus.Completed
                    ? new TeamId?(pending.HomeTeamId)
                    : null,
                status == CareerMatchResultStatus.Completed
                    ? new[] { new CareerMatchSetScore(1, 25, 21, true) }
                    : Array.Empty<CareerMatchSetScore>(),
                status == CareerMatchResultStatus.Completed ? 46 : 0,
                pending.OrderedPlayerIds
                    .Select(CareerMatchTestData.ZeroFacts)
                    .ToArray(),
                resultDigest);
            return new CareerMatchExecutionOutcome(
                new CareerCanonicalMatchContext(
                    pending.SessionId,
                    pending.ContextDigest,
                    pending.CanonicalContextUtf8),
                resultDigest,
                Encoding.UTF8.GetBytes("canonical-result"),
                facts);
        }

        private static CareerMatchExecutionOutcome StoredOutcome(
            CareerSaveSnapshot snapshot,
            Sha256Digest? resultDigest = null,
            byte[] canonicalResultUtf8 = null)
        {
            var history = snapshot.MatchHistory[0];
            var digest = resultDigest ?? history.ResultDigest;
            var launch = CareerMatchTestData.Launch(sessionId: history.SessionId);
            var facts = new CareerMatchFacts(
                launch.Versions,
                history.SessionId,
                history.ContextDigest,
                CareerMatchResultStatus.Completed,
                launch.Teams[0].TeamId,
                new[] { new CareerMatchSetScore(1, 25, 21, true) },
                46,
                launch.Teams.SelectMany(team => team.Players)
                    .Select(player => CareerMatchTestData.ZeroFacts(player.PlayerId))
                    .ToArray(),
                digest);
            return new CareerMatchExecutionOutcome(
                new CareerCanonicalMatchContext(
                    history.SessionId,
                    history.ContextDigest,
                    history.CanonicalContextUtf8),
                digest,
                canonicalResultUtf8 ?? history.CanonicalResultUtf8,
                facts);
        }

        private sealed class SpyExecutor : ICareerMatchExecutor
        {
            private readonly IList<string> _calls;
            private readonly CareerMatchExecutionOutcome _outcome;

            public SpyExecutor(IList<string> calls, CareerMatchExecutionOutcome outcome)
            {
                _calls = calls;
                _outcome = outcome;
            }

            public Exception DecodeException { get; set; }

            public CareerCanonicalMatchContext Encode(CareerMatchLaunch launch)
            {
                throw new NotSupportedException();
            }

            public System.Threading.Tasks.Task<CareerMatchExecutionOutcome> ExecuteAsync(
                CareerCanonicalMatchContext context,
                System.Threading.CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public CareerMatchExecutionOutcome DecodeAndValidate(
                byte[] canonicalContextUtf8,
                byte[] canonicalResultUtf8)
            {
                _calls.Add("decode");
                if (DecodeException != null)
                {
                    throw DecodeException;
                }

                return _outcome;
            }
        }

        private sealed class SpyCalculator : ICareerMatchSettlementCalculator
        {
            private readonly IList<string> _calls;

            public SpyCalculator(IList<string> calls)
            {
                _calls = calls;
            }

            public int Count { get; private set; }
            public CareerSettlementSummary LastSummary { get; private set; }

            public CareerSettlementSummary Calculate(
                PendingCareerMatch pendingMatch,
                CareerMatchFacts completedFacts,
                CareerPlayerRecord currentPlayer,
                PotentialGrade potentialGrade,
                int currentFatigue,
                int currentMindset,
                int currentCoachTrust)
            {
                _calls.Add("rules");
                Count++;
                LastSummary = CareerMatchSettlementRulesV1.Calculate(
                    pendingMatch,
                    completedFacts,
                    currentPlayer,
                    potentialGrade,
                    currentFatigue,
                    currentMindset,
                    currentCoachTrust);
                return LastSummary;
            }
        }

        public enum CasMode
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

            public CareerSaveSnapshot Current { get; private set; }
            public int LoadCount { get; private set; }
            public int CommitCount { get; private set; }
            public OperationId? CommitOperationId { get; private set; }
            public CasMode CasConflict { get; set; }
            public Exception LoadException { get; set; }

            public CareerPersistenceResult Load(ProfileId profileId, SaveId saveId)
            {
                _calls.Add("load");
                LoadCount++;
                if (LoadException != null)
                {
                    throw LoadException;
                }

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
                CommitOperationId = operationId;
                if (CasConflict != CasMode.None)
                {
                    if (CasConflict == CasMode.Exact)
                    {
                        Current = nextSnapshot;
                    }
                    else if (CasConflict == CasMode.Conflict)
                    {
                        Current = WithDifferentResult(nextSnapshot);
                    }

                    return new CareerPersistenceResult(PersistenceResultKind.VersionConflict);
                }

                Current = nextSnapshot;
                return new CareerPersistenceResult(PersistenceResultKind.Committed, nextSnapshot);
            }

            private static CareerSaveSnapshot WithDifferentResult(CareerSaveSnapshot snapshot)
            {
                var source = snapshot.MatchHistory[0];
                var digest = new Sha256Digest(new string('c', 64));
                var history = new CareerMatchHistoryEntry(
                    source.SessionId,
                    source.ScheduleItemId,
                    source.SourceWeekPlanId,
                    source.SourceSlotActionId,
                    source.ContextDigest,
                    digest,
                    source.CanonicalContextUtf8,
                    source.CanonicalResultUtf8,
                    source.AppliedLineageId,
                    source.AppliedRevision,
                    source.SettledAtUtcMs,
                    source.SettlementSummary);
                var receipt = new CareerSettlementReceipt(
                    history.SessionId,
                    history.ContextDigest,
                    history.ResultDigest,
                    history.AppliedLineageId,
                    history.AppliedRevision,
                    history.SettledAtUtcMs,
                    history.SettlementSummary);
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
                    snapshot.OperationReceipts,
                    null,
                    new[] { history },
                    new[] { receipt });
            }

            public CareerPersistenceResult Create(
                CareerSaveSnapshot initialSnapshot,
                OperationId operationId)
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
    }
}

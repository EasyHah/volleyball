using System;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    /// <summary>
    /// The V5-A Career lifecycle: profile -> canonical V5 pending -> formal
    /// result/replay. Settlement intentionally begins only in V5-B.
    /// </summary>
    public sealed class CareerV5MatchLifecycleService
    {
        private readonly CareerFirstMatchLaunchFactoryV5 _launchFactory;
        private readonly CareerMatchV5Mapper _mapper;
        private readonly CareerMatchExecutorV5 _executor;
        private readonly CareerV5PendingStore _pendingStore;

        public CareerV5MatchLifecycleService(CareerFirstMatchLaunchFactoryV5 launchFactory,
            CareerMatchV5Mapper mapper, CareerMatchExecutorV5 executor,
            CareerV5PendingStore pendingStore = null)
        {
            _launchFactory = launchFactory ?? throw new ArgumentNullException(nameof(launchFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _pendingStore = pendingStore;
        }

        public CareerPendingMatchV5 CreatePending(CareerPlayerProfileV5 profile,
            TeamId homeTeamId, int fatigue, Guid sessionId, uint matchSeed)
        {
            var launch = _launchFactory.Create(profile, homeTeamId, fatigue, sessionId, matchSeed);
            return CareerPendingMatchV5.Create(_mapper.ToContext(launch));
        }

        public CareerPendingMatchV5 CreateAndPersistPending(CareerPlayerProfileV5 profile,
            TeamId homeTeamId, int fatigue, Guid sessionId, uint matchSeed)
        {
            if (_pendingStore == null)
                throw new InvalidOperationException("The V5 pending store is not configured.");
            _pendingStore.SaveProfile(profile);
            var pending = CreatePending(profile, homeTeamId, fatigue, sessionId, matchSeed);
            _pendingStore.SavePending(profile.PlayerId, pending.CanonicalContextUtf8);
            return pending;
        }

        public CareerV5PendingRecovery LoadPending(CareerPlayerProfileV5 profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (_pendingStore == null)
                throw new InvalidOperationException("The V5 pending store is not configured.");
            var bytes = _pendingStore.LoadPending(profile.PlayerId);
            return CareerV5PendingRecovery.Read(bytes == null
                ? null
                : CareerPendingMatchV5.FromCanonicalContext(bytes));
        }

        public bool DiscardPending(CareerPlayerProfileV5 profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (_pendingStore == null)
                throw new InvalidOperationException("The V5 pending store is not configured.");
            return _pendingStore.DiscardPending(profile.PlayerId);
        }

        public Task<CareerMatchRunOutcomeV5> ExecuteAsync(CareerPendingMatchV5 pending,
            CancellationToken cancellationToken)
        {
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            // The pending artifact itself is the immutable canonical V5 source
            // of truth; executing it never re-applies fatigue or re-maps bases.
            return _executor.ExecuteContextAsync(pending.Context, cancellationToken);
        }

        public CareerV5PendingRecovery RecoverLegacy(PendingCareerMatch legacy) =>
            CareerV5PendingRecovery.RejectLegacy(legacy);

    }
}

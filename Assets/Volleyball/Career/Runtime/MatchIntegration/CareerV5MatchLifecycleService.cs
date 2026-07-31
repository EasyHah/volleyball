using System;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Domain;
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

        public CareerV5MatchLifecycleService(CareerFirstMatchLaunchFactoryV5 launchFactory,
            CareerMatchV5Mapper mapper, CareerMatchExecutorV5 executor)
        {
            _launchFactory = launchFactory ?? throw new ArgumentNullException(nameof(launchFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public CareerPendingMatchV5 CreatePending(CareerPlayerProfileV5 profile,
            TeamId homeTeamId, int fatigue, Guid sessionId, uint matchSeed)
        {
            var launch = _launchFactory.Create(profile, homeTeamId, fatigue, sessionId, matchSeed);
            return CareerPendingMatchV5.Create(_mapper.ToContext(launch));
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

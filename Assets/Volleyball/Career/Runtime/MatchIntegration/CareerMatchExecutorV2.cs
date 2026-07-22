using System;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerMatchExecutorV2 : ICareerMatchExecutor
    {
        private readonly IMatchRunnerV2 _runner;
        private readonly CareerMatchV2Mapper _mapper;

        public CareerMatchExecutorV2(IMatchRunnerV2 runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _mapper = new CareerMatchV2Mapper();
        }

        public async Task<CareerMatchFacts> ExecuteAsync(
            CareerMatchLaunch launch,
            CancellationToken cancellationToken)
        {
            if (launch == null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var context = _mapper.ToContext(launch);
            var result = await _runner.ExecuteAsync(context, cancellationToken);
            if (result == null)
            {
                throw new InvalidOperationException("The Match V2 runner returned no result.");
            }

            result.ValidateAgainst(context);
            return _mapper.ToCareerFacts(result);
        }
    }
}

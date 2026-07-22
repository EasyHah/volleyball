using System.Threading;
using System.Threading.Tasks;

namespace Volleyball.Shared.Contracts.V2
{
    public interface IMatchRunnerV2
    {
        Task<MatchResultV2> ExecuteAsync(
            MatchContextV2 context,
            CancellationToken cancellationToken);
    }
}

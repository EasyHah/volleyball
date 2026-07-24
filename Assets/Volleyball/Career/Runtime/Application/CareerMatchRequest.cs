using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public sealed class CareerMatchRequest
    {
        public CareerMatchRequest(
            MatchContextV4 context,
            Action<MatchResultV4> complete)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Complete = complete ?? throw new ArgumentNullException(nameof(complete));
        }

        public MatchContextV4 Context { get; }
        public Action<MatchResultV4> Complete { get; }
    }
}

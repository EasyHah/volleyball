using System;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public sealed class CareerMatchRequest
    {
        public CareerMatchRequest(CareerPlayerRecord focusPlayer, MatchContextV1 context)
        {
            FocusPlayer = focusPlayer ?? throw new ArgumentNullException(nameof(focusPlayer));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public CareerPlayerRecord FocusPlayer { get; }

        public MatchContextV1 Context { get; }
    }

    public interface IMatchGateway
    {
        MatchResultV1 Play(CareerMatchRequest request);
    }
}

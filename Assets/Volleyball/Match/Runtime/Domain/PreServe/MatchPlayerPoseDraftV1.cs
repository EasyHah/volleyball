using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public sealed class MatchPlayerPoseDraftV1
    {
        public MatchPlayerPoseDraftV1(PlayerId playerId, SimVector3 position)
        {
            PlayerId = playerId;
            Position = position;
        }

        public PlayerId PlayerId { get; }
        public SimVector3 Position { get; internal set; }

        internal MatchPlayerPoseDraftV1 DeepCopy()
        {
            return new MatchPlayerPoseDraftV1(PlayerId, Position);
        }
    }
}

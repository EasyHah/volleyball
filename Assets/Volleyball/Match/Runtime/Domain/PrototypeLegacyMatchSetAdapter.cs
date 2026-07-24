using System;
using System.Linq;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Domain
{
    // The only retained V2 set path, owned by the explicitly prototype-only 3v3 adapter.
    public sealed class PrototypeLegacyMatchSetAdapter : IMatchSetRuntime
    {
        private readonly MatchContextV2 _context;
        private readonly MatchSet _runtime;

        public PrototypeLegacyMatchSetAdapter(
            MatchContextV2 context,
            TeamSide firstServer,
            MatchSetRules rules = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _runtime = new MatchSet(
                context.Home.Players.Select(player => player.PlayerId),
                context.Away.Players.Select(player => player.PlayerId),
                firstServer,
                rules);
        }

        public int HomeScore => _runtime.HomeScore;
        public int AwayScore => _runtime.AwayScore;
        public TeamSide ServingSide => _runtime.ServingSide;
        public TeamSide ReceivingSide => _runtime.ReceivingSide;
        public bool IsComplete => _runtime.IsComplete;
        public int SetTargetScore => _runtime.SetTargetScore;
        public int RosterSize => _runtime.RosterSize;
        public int RotationOffsetFor(TeamSide side) => _runtime.RotationOffsetFor(side);
        public StablePlayerId ServerFor(TeamSide side) => _runtime.ServerFor(side);
        public StablePlayerId PlayerAtRotationPosition(TeamSide side, int position) =>
            _runtime.PlayerAtRotationPosition(side, position);
        public int RotationPositionFor(StablePlayerId playerId) =>
            _runtime.RotationPositionFor(playerId);
        public bool IsFrontRow(StablePlayerId playerId) => _runtime.IsFrontRow(playerId);
        public void RecordContact(StablePlayerId playerId, float movementDistance) =>
            _runtime.RecordContact(playerId, movementDistance);
        public void ResolveRally(
            TeamSide winner,
            StablePlayerId? pointScorer,
            StablePlayerId? errorPlayer) =>
            _runtime.ResolveRally(winner, pointScorer, errorPlayer);

        public MatchResultV2 CreatePrototypeResult()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException(
                    "A prototype result is available only after the set completes.");
            }

            var winner = _runtime.WinnerSide == TeamSide.Home
                ? _context.Home.TeamId
                : _context.Away.TeamId;
            return MatchResultV2.Create(
                _context,
                winner,
                HomeScore,
                AwayScore,
                _runtime.CreateRuntimeStats()
                    .Select(stats => new PlayerMatchStatsV2(
                        stats.PlayerId,
                        stats.Points,
                        stats.Contacts,
                        stats.Errors,
                        stats.Workload))
                    .ToArray());
        }
    }
}

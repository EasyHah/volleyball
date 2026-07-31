using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public static class OnCourtLineupRulesV5
    {
        public static OnCourtEligibilitySnapshot Create(
            MatchContextV5 context,
            IReadOnlyList<PlayerId> homeRotationOrder,
            IReadOnlyList<PlayerId> awayRotationOrder,
            PlayerId homeServer,
            PlayerId awayServer)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new OnCourtEligibilitySnapshot(Combine(context.Home, TeamSide.Home, homeRotationOrder,
                homeServer, context.Away, TeamSide.Away, awayRotationOrder, awayServer).ToArray());
        }

        private static IEnumerable<OnCourtPlayerEligibilityV3> Combine(
            TeamSnapshotV5 first, TeamSide firstSide, IReadOnlyList<PlayerId> firstOrder, PlayerId firstServer,
            TeamSnapshotV5 second, TeamSide secondSide, IReadOnlyList<PlayerId> secondOrder, PlayerId secondServer)
        {
            foreach (var value in Add(first, firstSide, firstOrder, firstServer)) yield return value;
            foreach (var value in Add(second, secondSide, secondOrder, secondServer)) yield return value;
        }

        private static IEnumerable<OnCourtPlayerEligibilityV3> Add(TeamSnapshotV5 team,
            TeamSide side, IReadOnlyList<PlayerId> order, PlayerId server)
        {
            if (order == null || order.Count != 6) throw new ArgumentException("Exactly six V5 rotation IDs are required.");
            var positions = new Dictionary<PlayerId, PlayerPosition>();
            foreach (var player in team.RotationOrder) positions.Add(player.PlayerId, player.Position);
            var seen = new HashSet<PlayerId>();
            for (var index = 0; index < order.Count; index++)
            {
                if (!seen.Add(order[index]) || !positions.TryGetValue(order[index], out var position))
                    throw new ArgumentException("V5 rotation IDs must be unique team members.");
                yield return new OnCourtPlayerEligibilityV3(order[index], side, position, index + 1,
                    order[index].Equals(server), null);
            }
        }
    }
}

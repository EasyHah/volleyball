using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public interface ICareerMatchRunnerV3
    {
        Task<MatchResultV3> ExecuteAsync(
            MatchContextV3 context,
            CancellationToken cancellationToken);
    }

    public sealed class DeterministicFixtureMatchRunnerV3 : ICareerMatchRunnerV3
    {
        public Task<MatchResultV3> ExecuteAsync(
            MatchContextV3 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                return Task.FromException<MatchResultV3>(
                    new ArgumentNullException(nameof(context)));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<MatchResultV3>(cancellationToken);
            }

            try
            {
                var stats = BuildStats(context);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(MatchResultV3.Create(
                    context,
                    context.Home.TeamId,
                    25,
                    21,
                    stats));
            }
            catch (OperationCanceledException exception)
                when (exception.CancellationToken == cancellationToken)
            {
                return Task.FromCanceled<MatchResultV3>(cancellationToken);
            }
            catch (Exception exception)
            {
                return Task.FromException<MatchResultV3>(exception);
            }
        }

        private static PlayerMatchStatsV3[] BuildStats(MatchContextV3 context)
        {
            var result = new List<PlayerMatchStatsV3>(12);
            AddTeamStats(context.Home, true, result);
            AddTeamStats(context.Away, false, result);
            return result.ToArray();
        }

        private static void AddTeamStats(
            TeamSnapshotV3 team,
            bool home,
            ICollection<PlayerMatchStatsV3> output)
        {
            var outsideIndex = 0;
            foreach (var player in team.Players)
            {
                var points = 0;
                var contacts = 8;
                var errors = 0;
                var workload = 0.60f;

                switch (player.Position)
                {
                    case PlayerPosition.OutsideHitter:
                        points = home && outsideIndex == 0 ? 7 : home ? 3 : 5;
                        contacts = home && outsideIndex == 0 ? 12 : 10;
                        errors = 1;
                        workload = home && outsideIndex == 0 ? 0.72f : 0.64f;
                        outsideIndex++;
                        break;
                    case PlayerPosition.Opposite:
                        points = home ? 3 : 5;
                        contacts = 8;
                        errors = 1;
                        break;
                    case PlayerPosition.MiddleBlocker:
                        points = 2;
                        contacts = 6;
                        break;
                    case PlayerPosition.Setter:
                        points = 1;
                        contacts = 8;
                        break;
                    case PlayerPosition.Libero:
                    case PlayerPosition.Defender:
                        contacts = home ? 10 : 11;
                        errors = home ? 0 : 1;
                        workload = 0.58f;
                        break;
                }

                output.Add(new PlayerMatchStatsV3(
                    player.PlayerId,
                    points,
                    contacts,
                    errors,
                    workload));
            }
        }
    }
}

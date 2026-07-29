using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public interface ICareerMatchRunnerV4
    {
        Task<MatchResultV4> ExecuteAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken);
    }

    public sealed class DeterministicFixtureMatchRunnerV4 : ICareerMatchRunnerV4
    {
        public Task<MatchResultV4> ExecuteAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                return Task.FromException<MatchResultV4>(
                    new ArgumentNullException(nameof(context)));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<MatchResultV4>(cancellationToken);
            }

            try
            {
                var stats = BuildStats(context);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(MatchResultV4.Create(
                    context,
                    context.Home.TeamId,
                    25,
                    21,
                    46,
                    96,
                    96,
                    stats));
            }
            catch (OperationCanceledException exception)
                when (exception.CancellationToken == cancellationToken)
            {
                return Task.FromCanceled<MatchResultV4>(cancellationToken);
            }
            catch (Exception exception)
            {
                return Task.FromException<MatchResultV4>(exception);
            }
        }

        private static PlayerMatchStatsV4[] BuildStats(MatchContextV4 context)
        {
            var result = new List<PlayerMatchStatsV4>(12);
            AddTeamStats(context.Home, true, result);
            AddTeamStats(context.Away, false, result);
            return result.ToArray();
        }

        private static void AddTeamStats(
            TeamSnapshotV4 team,
            bool home,
            ICollection<PlayerMatchStatsV4> output)
        {
            var outsideIndex = 0;
            foreach (var player in team.RotationOrder)
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

                output.Add(new PlayerMatchStatsV4(
                    player.PlayerId,
                    points,
                    contacts,
                    errors,
                    workload));
            }
        }
    }
}

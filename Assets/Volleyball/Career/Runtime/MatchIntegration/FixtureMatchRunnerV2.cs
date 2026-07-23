using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class FixtureMatchRunnerV2 : IMatchRunnerV2
    {
        private readonly VersionedMatchFixtureRepository _repository;

        public FixtureMatchRunnerV2(VersionedMatchFixtureRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public Task<MatchResultV2> ExecuteAsync(
            MatchContextV2 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                return Task.FromException<MatchResultV2>(new ArgumentNullException(nameof(context)));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<MatchResultV2>(cancellationToken);
            }

            try
            {
                return Task.FromResult(ExecuteCore(context, cancellationToken));
            }
            catch (OperationCanceledException exception)
                when (exception.CancellationToken == cancellationToken)
            {
                return Task.FromCanceled<MatchResultV2>(cancellationToken);
            }
            catch (Exception exception)
            {
                return Task.FromException<MatchResultV2>(exception);
            }
        }

        private MatchResultV2 ExecuteCore(
            MatchContextV2 context,
            CancellationToken cancellationToken)
        {
            _ = MatchContractV2Json.SerializeContext(context);
            if (context.ExecutionMode != MatchExecutionModeV2.Fixture ||
                context.FixtureId == null ||
                !context.FixtureVersion.HasValue)
            {
                throw new MatchV2ContractException("The fixture runner accepts only fixture-mode contexts.");
            }

            var definition = _repository.GetRequired(context.FixtureId, context.FixtureVersion.Value);
            ValidateTemplateIdentity(context, definition.Context);
            var templateFactsBySlot = IndexTemplateFactsBySideAndRotation(
                definition.Context, definition.Result);
            var reboundFacts = RebindFacts(context, templateFactsBySlot);
            var sets = CopySets(definition.Result.Sets);
            cancellationToken.ThrowIfCancellationRequested();

            MatchResultV2 result;
            if (definition.Result.Status == MatchStatusV2.Completed)
            {
                var winningSide = ResolveTemplateWinnerSide(definition.Context, definition.Result);
                var winnerTeamId = ResolveIncomingTeamId(context, winningSide);
                result = MatchResultV2.CreateCompleted(
                    context,
                    winnerTeamId,
                    sets,
                    definition.Result.RallyCount,
                    reboundFacts);
            }
            else
            {
                result = MatchResultV2.CreateAbandoned(
                    context,
                    sets,
                    definition.Result.RallyCount,
                    reboundFacts);
            }

            result.ValidateAgainst(context);
            return result;
        }

        private static void ValidateTemplateIdentity(MatchContextV2 incoming, MatchContextV2 template)
        {
            if (!incoming.Versions.Equals(template.Versions))
            {
                throw new MatchV2ContractException("The fixture version tuple does not match the template.");
            }

            if (!string.Equals(incoming.CompetitionId, template.CompetitionId, StringComparison.Ordinal) ||
                !string.Equals(incoming.ScheduleItemId, template.ScheduleItemId, StringComparison.Ordinal))
            {
                throw new MatchV2ContractException("The fixture schedule identity does not match.");
            }

            if (!FormatsEqual(incoming.Format, template.Format))
            {
                throw new MatchV2ContractException("The fixture format does not match the template.");
            }

            if (incoming.Teams.Count != template.Teams.Count)
            {
                throw new MatchV2ContractException("The fixture team topology does not match the template.");
            }

            for (var teamIndex = 0; teamIndex < template.Teams.Count; teamIndex++)
            {
                var incomingTeam = incoming.Teams[teamIndex];
                var templateTeam = template.Teams[teamIndex];
                if (incomingTeam.Side != templateTeam.Side ||
                    incomingTeam.Players.Count != templateTeam.Players.Count)
                {
                    throw new MatchV2ContractException("The fixture team side does not match the template.");
                }

                for (var playerIndex = 0; playerIndex < templateTeam.Players.Count; playerIndex++)
                {
                    var incomingPlayer = incomingTeam.Players[playerIndex];
                    var templatePlayer = templateTeam.Players[playerIndex];
                    if (incomingPlayer.Position != templatePlayer.Position ||
                        incomingPlayer.RotationSlot != templatePlayer.RotationSlot)
                    {
                        throw new MatchV2ContractException(
                            "The fixture position/rotation topology does not match the template.");
                    }
                }
            }
        }

        private static bool FormatsEqual(MatchFormatV2 left, MatchFormatV2 right)
        {
            return string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
                   left.TeamSize == right.TeamSize &&
                   left.SetsToWin == right.SetsToWin &&
                   left.SetTargetPoints == right.SetTargetPoints &&
                   left.MinimumLeadPoints == right.MinimumLeadPoints;
        }

        private static IReadOnlyDictionary<FixtureFactKey, MatchPlayerFactsV2>
            IndexTemplateFactsBySideAndRotation(
                MatchContextV2 templateContext,
                MatchResultV2 templateResult)
        {
            var factsByPlayerId = new Dictionary<PlayerId, MatchPlayerFactsV2>();
            foreach (var facts in templateResult.PlayerFacts)
            {
                factsByPlayerId.Add(facts.PlayerId, facts);
            }

            var factsBySlot = new Dictionary<FixtureFactKey, MatchPlayerFactsV2>();
            foreach (var team in templateContext.Teams)
            {
                foreach (var player in team.Players)
                {
                    if (!factsByPlayerId.TryGetValue(player.PlayerId, out var facts))
                    {
                        throw new MatchV2ContractException("The template result is missing a player fact.");
                    }

                    factsBySlot.Add(new FixtureFactKey(team.Side, player.RotationSlot), facts);
                }
            }

            return factsBySlot;
        }

        private static MatchPlayerFactsV2[] RebindFacts(
            MatchContextV2 incoming,
            IReadOnlyDictionary<FixtureFactKey, MatchPlayerFactsV2> templateFactsBySlot)
        {
            var rebound = new MatchPlayerFactsV2[12];
            var outputIndex = 0;
            foreach (var team in incoming.Teams)
            {
                foreach (var player in team.Players)
                {
                    var key = new FixtureFactKey(team.Side, player.RotationSlot);
                    if (!templateFactsBySlot.TryGetValue(key, out var template))
                    {
                        throw new MatchV2ContractException("The template has no fact for this side/rotation slot.");
                    }

                    rebound[outputIndex++] = CopyFacts(player.PlayerId, template);
                }
            }

            return rebound;
        }

        private static MatchPlayerFactsV2 CopyFacts(PlayerId playerId, MatchPlayerFactsV2 source)
        {
            return new MatchPlayerFactsV2(
                playerId,
                new SpikeFactsV2(source.Spike.Attempts, source.Spike.Points, source.Spike.Errors),
                new ServeFactsV2(source.Serve.Attempts, source.Serve.Aces, source.Serve.Errors),
                new ReceptionFactsV2(
                    source.Reception.Attempts,
                    source.Reception.Perfect,
                    source.Reception.Positive,
                    source.Reception.Neutral,
                    source.Reception.Negative,
                    source.Reception.Errors),
                new DefenseFactsV2(source.Defense.Attempts, source.Defense.Successes),
                new BlockFactsV2(
                    source.Block.Attempts, source.Block.EffectiveTouches, source.Block.Points),
                new MatchLoadFactsV2(
                    source.Load.RalliesPlayed,
                    source.Load.ActiveDurationMilliseconds,
                    source.Load.MovementDistanceMillimeters,
                    source.Load.JumpCount,
                    source.Load.HighLoadJumpCount,
                    source.Load.LandingLoadBasisPoints,
                    source.Load.TotalWorkloadBasisPoints),
                new StabilityFactsV2(
                    source.Stability.CriticalActions,
                    source.Stability.CriticalSuccesses,
                    source.Stability.CriticalErrors,
                    source.Stability.ErrorStreakEpisodes,
                    source.Stability.LongestErrorStreak));
        }

        private static SetScoreV2[] CopySets(IReadOnlyList<SetScoreV2> sets)
        {
            var copy = new SetScoreV2[sets.Count];
            for (var index = 0; index < sets.Count; index++)
            {
                copy[index] = new SetScoreV2(
                    sets[index].SetNumber,
                    sets[index].HomePoints,
                    sets[index].AwayPoints,
                    sets[index].IsComplete);
            }

            return copy;
        }

        private static TeamSideV2 ResolveTemplateWinnerSide(
            MatchContextV2 templateContext,
            MatchResultV2 templateResult)
        {
            if (!templateResult.WinnerTeamId.HasValue)
            {
                throw new MatchV2ContractException("A completed fixture template has no winner team ID.");
            }

            foreach (var team in templateContext.Teams)
            {
                if (team.TeamId.Equals(templateResult.WinnerTeamId.Value))
                {
                    return team.Side;
                }
            }

            throw new MatchV2ContractException("The template winner is not one of the template teams.");
        }

        private static TeamId ResolveIncomingTeamId(MatchContextV2 incoming, TeamSideV2 winningSide)
        {
            foreach (var team in incoming.Teams)
            {
                if (team.Side == winningSide)
                {
                    return team.TeamId;
                }
            }

            throw new MatchV2ContractException("The incoming context has no team on the winning side.");
        }

        private readonly struct FixtureFactKey : IEquatable<FixtureFactKey>
        {
            public FixtureFactKey(TeamSideV2 side, int rotationSlot)
            {
                Side = side;
                RotationSlot = rotationSlot;
            }

            public TeamSideV2 Side { get; }

            public int RotationSlot { get; }

            public bool Equals(FixtureFactKey other)
            {
                return Side == other.Side && RotationSlot == other.RotationSlot;
            }

            public override bool Equals(object obj)
            {
                return obj is FixtureFactKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Side * 397) ^ RotationSlot;
                }
            }
        }
    }
}

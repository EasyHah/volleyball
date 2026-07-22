using System;
using System.Collections.Generic;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.Contracts.V2
{
    public sealed class MatchContextV2
    {
        private readonly MatchTeamSnapshotV2[] _teams;

        private MatchContextV2(
            MatchVersionSetV2 versions, Guid sessionId, MatchExecutionModeV2 executionMode,
            string fixtureId, int? fixtureVersion, uint matchSeed, string competitionId,
            string scheduleItemId, int importanceBasisPoints, MatchFormatV2 format,
            PreMatchPriorityV2 preMatchPriority, IReadOnlyList<MatchTeamSnapshotV2> teams)
        {
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
            if (sessionId == Guid.Empty) throw new MatchV2ContractException("sessionId cannot be empty.");
            MatchV2Guard.Enum(executionMode, nameof(executionMode));
            MatchV2Guard.Enum(preMatchPriority, nameof(preMatchPriority));
            ValidateMode(executionMode, fixtureId, fixtureVersion, versions);
            SessionId = sessionId;
            ExecutionMode = executionMode;
            FixtureId = fixtureId;
            FixtureVersion = fixtureVersion;
            MatchSeed = matchSeed;
            CompetitionId = MatchV2Guard.StableId(competitionId, nameof(competitionId));
            ScheduleItemId = MatchV2Guard.StableId(scheduleItemId, nameof(scheduleItemId));
            ImportanceBasisPoints = MatchV2Guard.BasisPoints(importanceBasisPoints, nameof(importanceBasisPoints));
            Format = format ?? throw new ArgumentNullException(nameof(format));
            PreMatchPriority = preMatchPriority;
            _teams = MatchV2Guard.Copy(teams, nameof(teams));
            ValidateTeams(_teams);
            ContextHash = MatchContractV2Json.ComputeContextHash(this);
        }

        public MatchVersionSetV2 Versions { get; }
        public Guid SessionId { get; }
        public MatchExecutionModeV2 ExecutionMode { get; }
        public string FixtureId { get; }
        public int? FixtureVersion { get; }
        public uint MatchSeed { get; }
        public string CompetitionId { get; }
        public string ScheduleItemId { get; }
        public int ImportanceBasisPoints { get; }
        public MatchFormatV2 Format { get; }
        public PreMatchPriorityV2 PreMatchPriority { get; }
        public IReadOnlyList<MatchTeamSnapshotV2> Teams => Array.AsReadOnly(_teams);
        public string ContextHash { get; }

        public static MatchContextV2 Create(
            MatchVersionSetV2 versions, Guid sessionId, MatchExecutionModeV2 executionMode,
            string fixtureId, int? fixtureVersion, uint matchSeed, string competitionId,
            string scheduleItemId, int importanceBasisPoints, MatchFormatV2 format,
            PreMatchPriorityV2 preMatchPriority, IReadOnlyList<MatchTeamSnapshotV2> teams) =>
            new MatchContextV2(versions, sessionId, executionMode, fixtureId, fixtureVersion,
                matchSeed, competitionId, scheduleItemId, importanceBasisPoints, format,
                preMatchPriority, teams);

        internal void Validate()
        {
            ValidateMode(ExecutionMode, FixtureId, FixtureVersion, Versions);
            ValidateTeams(_teams);
            MatchV2Guard.Hash(ContextHash, nameof(ContextHash));
            if (!MatchV2Hash.FixedTimeEquals(ContextHash, MatchContractV2Json.ComputeContextHash(this)))
                throw new MatchV2ContractException("contextHash does not match the context payload.");
        }

        private static void ValidateMode(
            MatchExecutionModeV2 mode, string fixtureId, int? fixtureVersion, MatchVersionSetV2 versions)
        {
            var hasFixture = fixtureId != null || fixtureVersion.HasValue;
            var hasAlgorithms = versions.MatchSimulationVersion.HasValue || versions.MatchRandomAlgorithmVersion.HasValue;
            if (mode == MatchExecutionModeV2.Fixture)
            {
                MatchV2Guard.StableId(fixtureId, nameof(fixtureId));
                MatchV2Guard.Positive(fixtureVersion ?? 0, nameof(fixtureVersion));
                if (hasAlgorithms) throw new MatchV2ContractException("Fixture mode cannot set Match algorithm versions.");
            }
            else if (mode == MatchExecutionModeV2.Direct)
            {
                if (hasFixture || hasAlgorithms)
                    throw new MatchV2ContractException("Direct mode requires null fixture and Match algorithm versions.");
            }
            else if (mode == MatchExecutionModeV2.QuickSimulation)
            {
                if (hasFixture || !versions.MatchSimulationVersion.HasValue || !versions.MatchRandomAlgorithmVersion.HasValue)
                    throw new MatchV2ContractException("Quick simulation requires both Match algorithm versions and no fixture.");
            }
        }

        private static void ValidateTeams(IReadOnlyList<MatchTeamSnapshotV2> teams)
        {
            if (teams.Count != 2 || teams[0] == null || teams[1] == null)
                throw new MatchV2ContractException("A context requires home and away teams.");
            if (teams[0].Side != TeamSideV2.Home || teams[1].Side != TeamSideV2.Away)
                throw new MatchV2ContractException("Teams must be ordered home then away.");
            if (teams[0].TeamId.Equals(teams[1].TeamId))
                throw new MatchV2ContractException("Team IDs must be unique.");
            var ids = new HashSet<PlayerId>();
            foreach (var team in teams)
                foreach (var player in team.Players)
                    if (!ids.Add(player.PlayerId))
                        throw new MatchV2ContractException("Player IDs must be globally unique.");
        }
    }
}

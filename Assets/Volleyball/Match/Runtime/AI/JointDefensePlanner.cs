using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using ContractPlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.AI
{
    public readonly struct DefensePlayerSnapshotV3
    {
        public DefensePlayerSnapshotV3(ContractPlayerId id, SimVector3 position, float movementSpeed, float jump, bool isFrontRow)
        {
            if (!position.IsFinite || float.IsNaN(movementSpeed) || float.IsInfinity(movementSpeed) || movementSpeed < 0f ||
                float.IsNaN(jump) || float.IsInfinity(jump) || jump < 0f || jump > 1f) throw new ArgumentOutOfRangeException(nameof(position));
            Id = id; Position = position; MovementSpeed = movementSpeed; Jump = jump; IsFrontRow = isFrontRow;
        }
        public ContractPlayerId Id { get; } public SimVector3 Position { get; } public float MovementSpeed { get; }
        public float Jump { get; } public bool IsFrontRow { get; }
    }

    public sealed class JointDefensePlanningRequestV3
    {
        public JointDefensePlanningRequestV3(long revision, TeamSide defendingSide, PublicAttackThreatV3 publicThreat,
            IReadOnlyList<DefensePlayerSnapshotV3> players, IReadOnlyList<PlayerResponsibilityAssignmentV3> assignments,
            IReadOnlyList<ReorganizationExitV3> exits)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(TeamSide), defendingSide)) throw new ArgumentOutOfRangeException(nameof(defendingSide));
            Revision = revision; DefendingSide = defendingSide; PublicThreat = publicThreat ?? throw new ArgumentNullException(nameof(publicThreat));
            Players = Copy(players, nameof(players)); Assignments = Copy(assignments, nameof(assignments)); Exits = Copy(exits, nameof(exits));
            if (Players.Count != 6 || Assignments.Count != 6 || Players.Select(value => value.Id).Distinct().Count() != 6 ||
                Assignments.Select(value => value.PlayerId).Distinct().Count() != 6 ||
                Players.Select(value => value.Id).Except(Assignments.Select(value => value.PlayerId)).Any())
                throw new ArgumentException("Formal defense requires six matching on-court players and assignments.");
            if (Assignments.Select(value => value.SpatialClaim).Distinct().Count() != Assignments.Count)
                throw new ArgumentException("Hard spatial claims cannot conflict.", nameof(assignments));
        }
        public long Revision { get; } public TeamSide DefendingSide { get; } public PublicAttackThreatV3 PublicThreat { get; }
        public IReadOnlyList<DefensePlayerSnapshotV3> Players { get; } public IReadOnlyList<PlayerResponsibilityAssignmentV3> Assignments { get; }
        public IReadOnlyList<ReorganizationExitV3> Exits { get; }
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, string name)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("Values are required.", name);
            return new ReadOnlyCollection<T>(values.ToArray());
        }
    }

    public sealed class JointDefensePlanner
    {
        public JointDefensePlanV3 Plan(JointDefensePlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var orderedThreat = request.PublicThreat.Entries.OrderByDescending(value => value.Probability)
                .ThenBy(value => value.Zone, StringComparer.Ordinal).ThenBy(value => (int)value.ActionClass).ToArray();
            var blockZone = orderedThreat[0].Zone;
            // BlockUnitPlanner's legacy candidate identifier has only a numeric tie-breaker.
            // Derive it from the real contract player identity, never request enumeration,
            // then map the selected legacy candidates back to those stable identities.
            var stablePlayers = request.Players.OrderBy(value => value.Id.Value, StringComparer.Ordinal).ToArray();
            var identitiesByBlockCandidate = new Dictionary<PlayerId, ContractPlayerId>();
            var blockCandidates = stablePlayers.Select((value, index) =>
            {
                var candidateId = new PlayerId(
                    request.DefendingSide == TeamSide.Home ? TeamId.Blue : TeamId.Orange,
                    (PlayerRole)index, index);
                identitiesByBlockCandidate.Add(candidateId, value.Id);
                return new BlockCandidateSnapshot(candidateId, value.Position, value.MovementSpeed, value.Jump, value.IsFrontRow);
            }).ToArray();
            var units = BlockUnitPlanner.EvaluateUnits(blockCandidates, Intercept(blockZone), orderedThreat[0].ArrivalTime, true);
            var blockers = units.Count == 0 ? Array.Empty<BlockCandidateSnapshot>() : units[units.Count - 1].Blockers;
            var blockerIds = new HashSet<ContractPlayerId>(blockers.Select(value => identitiesByBlockCandidate[value.Id]));
            var residualZones = orderedThreat.Where(value => value.Zone != blockZone).Select(value => value.Zone).Distinct(StringComparer.Ordinal).ToArray();
            var assignments = request.Assignments.OrderBy(value => value.Rank).ThenBy(value => value.PlayerId.Value, StringComparer.Ordinal).ToArray();
            var responsibilities = new List<DefenseResponsibilityV3>(6);
            var residualIndex = 0;
            foreach (var assignment in assignments)
            {
                DefenseResponsibilityKindV3 kind;
                string zone;
                if (blockerIds.Contains(assignment.PlayerId)) { kind = responsibilities.Any(value => value.Kind == DefenseResponsibilityKindV3.PrimaryBlock) ? DefenseResponsibilityKindV3.SupportingBlock : DefenseResponsibilityKindV3.PrimaryBlock; zone = blockZone; }
                else if (residualZones.Length > 0) { kind = residualIndex++ == 0 ? DefenseResponsibilityKindV3.CrossDefense : DefenseResponsibilityKindV3.DeepDefense; zone = residualZones[(residualIndex - 1) % residualZones.Length]; }
                else { kind = DefenseResponsibilityKindV3.ReboundCoverage; zone = blockZone; }
                responsibilities.Add(new DefenseResponsibilityV3(assignment.PlayerId, kind, zone, RallyPlanBranchV3.Primary));
            }
            return new JointDefensePlanV3(request.PublicThreat.ThreatIdentity, responsibilities, request.Exits,
                blockers.Count == 0 ? Array.Empty<string>() : new[] { blockZone }, residualZones);
        }
        private static SimVector3 Intercept(string zone) => new SimVector3(zone == "Line" ? -1f : zone == "Cross" ? 1f : 0f, 3f, .25f);
    }
}

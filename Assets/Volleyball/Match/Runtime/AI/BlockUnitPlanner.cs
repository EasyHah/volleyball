using System;
using System.Collections.Generic;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public readonly struct BlockCandidateSnapshot
    {
        public BlockCandidateSnapshot(
            PlayerId id,
            SimVector3 position,
            float movementSpeed,
            float jump,
            bool isFrontRow)
        {
            if (!position.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            if (!IsFinite(movementSpeed) || movementSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementSpeed));
            }

            if (!IsFinite(jump) || jump < 0f || jump > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(jump));
            }

            Id = id;
            Position = position;
            MovementSpeed = movementSpeed;
            Jump = jump;
            IsFrontRow = isFrontRow;
        }

        public PlayerId Id { get; }
        public SimVector3 Position { get; }
        public float MovementSpeed { get; }
        public float Jump { get; }
        public bool IsFrontRow { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct BlockUnitPlan
    {
        public BlockUnitPlan(IReadOnlyList<BlockCandidateSnapshot> blockers)
        {
            Blockers = blockers ?? throw new ArgumentNullException(nameof(blockers));
        }

        public IReadOnlyList<BlockCandidateSnapshot> Blockers { get; }
    }

    public static class BlockUnitPlanner
    {
        private const float JumpScoreBonus = 0.35f;
        private const float MinimumLaneSeparation = 0.35f;

        public static BlockUnitPlan Select(
            IReadOnlyList<BlockCandidateSnapshot> candidates,
            SimVector3 intercept,
            float availableSeconds,
            bool requireFrontRow = false)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (!intercept.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(intercept));
            }

            if (float.IsNaN(availableSeconds) || float.IsInfinity(availableSeconds) ||
                availableSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(availableSeconds));
            }

            if (candidates.Count == 0)
            {
                return new BlockUnitPlan(Array.Empty<BlockCandidateSnapshot>());
            }

            var team = candidates[0].Id.Team;
            var reachable = new List<ScoredCandidate>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.Id.Team != team || requireFrontRow && !candidate.IsFrontRow)
                {
                    continue;
                }

                var distance = GroundDistance(candidate.Position, intercept);
                if (distance > candidate.MovementSpeed * availableSeconds + 0.0001f)
                {
                    continue;
                }

                reachable.Add(new ScoredCandidate(
                    candidate,
                    distance - (candidate.Jump * JumpScoreBonus)));
            }

            reachable.Sort(Compare);
            if (reachable.Count == 0)
            {
                return new BlockUnitPlan(Array.Empty<BlockCandidateSnapshot>());
            }

            var primary = reachable[0].Candidate;
            var selected = new List<BlockCandidateSnapshot>(3) { primary };
            AddBestAdjacent(reachable, primary, searchLeft: true, selected);
            AddBestAdjacent(reachable, primary, searchLeft: false, selected);
            return new BlockUnitPlan(selected.ToArray());
        }

        // Formal 6v6 composition consumes candidates, not an imperative selection.
        // Select remains the legacy 3v3 entry point above.
        public static IReadOnlyList<BlockUnitPlan> EvaluateUnits(
            IReadOnlyList<BlockCandidateSnapshot> candidates, SimVector3 intercept,
            float availableSeconds, bool requireFrontRow = false)
        {
            var selected = Select(candidates, intercept, availableSeconds, requireFrontRow).Blockers;
            var units = new List<BlockUnitPlan>();
            for (var count = 1; count <= selected.Count; count++)
            {
                var unit = new BlockCandidateSnapshot[count];
                for (var index = 0; index < count; index++) unit[index] = selected[index];
                units.Add(new BlockUnitPlan(unit));
            }
            return units;
        }

        private static void AddBestAdjacent(
            IReadOnlyList<ScoredCandidate> reachable,
            BlockCandidateSnapshot primary,
            bool searchLeft,
            ICollection<BlockCandidateSnapshot> selected)
        {
            for (var index = 1; index < reachable.Count; index++)
            {
                var candidate = reachable[index].Candidate;
                var offset = candidate.Position.X - primary.Position.X;
                if (searchLeft ? offset <= -MinimumLaneSeparation : offset >= MinimumLaneSeparation)
                {
                    selected.Add(candidate);
                    return;
                }
            }
        }

        private static int Compare(ScoredCandidate left, ScoredCandidate right)
        {
            var score = left.Score.CompareTo(right.Score);
            if (score != 0)
            {
                return score;
            }

            var slot = left.Candidate.Id.RosterSlot.CompareTo(right.Candidate.Id.RosterSlot);
            if (slot != 0)
            {
                return slot;
            }

            return ((int)left.Candidate.Id.Role).CompareTo((int)right.Candidate.Id.Role);
        }

        private static float GroundDistance(SimVector3 left, SimVector3 right)
        {
            var x = left.X - right.X;
            var z = left.Z - right.Z;
            return (float)Math.Sqrt((x * x) + (z * z));
        }

        private readonly struct ScoredCandidate
        {
            public ScoredCandidate(BlockCandidateSnapshot candidate, float score)
            {
                Candidate = candidate;
                Score = score;
            }

            public BlockCandidateSnapshot Candidate { get; }
            public float Score { get; }
        }
    }
}

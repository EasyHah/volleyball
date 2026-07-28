using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum RallyPlanTaskV3 { Cover, Receive, Set, Attack, Block, Defend }
    public enum RallyPlanConditionV3 { Always, BallOnOurSide, BallOnOpponentSide, AfterFirstContact, AfterSecondContact }
    public enum RallyPlanSpatialClaimV3 { FrontRight = 1, FrontCenter, FrontLeft, BackLeft, BackCenter, BackRight }
    public enum RallyPlanBranchV3 { Primary, Contingency }

    public sealed class PlayerResponsibilityAssignmentV3
    {
        public PlayerResponsibilityAssignmentV3(
            PlayerId playerId,
            RallyPlanTaskV3 task,
            RallyPlanConditionV3 condition,
            RallyPlanSpatialClaimV3 spatialClaim,
            RallyPlanBranchV3 branch,
            float value,
            int rank)
        {
            PlayerId = PlayerWorldSnapshotV3.RequirePlayerId(playerId, nameof(playerId));
            Task = PlayerWorldSnapshotV3.RequireDefinedEnum(task, nameof(task));
            Condition = PlayerWorldSnapshotV3.RequireDefinedEnum(condition, nameof(condition));
            SpatialClaim = PlayerWorldSnapshotV3.RequireDefinedEnum(spatialClaim, nameof(spatialClaim));
            Branch = PlayerWorldSnapshotV3.RequireDefinedEnum(branch, nameof(branch));
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be finite.");
            }

            if (rank < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be positive.");
            }

            Value = value;
            Rank = rank;
        }

        public PlayerId PlayerId { get; }
        public RallyPlanTaskV3 Task { get; }
        public RallyPlanConditionV3 Condition { get; }
        public RallyPlanSpatialClaimV3 SpatialClaim { get; }
        public RallyPlanBranchV3 Branch { get; }
        public float Value { get; }
        public int Rank { get; }
    }
}

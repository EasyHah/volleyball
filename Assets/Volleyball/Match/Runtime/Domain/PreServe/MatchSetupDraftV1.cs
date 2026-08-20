using System;
using System.Collections.Generic;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public sealed class MatchSetupDraftV1
    {
        private MatchSetupDraftV1(MatchContextV5 baseContext)
        {
            BaseContext = baseContext ?? throw new ArgumentNullException(nameof(baseContext));
            HomeRotation = new List<PlayerId>(6);
            AwayRotation = new List<PlayerId>(6);
            Players = new List<MatchPlayerPoseDraftV1>(12);
            AttributeOverrides = new Dictionary<PlayerId,
                TrainingPlayerAttributeOverrideV2>();
        }

        public MatchContextV5 BaseContext { get; }
        public TeamSide FirstServingSide { get; set; }
        public List<PlayerId> HomeRotation { get; }
        public List<PlayerId> AwayRotation { get; }
        public List<MatchPlayerPoseDraftV1> Players { get; }
        public SimVector3 BallPosition { get; internal set; }
        public SimVector3 BallVelocity { get; internal set; }
        public Dictionary<PlayerId, TrainingPlayerAttributeOverrideV2>
            AttributeOverrides { get; }
        public bool RotationLocked { get; set; }

        public static MatchSetupDraftV1 CreateDefault(
            MatchContextV5 context,
            TeamSide firstServingSide)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(TeamSide), firstServingSide))
                throw new ArgumentOutOfRangeException(nameof(firstServingSide));
            var draft = new MatchSetupDraftV1(context)
            {
                FirstServingSide = firstServingSide
            };
            AddTeam(draft, context.Home, TeamSide.Home, draft.HomeRotation);
            AddTeam(draft, context.Away, TeamSide.Away, draft.AwayRotation);
            var direction = firstServingSide == TeamSide.Home ? 1f : -1f;
            var depth = firstServingSide == TeamSide.Home
                ? -FormalCourtGeometryV1.HalfLengthMeters - .2f
                : FormalCourtGeometryV1.HalfLengthMeters + .2f;
            draft.BallPosition = new SimVector3(0f, 2.2f, depth);
            draft.BallVelocity = new SimVector3(0f, 2.5f, 10f * direction);
            return draft;
        }

        public static MatchSetupDraftV1 Restore(
            MatchContextV5 context,
            TeamSide firstServingSide,
            IEnumerable<PlayerId> homeRotation,
            IEnumerable<PlayerId> awayRotation,
            IEnumerable<MatchPlayerPoseDraftV1> players,
            SimVector3 ballPosition,
            SimVector3 ballVelocity,
            IReadOnlyDictionary<PlayerId, TrainingPlayerAttributeOverrideV2>
                attributeOverrides,
            bool rotationLocked)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var draft = new MatchSetupDraftV1(context)
            {
                FirstServingSide = firstServingSide,
                BallPosition = ballPosition,
                BallVelocity = ballVelocity,
                RotationLocked = rotationLocked
            };
            draft.HomeRotation.AddRange(homeRotation ??
                throw new ArgumentNullException(nameof(homeRotation)));
            draft.AwayRotation.AddRange(awayRotation ??
                throw new ArgumentNullException(nameof(awayRotation)));
            foreach (var player in players ??
                     throw new ArgumentNullException(nameof(players)))
                draft.Players.Add(player?.DeepCopy() ??
                    throw new ArgumentException("Player poses cannot contain null.",
                        nameof(players)));
            if (attributeOverrides != null)
            {
                foreach (var pair in attributeOverrides)
                    draft.AttributeOverrides.Add(pair.Key,
                        pair.Value?.DeepCopy() ?? throw new ArgumentException(
                            "Attribute overrides cannot contain null.",
                            nameof(attributeOverrides)));
            }
            new MatchSetupEditorV1(draft).Validate();
            return draft;
        }

        public MatchSetupDraftV1 DeepCopy()
        {
            return Restore(BaseContext, FirstServingSide, HomeRotation,
                AwayRotation, Players, BallPosition, BallVelocity,
                AttributeOverrides, RotationLocked);
        }

        private static void AddTeam(
            MatchSetupDraftV1 draft,
            TeamSnapshotV5 team,
            TeamSide side,
            ICollection<PlayerId> rotation)
        {
            if (team.Side != side || team.RotationOrder.Count != 6)
                throw new ArgumentException("V5 setup requires one six-player team per side.");
            for (var index = 0; index < team.RotationOrder.Count; index++)
            {
                var player = team.RotationOrder[index];
                rotation.Add(player.PlayerId);
                draft.Players.Add(new MatchPlayerPoseDraftV1(
                    player.PlayerId,
                    TrainingTeamCourtTransformV1.ToWorld(side,
                        DefaultLocalPosition(index + 1))));
            }
        }

        private static SimVector3 DefaultLocalPosition(int slot)
        {
            return slot switch
            {
                1 => new SimVector3(3.05f, 0f, 6.25f),
                2 => new SimVector3(3f, 0f, 2.35f),
                3 => new SimVector3(0f, 0f, 2.05f),
                4 => new SimVector3(-3f, 0f, 2.35f),
                5 => new SimVector3(-3.05f, 0f, 6.25f),
                6 => new SimVector3(0f, 0f, 5.55f),
                _ => throw new ArgumentOutOfRangeException(nameof(slot))
            };
        }
    }
}

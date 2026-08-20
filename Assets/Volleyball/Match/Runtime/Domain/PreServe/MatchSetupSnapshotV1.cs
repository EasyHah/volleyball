using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public sealed class MatchPlayerPoseSnapshotV1
    {
        internal MatchPlayerPoseSnapshotV1(PlayerId playerId, SimVector3 position)
        {
            PlayerId = playerId;
            Position = position;
        }

        public PlayerId PlayerId { get; }
        public SimVector3 Position { get; }
    }

    public sealed class MatchSetupAttributeOverrideSnapshotV1
    {
        internal MatchSetupAttributeOverrideSnapshotV1(
            PlayerId playerId,
            TrainingPlayerAttributeOverrideV2 value)
        {
            PlayerId = playerId;
            Strength = value.Strength;
            HeightMillimeters = value.HeightMillimeters;
            Jump = value.Jump;
            Movement = value.Movement;
            Reaction = value.Reaction;
            Coordination = value.Coordination;
            Attack = value.Attack;
            Defense = value.Defense;
            CourtIq = value.CourtIq;
            Block = value.Block;
            Serve = value.Serve;
            Setting = value.Setting;
            DominantHand = value.DominantHand;
        }

        public PlayerId PlayerId { get; }
        public int? Strength { get; }
        public int? HeightMillimeters { get; }
        public int? Jump { get; }
        public int? Movement { get; }
        public int? Reaction { get; }
        public int? Coordination { get; }
        public int? Attack { get; }
        public int? Defense { get; }
        public int? CourtIq { get; }
        public int? Block { get; }
        public int? Serve { get; }
        public int? Setting { get; }
        public DominantHandV5? DominantHand { get; }

        public CareerBaseAttributesV5 ApplyTo(CareerBaseAttributesV5 source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CareerBaseAttributesV5(
                Strength ?? source.Strength,
                HeightMillimeters ?? source.HeightMillimeters,
                Jump ?? source.Jump,
                Movement ?? source.Movement,
                Reaction ?? source.Reaction,
                Coordination ?? source.Coordination,
                Attack ?? source.Attack,
                Defense ?? source.Defense,
                CourtIq ?? source.CourtIq,
                Block ?? source.Block,
                Serve ?? source.Serve,
                Setting ?? source.Set);
        }
    }

    public sealed class MatchSetupSnapshotV1
    {
        private MatchSetupSnapshotV1(
            MatchContextV5 baseContext,
            TeamSide firstServingSide,
            IReadOnlyList<PlayerId> homeRotation,
            IReadOnlyList<PlayerId> awayRotation,
            IReadOnlyList<MatchPlayerPoseSnapshotV1> players,
            SimVector3 ballPosition,
            SimVector3 ballVelocity,
            bool rotationLocked,
            IReadOnlyList<MatchSetupAttributeOverrideSnapshotV1> overrides)
        {
            BaseContext = baseContext;
            FirstServingSide = firstServingSide;
            HomeRotation = new ReadOnlyCollection<PlayerId>(homeRotation.ToArray());
            AwayRotation = new ReadOnlyCollection<PlayerId>(awayRotation.ToArray());
            Players = new ReadOnlyCollection<MatchPlayerPoseSnapshotV1>(players.ToArray());
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            RotationLocked = rotationLocked;
            AttributeOverrides =
                new ReadOnlyCollection<MatchSetupAttributeOverrideSnapshotV1>(
                    overrides.ToArray());
            SetupHash = ComputeHash(this);
        }

        public MatchContextV5 BaseContext { get; }
        public TeamSide FirstServingSide { get; }
        public IReadOnlyList<PlayerId> HomeRotation { get; }
        public IReadOnlyList<PlayerId> AwayRotation { get; }
        public IReadOnlyList<MatchPlayerPoseSnapshotV1> Players { get; }
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }
        public bool RotationLocked { get; }
        public IReadOnlyList<MatchSetupAttributeOverrideSnapshotV1>
            AttributeOverrides { get; }
        public string SetupHash { get; }

        internal static MatchSetupSnapshotV1 Create(MatchSetupDraftV1 draft)
        {
            var players = draft.Players
                .OrderBy(value => value.PlayerId.Value, StringComparer.Ordinal)
                .Select(value => new MatchPlayerPoseSnapshotV1(
                    value.PlayerId, value.Position))
                .ToArray();
            var overrides = draft.AttributeOverrides
                .Where(pair => pair.Value.HasAny)
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(pair => new MatchSetupAttributeOverrideSnapshotV1(
                    pair.Key, pair.Value))
                .ToArray();
            return new MatchSetupSnapshotV1(
                draft.BaseContext,
                draft.FirstServingSide,
                draft.HomeRotation,
                draft.AwayRotation,
                players,
                draft.BallPosition,
                draft.BallVelocity,
                draft.RotationLocked,
                overrides);
        }

        private static string ComputeHash(MatchSetupSnapshotV1 value)
        {
            var output = new StringBuilder(4096);
            output.Append("volleyball.match-setup.v1\n");
            Part(output, value.BaseContext.ContextHash);
            output.Append((int)value.FirstServingSide).Append('\n');
            output.Append(value.RotationLocked ? "1\n" : "0\n");
            AppendIds(output, value.HomeRotation);
            AppendIds(output, value.AwayRotation);
            foreach (var player in value.Players)
            {
                Part(output, player.PlayerId.Value);
                Vector(output, player.Position);
            }
            Vector(output, value.BallPosition);
            Vector(output, value.BallVelocity);
            foreach (var item in value.AttributeOverrides)
            {
                Part(output, item.PlayerId.Value);
                Optional(output, item.Strength);
                Optional(output, item.HeightMillimeters);
                Optional(output, item.Jump);
                Optional(output, item.Movement);
                Optional(output, item.Reaction);
                Optional(output, item.Coordination);
                Optional(output, item.Attack);
                Optional(output, item.Defense);
                Optional(output, item.CourtIq);
                Optional(output, item.Block);
                Optional(output, item.Serve);
                Optional(output, item.Setting);
                Optional(output, item.DominantHand.HasValue
                    ? (int?)item.DominantHand.Value : null);
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(output.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var item in hash)
                hex.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }

        private static void AppendIds(StringBuilder output,
            IEnumerable<PlayerId> values)
        {
            foreach (var value in values) Part(output, value.Value);
        }

        private static void Vector(StringBuilder output, SimVector3 value)
        {
            output.Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void Optional(StringBuilder output, int? value)
        {
            output.Append(value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "-").Append('\n');
        }

        private static void Part(StringBuilder output, string value)
        {
            output.Append(value.Length).Append(':').Append(value).Append('\n');
        }
    }
}

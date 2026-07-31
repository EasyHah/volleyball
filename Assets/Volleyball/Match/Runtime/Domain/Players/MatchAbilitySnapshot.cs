using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Domain.Players
{
    /// <summary>
    /// Match-owned, version-neutral projection of frozen Shared attributes.
    /// Shared V4 and V5 are converted only at their scene entry boundaries.
    /// </summary>
    public sealed class MatchAbilitySnapshot
    {
        public MatchAbilitySnapshot(
            float attackDirectionControl, float attackSpeedControl, float attackPowerCapacity,
            float attackContactHeightMeters, float attackApproachMobility,
            float blockTiming, float blockHandControl, float blockReachHeightMeters,
            float blockLateralMobility, float defensePlatformControl, float defenseAwareness,
            float receiveControl, float receiveAwareness, float setPlacementControl,
            float setTempoControl, float setSoftTouch, float serveDirectionControl,
            float serveSpeedControl, float servePowerCapacity, string fingerprint)
        {
            AttackDirectionControl = Unit(attackDirectionControl, nameof(attackDirectionControl));
            AttackSpeedControl = Unit(attackSpeedControl, nameof(attackSpeedControl));
            AttackPowerCapacity = Unit(attackPowerCapacity, nameof(attackPowerCapacity));
            AttackContactHeightMeters = Positive(attackContactHeightMeters, nameof(attackContactHeightMeters));
            AttackApproachMobility = Unit(attackApproachMobility, nameof(attackApproachMobility));
            BlockTiming = Unit(blockTiming, nameof(blockTiming));
            BlockHandControl = Unit(blockHandControl, nameof(blockHandControl));
            BlockReachHeightMeters = Positive(blockReachHeightMeters, nameof(blockReachHeightMeters));
            BlockLateralMobility = Unit(blockLateralMobility, nameof(blockLateralMobility));
            DefensePlatformControl = Unit(defensePlatformControl, nameof(defensePlatformControl));
            DefenseAwareness = Unit(defenseAwareness, nameof(defenseAwareness));
            ReceiveControl = Unit(receiveControl, nameof(receiveControl));
            ReceiveAwareness = Unit(receiveAwareness, nameof(receiveAwareness));
            SetPlacementControl = Unit(setPlacementControl, nameof(setPlacementControl));
            SetTempoControl = Unit(setTempoControl, nameof(setTempoControl));
            SetSoftTouch = Unit(setSoftTouch, nameof(setSoftTouch));
            ServeDirectionControl = Unit(serveDirectionControl, nameof(serveDirectionControl));
            ServeSpeedControl = Unit(serveSpeedControl, nameof(serveSpeedControl));
            ServePowerCapacity = Unit(servePowerCapacity, nameof(servePowerCapacity));
            if (string.IsNullOrEmpty(fingerprint) || fingerprint.Length != 64)
                throw new ArgumentException("A SHA-256 fingerprint is required.", nameof(fingerprint));
            Fingerprint = fingerprint;
        }

        public float AttackDirectionControl { get; }
        public float AttackSpeedControl { get; }
        public float AttackPowerCapacity { get; }
        public float AttackContactHeightMeters { get; }
        public float AttackApproachMobility { get; }
        public float BlockTiming { get; }
        public float BlockHandControl { get; }
        public float BlockReachHeightMeters { get; }
        public float BlockLateralMobility { get; }
        public float DefensePlatformControl { get; }
        public float DefenseAwareness { get; }
        public float ReceiveControl { get; }
        public float ReceiveAwareness { get; }
        public float SetPlacementControl { get; }
        public float SetTempoControl { get; }
        public float SetSoftTouch { get; }
        public float ServeDirectionControl { get; }
        public float ServeSpeedControl { get; }
        public float ServePowerCapacity { get; }
        public string Fingerprint { get; }

        public static MatchAbilitySnapshot FromV4(DerivedMatchAttributesV4 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var a = value.Attributes;
            return new MatchAbilitySnapshot(a.Attack.DirectionControl, a.Attack.SpeedControl,
                a.Attack.PowerCapacity, a.Attack.ContactHeightMeters, a.Attack.ApproachMobility,
                a.Block.Timing, a.Block.HandControl, a.Block.ReachHeightMeters, a.Block.LateralMobility,
                a.Defense.PlatformControl, a.Defense.Awareness, a.Receive.FirstTouchControl,
                a.Receive.Awareness, a.Set.PlacementControl, a.Set.TempoControl, a.Set.SoftTouch,
                a.Serve.DirectionControl, a.Serve.SpeedControl, a.Serve.PowerCapacity, value.ResultFingerprint);
        }

        public static MatchAbilitySnapshot FromV5(DerivedMatchAttributesV5 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            const float scale = 1f / 10000f;
            return new MatchAbilitySnapshot(value.AttackControl * scale, value.AttackControl * scale,
                value.AttackPower * scale, value.AttackReachMillimeters / 1000f,
                value.Movement * scale, value.BlockControl * scale, value.BlockControl * scale,
                value.BlockReachMillimeters / 1000f, value.Movement * scale,
                value.DefenseControl * scale, value.CourtIq * scale, value.ReceiveControl * scale,
                value.CourtIq * scale, value.SetControl * scale, value.SetControl * scale,
                value.SetControl * scale, value.ServeControl * scale, value.ServeControl * scale,
                value.ServeControl * scale, value.ResultFingerprint);
        }

        private static float Unit(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(name);
            return value;
        }

        private static float Positive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }
}

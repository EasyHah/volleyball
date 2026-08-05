using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Domain.Players
{
    public enum TechniqueAction
    {
        Receive,
        Set,
        Attack,
        Block,
        Serve
    }

    public readonly struct PlayerAbilityProfile
    {
        private static readonly MatchAbilitySnapshot DefaultSnapshot = MatchAbilitySnapshot.FromV4(
            MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(1.90f, 2.47f, 0.8f, 0.8f, 0.8f, 0.8f),
                new TechnicalBaseAttributesV4(
                    0.8f,
                    0.8f,
                    0.8f,
                    0.8f,
                    0.8f,
                    0.8f,
                    0.8f,
                    0.8f,
                    0.8f),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1));

        public PlayerAbilityProfile(DerivedMatchAttributesV4 derived)
        {
            Derived = derived ?? throw new ArgumentNullException(nameof(derived));
            Snapshot = MatchAbilitySnapshot.FromV4(derived);
        }

        private PlayerAbilityProfile(DerivedMatchAttributesV5 derived)
        {
            Derived = null;
            Snapshot = MatchAbilitySnapshot.FromV5(derived);
        }

        public static PlayerAbilityProfile FromV5(DerivedMatchAttributesV5 derived) =>
            new PlayerAbilityProfile(derived);

        public static PlayerAbilityProfile Default =>
            new PlayerAbilityProfile(DefaultSnapshot);

        private PlayerAbilityProfile(MatchAbilitySnapshot snapshot)
        {
            Derived = null;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public MatchAbilitySnapshot Snapshot { get; }

        // Retained only for legacy V4 test and replay callers. V5 runtime code
        // must consume Snapshot and never reads a V4-derived DTO.
        public DerivedMatchAttributesV4 Derived { get; }

        public float Mobility => Snapshot.DefenseCoverageMobility;

        public float Reaction => Snapshot.DefenseReaction;

        public float Jump => Snapshot.BlockTiming;

        public float ReceiveTechnique => Snapshot.ReceiveControl;

        public float SetTechnique => Snapshot.SetPlacementControl;

        public float AttackDirectionControl => Snapshot.AttackDirectionControl;

        public float AttackSpeedControl => Snapshot.AttackSpeedControl;

        public float AttackPowerCapacity => Snapshot.AttackPowerCapacity;

        public float PlannedAttackContactHeightMeters =>
            Snapshot.AttackContactHeightMeters;

        public float TechniqueFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => Snapshot.ReceiveControl,
                TechniqueAction.Set => Snapshot.SetPlacementControl,
                TechniqueAction.Attack => Snapshot.AttackDirectionControl,
                TechniqueAction.Block => Snapshot.BlockHandControl,
                TechniqueAction.Serve => Snapshot.ServeDirectionControl,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

    }
}

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
        private static readonly DerivedMatchAttributesV4 DefaultDerived =
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
                MatchAttributeDerivationConfigV4.Version1);

        public PlayerAbilityProfile(DerivedMatchAttributesV4 derived)
        {
            Derived = derived ?? throw new ArgumentNullException(nameof(derived));
        }

        public static PlayerAbilityProfile Default =>
            new PlayerAbilityProfile(DefaultDerived);

        public DerivedMatchAttributesV4 Derived { get; }

        public MatchAttributesV4 Attributes => RequireDerived().Attributes;

        public float Mobility => Attributes.Defense.CoverageMobility;

        public float Reaction => Attributes.Defense.Reaction;

        public float Jump => Attributes.Block.Timing;

        public float ReceiveTechnique => Attributes.Receive.FirstTouchControl;

        public float SetTechnique => Attributes.Set.PlacementControl;

        public float AttackDirectionControl => Attributes.Attack.DirectionControl;

        public float AttackSpeedControl => Attributes.Attack.SpeedControl;

        public float AttackPowerCapacity => Attributes.Attack.PowerCapacity;

        public float PlannedAttackContactHeightMeters =>
            Attributes.Attack.ContactHeightMeters;

        public float TechniqueFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => Attributes.Receive.FirstTouchControl,
                TechniqueAction.Set => Attributes.Set.PlacementControl,
                TechniqueAction.Attack => Attributes.Attack.DirectionControl,
                TechniqueAction.Block => Attributes.Block.HandControl,
                TechniqueAction.Serve => Attributes.Serve.DirectionControl,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private DerivedMatchAttributesV4 RequireDerived()
        {
            return Derived ?? throw new InvalidOperationException(
                "A player ability profile must wrap V4 derived match attributes.");
        }
    }
}

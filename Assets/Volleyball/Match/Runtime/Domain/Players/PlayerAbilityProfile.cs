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
        public PlayerAbilityProfile(
            float mobility,
            float reaction,
            float jump,
            float receiveTechnique,
            float setTechnique,
            float attackTechnique,
            float attackPower)
        {
            Mobility = Validate(mobility, nameof(mobility));
            Reaction = Validate(reaction, nameof(reaction));
            Jump = Validate(jump, nameof(jump));
            ReceiveTechnique = Validate(receiveTechnique, nameof(receiveTechnique));
            SetTechnique = Validate(setTechnique, nameof(setTechnique));
            AttackTechnique = Validate(attackTechnique, nameof(attackTechnique));
            AttackPower = Validate(attackPower, nameof(attackPower));
        }

        public PlayerAbilityProfile(PlayerAbilitySnapshotV1 snapshot)
            : this(
                snapshot?.Mobility ?? throw new ArgumentNullException(nameof(snapshot)),
                snapshot.Reaction,
                snapshot.Jump,
                snapshot.ReceiveTechnique,
                snapshot.SetTechnique,
                snapshot.AttackTechnique,
                snapshot.AttackPower)
        {
        }

        public static PlayerAbilityProfile Default =>
            new PlayerAbilityProfile(0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f);

        public float Mobility { get; }

        public float Reaction { get; }

        public float Jump { get; }

        public float ReceiveTechnique { get; }

        public float SetTechnique { get; }

        public float AttackTechnique { get; }

        public float AttackPower { get; }

        public PlayerAbilitySnapshotV1 ToSnapshot()
        {
            return new PlayerAbilitySnapshotV1(
                Mobility,
                Reaction,
                Jump,
                ReceiveTechnique,
                SetTechnique,
                AttackTechnique,
                AttackPower);
        }

        public float TechniqueFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => ReceiveTechnique,
                TechniqueAction.Set => SetTechnique,
                TechniqueAction.Attack => AttackTechnique,
                TechniqueAction.Block => ReceiveTechnique,
                TechniqueAction.Serve => AttackTechnique,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static float Validate(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Ability must be finite and in the range [0, 1].");
            }

            return value;
        }
    }
}

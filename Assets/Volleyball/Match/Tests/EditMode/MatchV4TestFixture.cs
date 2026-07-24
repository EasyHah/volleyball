using System;
using System.Collections.Generic;
using Volleyball.Domain.Players;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    internal static class MatchV4TestFixture
    {
        public static MatchContextV4 CreateContext(
            Guid? sessionId = null,
            int seed = 7351,
            float attackTechnique = 0.76f,
            float attackPower = 0.77f)
        {
            return MatchContextV4.Create(
                sessionId ?? Guid.Parse("66666666-6666-6666-6666-666666666666"),
                seed,
                CreateTeam("home6", "Home 6", TeamSide.Home, "home", attackTechnique, attackPower),
                CreateTeam("away6", "Away 6", TeamSide.Away, "away", attackTechnique, attackPower),
                new string('a', 64),
                rulesVersion: ContractVersions.MatchV3);
        }

        public static TeamSnapshotV4 CreateTeam(
            string id,
            string name,
            TeamSide side,
            string prefix,
            float attackTechnique = 0.76f,
            float attackPower = 0.77f)
        {
            return new TeamSnapshotV4(
                new TeamId(id),
                name,
                side,
                new List<PlayerSnapshotV4>
                {
                    CreatePlayer(prefix + "-opposite", "Opposite", 1, PlayerPosition.Opposite, attackTechnique, attackPower),
                    CreatePlayer(prefix + "-outside-a", "Outside A", 2, PlayerPosition.OutsideHitter, attackTechnique, attackPower),
                    CreatePlayer(prefix + "-middle-a", "Middle A", 3, PlayerPosition.MiddleBlocker, attackTechnique, attackPower),
                    CreatePlayer(prefix + "-setter", "Setter", 4, PlayerPosition.Setter, attackTechnique, attackPower),
                    CreatePlayer(prefix + "-outside-b", "Outside B", 5, PlayerPosition.OutsideHitter, attackTechnique, attackPower),
                    CreatePlayer(prefix + "-libero", "Libero", 6, PlayerPosition.Libero, attackTechnique, attackPower)
                });
        }

        public static PlayerSnapshotV4 CreatePlayer(
            string id = "career-player-0042",
            string name = "Player",
            int number = 4,
            PlayerPosition position = PlayerPosition.Setter,
            float attackTechnique = 0.76f,
            float attackPower = 0.77f)
        {
            return new PlayerSnapshotV4(
                new PlayerId(id),
                name,
                number,
                position,
                DominantHandV4.Right,
                CreatePhysical(),
                CreateTechnical(attackTechnique, attackPower),
                MatchAttributeDerivationConfigV4.Version1);
        }

        public static DerivedMatchAttributesV4 CreateDerived(
            float attackTechnique = 0.76f,
            float attackPower = 0.77f)
        {
            return MatchAttributeDerivationV4.Derive(
                CreatePhysical(),
                CreateTechnical(attackTechnique, attackPower),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }

        public static PlayerAbilityProfile CreateAbility(
            float mobility,
            float reaction,
            float jump,
            float receiveTechnique,
            float setTechnique,
            float attackTechnique,
            float attackPower,
            float plannedContactHeightMeters = 3.20f)
        {
            var standingReach = Math.Max(
                2.20f,
                Math.Min(
                    2.80f,
                    plannedContactHeightMeters - 0.25f - (0.60f * jump)));
            var derived = MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(
                    1.90f,
                    standingReach,
                    jump,
                    mobility,
                    reaction,
                    0.8f),
                new TechnicalBaseAttributesV4(
                    attackTechnique,
                    attackPower,
                    jump,
                    receiveTechnique,
                    receiveTechnique,
                    setTechnique,
                    attackTechnique,
                    setTechnique,
                    reaction),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
            return new PlayerAbilityProfile(derived);
        }

        private static PhysicalBaseAttributesV4 CreatePhysical()
        {
            return new PhysicalBaseAttributesV4(
                1.91f,
                2.43f,
                0.73f,
                0.71f,
                0.72f,
                0.70f);
        }

        private static TechnicalBaseAttributesV4 CreateTechnical(
            float attackTechnique,
            float attackPower)
        {
            return new TechnicalBaseAttributesV4(
                attackTechnique,
                attackPower,
                0.72f,
                0.73f,
                0.74f,
                0.75f,
                0.76f,
                0.72f,
                0.78f);
        }
    }
}

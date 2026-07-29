using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public enum CareerTrainingDirection
    {
        Spike = 0,
        Serve = 1,
        Reception = 2,
        Defense = 3,
        Block = 4,
        Movement = 5,
        Jump = 6,
        Stamina = 7
    }

    public sealed class CareerWeekActionContentDefinition
    {
        public CareerWeekActionContentDefinition(
            string contentId,
            CareerWeekActionKind kind,
            CareerTrainingDirection? direction,
            int baseGrowthExperience,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta,
            int? mindsetTarget = null,
            int mindsetMaximumStep = 0)
        {
            ContentId = CareerSaveModelGuard.BusinessId(contentId, nameof(contentId));
            CareerSaveModelGuard.DefinedEnum(kind, nameof(kind));
            if (direction.HasValue)
            {
                CareerSaveModelGuard.DefinedEnum(direction.Value, nameof(direction));
            }

            if (baseGrowthExperience < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseGrowthExperience));
            }

            CareerSaveModelGuard.InclusiveRange(fatigueDelta, -100, 100, nameof(fatigueDelta));
            CareerSaveModelGuard.InclusiveRange(mindsetDelta, -100, 100, nameof(mindsetDelta));
            CareerSaveModelGuard.InclusiveRange(coachTrustDelta, -100, 100, nameof(coachTrustDelta));
            if (mindsetTarget.HasValue)
            {
                CareerSaveModelGuard.InclusiveRange(mindsetTarget.Value, 0, 100, nameof(mindsetTarget));
            }

            CareerSaveModelGuard.InclusiveRange(
                mindsetMaximumStep,
                0,
                100,
                nameof(mindsetMaximumStep));

            Kind = kind;
            Direction = direction;
            BaseGrowthExperience = baseGrowthExperience;
            FatigueDelta = fatigueDelta;
            MindsetDelta = mindsetDelta;
            CoachTrustDelta = coachTrustDelta;
            MindsetTarget = mindsetTarget;
            MindsetMaximumStep = mindsetMaximumStep;
        }

        public string ContentId { get; }
        public CareerWeekActionKind Kind { get; }
        public CareerTrainingDirection? Direction { get; }
        public int BaseGrowthExperience { get; }
        public int FatigueDelta { get; }
        public int MindsetDelta { get; }
        public int CoachTrustDelta { get; }
        public int? MindsetTarget { get; }
        public int MindsetMaximumStep { get; }

        internal CareerWeekActionContentDefinition Copy()
        {
            return new CareerWeekActionContentDefinition(
                ContentId,
                Kind,
                Direction,
                BaseGrowthExperience,
                FatigueDelta,
                MindsetDelta,
                CoachTrustDelta,
                MindsetTarget,
                MindsetMaximumStep);
        }
    }

    public sealed class CareerWeekActionCatalog
    {
        private readonly CareerWeekActionContentDefinition[] _actions;
        private readonly ReadOnlyCollection<CareerWeekActionContentDefinition> _readOnlyActions;

        public CareerWeekActionCatalog(
            int contentVersion,
            int rulesetVersion,
            IEnumerable<CareerWeekActionContentDefinition> actions)
        {
            if (contentVersion != 1 || rulesetVersion != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contentVersion),
                    "Only first-week content/rules V1 is supported.");
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var copied = new List<CareerWeekActionContentDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in actions)
            {
                if (action == null || !ids.Add(action.ContentId))
                {
                    throw new ArgumentException(
                        "Action definitions must be non-null and globally unique.",
                        nameof(actions));
                }

                copied.Add(action.Copy());
            }

            ValidateV1Shape(copied);
            ContentVersion = contentVersion;
            RulesetVersion = rulesetVersion;
            _actions = copied.ToArray();
            _readOnlyActions = Array.AsReadOnly(_actions);
        }

        public int ContentVersion { get; }
        public int RulesetVersion { get; }
        public IReadOnlyList<CareerWeekActionContentDefinition> Actions => _readOnlyActions;

        public CareerWeekActionContentDefinition Find(string contentId)
        {
            CareerSaveModelGuard.BusinessId(contentId, nameof(contentId));
            for (var index = 0; index < _actions.Length; index++)
            {
                if (string.Equals(_actions[index].ContentId, contentId, StringComparison.Ordinal))
                {
                    return _actions[index];
                }
            }

            return null;
        }

        private static void ValidateV1Shape(IReadOnlyList<CareerWeekActionContentDefinition> actions)
        {
            var expected = CareerWeekActionCatalogV1.CreateDefinitions();
            if (actions.Count != expected.Length)
            {
                throw new ArgumentException(
                    "First-week content V1 requires exactly eleven ordered actions.",
                    nameof(actions));
            }

            for (var index = 0; index < expected.Length; index++)
            {
                var actual = actions[index];
                var canonical = expected[index];
                if (!string.Equals(actual.ContentId, canonical.ContentId, StringComparison.Ordinal) ||
                    actual.Kind != canonical.Kind ||
                    actual.Direction != canonical.Direction ||
                    actual.BaseGrowthExperience != canonical.BaseGrowthExperience ||
                    actual.FatigueDelta != canonical.FatigueDelta ||
                    actual.MindsetDelta != canonical.MindsetDelta ||
                    actual.CoachTrustDelta != canonical.CoachTrustDelta ||
                    actual.MindsetTarget != canonical.MindsetTarget ||
                    actual.MindsetMaximumStep != canonical.MindsetMaximumStep)
                {
                    throw new ArgumentException(
                        "First-week content V1 identity, order, semantics, and tuning are closed.",
                        nameof(actions));
                }
            }
        }
    }

    public static class CareerWeekActionCatalogV1
    {
        public static CareerWeekActionCatalog Create()
        {
            return new CareerWeekActionCatalog(1, 1, CreateDefinitions());
        }

        internal static CareerWeekActionContentDefinition[] CreateDefinitions()
        {
            return new[]
            {
                Training("week_action.specialized.spike", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Spike, 120, 8),
                Training("week_action.specialized.serve", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Serve, 120, 8),
                Training("week_action.specialized.reception", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Reception, 120, 8),
                Training("week_action.specialized.defense", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Defense, 120, 8),
                Training("week_action.specialized.block", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Block, 120, 8),
                Training("week_action.strength.movement", CareerWeekActionKind.StrengthTraining, CareerTrainingDirection.Movement, 100, 12),
                Training("week_action.strength.jump", CareerWeekActionKind.StrengthTraining, CareerTrainingDirection.Jump, 100, 12),
                Training("week_action.strength.stamina", CareerWeekActionKind.StrengthTraining, CareerTrainingDirection.Stamina, 100, 12),
                new CareerWeekActionContentDefinition(
                    "week_action.team_practice.standard",
                    CareerWeekActionKind.TeamPractice,
                    null,
                    20,
                    6,
                    0,
                    5),
                new CareerWeekActionContentDefinition(
                    "week_action.rest.standard",
                    CareerWeekActionKind.Rest,
                    null,
                    0,
                    -18,
                    0,
                    0,
                    50,
                    5),
                new CareerWeekActionContentDefinition(
                    "schedule.u1w1.match.01",
                    CareerWeekActionKind.Match,
                    null,
                    0,
                    0,
                    0,
                    0)
            };
        }

        private static CareerWeekActionContentDefinition Training(
            string contentId,
            CareerWeekActionKind kind,
            CareerTrainingDirection direction,
            int baseGrowthExperience,
            int fatigueDelta)
        {
            return new CareerWeekActionContentDefinition(
                contentId,
                kind,
                direction,
                baseGrowthExperience,
                fatigueDelta,
                0,
                0);
        }
    }

    public static class CareerTrainingDirectionIds
    {
        public static string Format(CareerTrainingDirection direction)
        {
            switch (direction)
            {
                case CareerTrainingDirection.Spike: return "spike";
                case CareerTrainingDirection.Serve: return "serve";
                case CareerTrainingDirection.Reception: return "reception";
                case CareerTrainingDirection.Defense: return "defense";
                case CareerTrainingDirection.Block: return "block";
                case CareerTrainingDirection.Movement: return "movement";
                case CareerTrainingDirection.Jump: return "jump";
                case CareerTrainingDirection.Stamina: return "stamina";
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static CareerTrainingDirection Parse(string value)
        {
            switch (value)
            {
                case "spike": return CareerTrainingDirection.Spike;
                case "serve": return CareerTrainingDirection.Serve;
                case "reception": return CareerTrainingDirection.Reception;
                case "defense": return CareerTrainingDirection.Defense;
                case "block": return CareerTrainingDirection.Block;
                case "movement": return CareerTrainingDirection.Movement;
                case "jump": return CareerTrainingDirection.Jump;
                case "stamina": return CareerTrainingDirection.Stamina;
                default:
                    throw new ArgumentException("Unknown training direction identifier.", nameof(value));
            }
        }
    }
}

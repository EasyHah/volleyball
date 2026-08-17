using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public static class TrainingLabV5OverrideInspectorV2
    {
        public static void Render(VisualElement grid,
            TrainingLabWorkbenchControllerV2 controller, PlayerId playerId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (controller == null) throw new ArgumentNullException(
                nameof(controller));
            var original = controller.BasePlayer(playerId);
            var effective = controller.EffectiveBases(playerId);
            var explicitValue = controller.ExplicitOverride(playerId);
            grid.Clear();
            foreach (TrainingPlayerAttributeFieldV2 field in Enum.GetValues(
                         typeof(TrainingPlayerAttributeFieldV2)))
            {
                if (field == TrainingPlayerAttributeFieldV2.DominantHand)
                    continue;
                var captured = field;
                var row = new VisualElement
                {
                    name = "v5-override-" + field.ToString().ToLowerInvariant()
                };
                row.AddToClassList("v5-override-row");
                var hasOverride = ExplicitValue(explicitValue, field).HasValue;
                row.EnableInClassList("explicit-override", hasOverride);
                row.Add(new Label(field + " · 原 " +
                    BaseValue(original.Bases, field) + " · 有效 " +
                    BaseValue(effective, field)));
                var input = new IntegerField
                {
                    value = BaseValue(effective, field),
                    name = row.name + "-input"
                };
                input.RegisterValueChangedCallback(change =>
                    controller.SetPlayerAttributeOverride(playerId,
                        captured, change.newValue));
                row.Add(input);
                var clear = new Button(() =>
                    controller.ClearPlayerAttributeOverride(playerId,
                        captured))
                {
                    text = "清除",
                    name = row.name + "-clear"
                };
                clear.SetEnabled(hasOverride);
                row.Add(clear);
                grid.Add(row);
            }

            var handRow = new VisualElement
            {
                name = "v5-override-dominanthand"
            };
            handRow.AddToClassList("v5-override-row");
            var handOverride = explicitValue?.DominantHand;
            handRow.EnableInClassList("explicit-override",
                handOverride.HasValue);
            handRow.Add(new Label("DominantHand · 原 " +
                original.DominantHand + " · 有效 " +
                controller.EffectiveDominantHand(playerId)));
            var hand = new DropdownField(new List<string>
                { "Left", "Right" },
                controller.EffectiveDominantHand(playerId).ToString());
            hand.RegisterValueChangedCallback(change =>
                controller.SetPlayerDominantHandOverride(playerId,
                    Enum.Parse<DominantHandV5>(change.newValue)));
            handRow.Add(hand);
            var clearHand = new Button(() =>
                controller.ClearPlayerAttributeOverride(playerId,
                    TrainingPlayerAttributeFieldV2.DominantHand))
            {
                text = "清除",
                name = "v5-override-dominanthand-clear"
            };
            clearHand.SetEnabled(handOverride.HasValue);
            handRow.Add(clearHand);
            grid.Add(handRow);
        }

        private static int BaseValue(CareerBaseAttributesV5 value,
            TrainingPlayerAttributeFieldV2 field)
        {
            return field switch
            {
                TrainingPlayerAttributeFieldV2.Strength => value.Strength,
                TrainingPlayerAttributeFieldV2.Height => value.HeightMillimeters,
                TrainingPlayerAttributeFieldV2.Jump => value.Jump,
                TrainingPlayerAttributeFieldV2.Movement => value.Movement,
                TrainingPlayerAttributeFieldV2.Reaction => value.Reaction,
                TrainingPlayerAttributeFieldV2.Coordination => value.Coordination,
                TrainingPlayerAttributeFieldV2.Attack => value.Attack,
                TrainingPlayerAttributeFieldV2.Defense => value.Defense,
                TrainingPlayerAttributeFieldV2.CourtIq => value.CourtIq,
                TrainingPlayerAttributeFieldV2.Block => value.Block,
                TrainingPlayerAttributeFieldV2.Serve => value.Serve,
                TrainingPlayerAttributeFieldV2.Set => value.Set,
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            };
        }

        private static int? ExplicitValue(
            TrainingPlayerAttributeOverrideV2 value,
            TrainingPlayerAttributeFieldV2 field)
        {
            if (value == null) return null;
            return field switch
            {
                TrainingPlayerAttributeFieldV2.Strength => value.Strength,
                TrainingPlayerAttributeFieldV2.Height => value.HeightMillimeters,
                TrainingPlayerAttributeFieldV2.Jump => value.Jump,
                TrainingPlayerAttributeFieldV2.Movement => value.Movement,
                TrainingPlayerAttributeFieldV2.Reaction => value.Reaction,
                TrainingPlayerAttributeFieldV2.Coordination => value.Coordination,
                TrainingPlayerAttributeFieldV2.Attack => value.Attack,
                TrainingPlayerAttributeFieldV2.Defense => value.Defense,
                TrainingPlayerAttributeFieldV2.CourtIq => value.CourtIq,
                TrainingPlayerAttributeFieldV2.Block => value.Block,
                TrainingPlayerAttributeFieldV2.Serve => value.Serve,
                TrainingPlayerAttributeFieldV2.Set => value.Setting,
                _ => null
            };
        }
    }
}

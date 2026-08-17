using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public static class TrainingScenarioCatalogV2
    {
        public static IReadOnlyList<string> ScenarioIds { get; } = new[]
        {
            "standard-rotation",
            "home-serve",
            "away-serve",
            "position-fault-home",
            "position-fault-away",
            "attribute-override"
        };

        public static TrainingScenarioTemplateV2 Create(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) ||
                !Contains(scenarioId))
                throw new ArgumentOutOfRangeException(nameof(scenarioId));
            var preset = Resources.Load<TrainingScenarioPresetV2>(
                "TrainingScenariosV2/" + scenarioId);
            if (preset == null)
                throw new InvalidOperationException(
                    "Missing built-in V2 training scenario: " + scenarioId);
            return preset.ToDefinition();
        }

        public static MatchSetupDraftV1 CreateSetup(string scenarioId)
        {
            var template = Create(scenarioId);
            var servingSide = string.Equals(scenarioId, "away-serve",
                StringComparison.Ordinal) || string.Equals(scenarioId,
                "position-fault-away", StringComparison.Ordinal)
                ? TeamSide.Away
                : TeamSide.Home;
            var setup = MatchSetupDraftV1.CreateDefault(template.Context,
                servingSide);
            var editor = new MatchSetupEditorV1(setup);
            if (string.Equals(scenarioId, "position-fault-home",
                    StringComparison.Ordinal))
                editor.SetPlayerPosition(setup.HomeRotation[3],
                    TrainingTeamCourtTransformV1.ToWorld(TeamSide.Home,
                        new SimVector3(-3f, 0f, 7f)));
            else if (string.Equals(scenarioId, "position-fault-away",
                         StringComparison.Ordinal))
                editor.SetPlayerPosition(setup.AwayRotation[3],
                    TrainingTeamCourtTransformV1.ToWorld(TeamSide.Away,
                        new SimVector3(-3f, 0f, 7f)));
            else if (string.Equals(scenarioId, "attribute-override",
                         StringComparison.Ordinal))
            {
                var value = new TrainingPlayerAttributeOverrideV2();
                value.Set(TrainingPlayerAttributeFieldV2.Attack, 9000);
                value.Set(TrainingPlayerAttributeFieldV2.Height, 2100);
                value.SetDominantHand(DominantHandV5.Left);
                setup.AttributeOverrides.Add(setup.HomeRotation[0], value);
            }
            return setup;
        }

        private static bool Contains(string value)
        {
            foreach (var id in ScenarioIds)
                if (string.Equals(id, value, StringComparison.Ordinal)) return true;
            return false;
        }

    }
}

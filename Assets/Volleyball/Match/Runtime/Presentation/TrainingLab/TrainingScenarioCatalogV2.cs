using System;
using System.Collections.Generic;
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
            var context = FormalSixVsSixRallyBootstrap.CreateDefaultFormalContextV5();
            var id = TrainingScenarioTemplateV2.ScenarioIdPrefix + scenarioId;
            var displayName = DisplayName(scenarioId);
            return new TrainingScenarioTemplateV2(
                id,
                displayName,
                "project-catalog-v2",
                context,
                TrainingScenarioCanonicalizerV2.ComputeTemplateHash(
                    id, displayName, context));
        }

        private static bool Contains(string value)
        {
            foreach (var id in ScenarioIds)
                if (string.Equals(id, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string DisplayName(string id)
        {
            return id switch
            {
                "standard-rotation" => "标准轮转",
                "home-serve" => "主队发球",
                "away-serve" => "客队发球",
                "position-fault-home" => "主队位置错误",
                "position-fault-away" => "客队位置错误",
                "attribute-override" => "V5 属性覆盖",
                _ => throw new ArgumentOutOfRangeException(nameof(id))
            };
        }
    }
}

using System;
using UnityEngine;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingScenarioTemplateV2
    {
        public const int CurrentFormatVersion = 2;
        public const string ScenarioIdPrefix = "training-v2/";

        internal TrainingScenarioTemplateV2(
            string scenarioId,
            string displayName,
            string source,
            MatchContextV5 context,
            string contentHash)
        {
            ScenarioId = scenarioId;
            DisplayName = displayName;
            Source = source;
            Context = context;
            ContentHash = contentHash;
        }

        public int FormatVersion => CurrentFormatVersion;
        public string ScenarioId { get; }
        public string DisplayName { get; }
        public string Source { get; }
        public MatchContextV5 Context { get; }
        public string ContentHash { get; }
    }

    [CreateAssetMenu(
        fileName = "TrainingScenarioV2",
        menuName = "Volleyball/Formal Training Scenario V2")]
    public sealed class TrainingScenarioPresetV2 : ScriptableObject
    {
        [SerializeField] private int formatVersion =
            TrainingScenarioTemplateV2.CurrentFormatVersion;
        [SerializeField] private string scenarioId = "training-v2/new";
        [SerializeField] private string displayName = "New V5 Training Scenario";
        [SerializeField] private string source = "project";
        [TextArea(8, 30)] [SerializeField] private string matchContextJson;
        [SerializeField] private string contentHash;

        public TrainingScenarioTemplateV2 ToDefinition()
        {
            if (formatVersion != TrainingScenarioTemplateV2.CurrentFormatVersion)
                throw new InvalidOperationException(
                    "不支持的 TrainingLab 情景版本；V1 文件必须保留原样并重新创建。" );
            if (string.IsNullOrWhiteSpace(scenarioId) ||
                !scenarioId.StartsWith(TrainingScenarioTemplateV2.ScenarioIdPrefix,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("V2 training scenario ID is invalid.");
            if (string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(matchContextJson) ||
                string.IsNullOrWhiteSpace(contentHash))
                throw new InvalidOperationException(
                    "V2 training scenario metadata is incomplete.");

            var context = ContractJson.DeserializeMatchContextV5(matchContextJson);
            var expected = TrainingScenarioCanonicalizerV2.ComputeTemplateHash(
                scenarioId, displayName, context);
            if (!string.Equals(contentHash, expected, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "V2 training scenario content hash does not match its payload.");
            return new TrainingScenarioTemplateV2(
                scenarioId, displayName, source, context, contentHash);
        }
    }
}

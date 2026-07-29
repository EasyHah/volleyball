using UnityEngine;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    [CreateAssetMenu(
        fileName = "FormalMatchScenarioV4",
        menuName = "Volleyball/Formal Match Scenario V4")]
    public sealed class FormalMatchScenarioPresetV4 : ScriptableObject
    {
        [SerializeField] private string scenarioId;
        [SerializeField] private int formatVersion = FormalMatchScenarioDefinitionV4.FormatVersion;
        [TextArea(8, 30)] [SerializeField] private string matchContextJson;
        [SerializeField] private TeamSide firstServingSide = TeamSide.Home;
        [Range(0, 5)] [SerializeField] private int homeInitialRotationOffset;
        [Range(0, 5)] [SerializeField] private int awayInitialRotationOffset;
        [SerializeField] private string configurationIdentity =
            FormalMatchScenarioDefinitionV4.FormalIndoorConfigurationIdentity;
        [SerializeField] private FormalMatchTacticInputV4 homeTactics;
        [SerializeField] private FormalMatchTacticInputV4 awayTactics;
        [SerializeField] private FormalMatchAiInputV4 ai;
        [SerializeField] private string contentHash;

        public FormalMatchScenarioDefinitionV4 ToDefinition()
        {
            if (string.IsNullOrWhiteSpace(matchContextJson))
            {
                throw new System.InvalidOperationException(
                    "Formal scenario requires a complete canonical MatchContextV4 payload.");
            }

            if (string.IsNullOrWhiteSpace(contentHash))
            {
                throw new System.InvalidOperationException(
                    "Formal scenario requires its canonical content hash.");
            }

            return new FormalMatchScenarioDefinitionV4(
                scenarioId,
                formatVersion,
                ContractJson.DeserializeMatchContextV4(matchContextJson),
                firstServingSide,
                homeInitialRotationOffset,
                awayInitialRotationOffset,
                configurationIdentity,
                homeTactics,
                awayTactics,
                ai,
                contentHash);
        }

        [ContextMenu("Refresh Content Hash")]
        private void RefreshContentHash()
        {
            if (string.IsNullOrWhiteSpace(matchContextJson))
            {
                Debug.LogError(
                    "Formal scenario requires canonical MatchContextV4 JSON before hashing.",
                    this);
                return;
            }

            try
            {
                var definition = new FormalMatchScenarioDefinitionV4(
                    scenarioId,
                    formatVersion,
                    ContractJson.DeserializeMatchContextV4(matchContextJson),
                    firstServingSide,
                    homeInitialRotationOffset,
                    awayInitialRotationOffset,
                    configurationIdentity,
                    homeTactics,
                    awayTactics,
                    ai);
                contentHash = definition.ContentHash;
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "Formal scenario hash could not be refreshed: " +
                    exception.Message,
                    this);
            }
        }
    }
}

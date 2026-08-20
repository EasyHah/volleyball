using UnityEditor;
using UnityEngine;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor
{
    public static class TrainingScenarioAssetCreatorV2
    {
        private const string Directory =
            "Assets/Volleyball/Match/Runtime/Resources/TrainingScenariosV2";

        [MenuItem("Volleyball/Training Lab/Rebuild V2 Templates")]
        public static void CreateAll()
        {
            EnsureDirectory();
            foreach (var scenarioId in TrainingScenarioCatalogV2.ScenarioIds)
                Create(scenarioId);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureDirectory()
        {
            const string runtime = "Assets/Volleyball/Match/Runtime";
            const string resources = runtime + "/Resources";
            if (!AssetDatabase.IsValidFolder(resources))
                AssetDatabase.CreateFolder(runtime, "Resources");
            if (!AssetDatabase.IsValidFolder(Directory))
                AssetDatabase.CreateFolder(resources, "TrainingScenariosV2");
        }

        private static void Create(string scenarioId)
        {
            var definition = TrainingScenarioCatalogV2.Create(scenarioId);
            var path = Directory + "/" + scenarioId + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<TrainingScenarioPresetV2>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<TrainingScenarioPresetV2>();
                AssetDatabase.CreateAsset(preset, path);
            }

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("formatVersion").intValue =
                TrainingScenarioTemplateV2.CurrentFormatVersion;
            serialized.FindProperty("scenarioId").stringValue = definition.ScenarioId;
            serialized.FindProperty("displayName").stringValue = definition.DisplayName;
            serialized.FindProperty("source").stringValue = definition.Source;
            serialized.FindProperty("matchContextJson").stringValue =
                ContractJson.SerializeV5(definition.Context);
            serialized.FindProperty("contentHash").stringValue = definition.ContentHash;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
        }
    }
}

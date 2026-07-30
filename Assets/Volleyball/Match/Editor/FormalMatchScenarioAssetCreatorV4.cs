using UnityEditor;
using UnityEngine;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor
{
    public static class FormalMatchScenarioAssetCreatorV4
    {
        private const string Directory =
            "Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4";

        public static void CreateAll()
        {
            EnsureDirectory();
            Create("ReachableFloorDefense", "reachable-floor-defense");
            Create("LateFloorDefense", "late-floor-defense");
            Create("AttackSideBlockRebound", "attack-side-block-rebound");
            Create("BlockingSideBlockRebound", "blocking-side-block-rebound");
            Create("PostBlockMiss", "post-block-miss");
            Create("OverlappingDefenders", "overlapping-defenders");
            Create("ServeNetDeflection", "serve-net-deflection");
            Create("ServeNetDeflectionMiss", "serve-net-deflection-miss");
            Create("ServeNetRebound", "serve-net-rebound");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureDirectory()
        {
            const string resources = "Assets/Volleyball/Match/Tests/Resources";
            if (!AssetDatabase.IsValidFolder(resources))
            {
                AssetDatabase.CreateFolder("Assets/Volleyball/Match/Tests", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(Directory))
            {
                AssetDatabase.CreateFolder(resources, "FormalMatchScenariosV4");
            }
        }

        private static void Create(string assetName, string scenarioId)
        {
            var definition = FormalMatchScenarioCatalogV4.Create(scenarioId);
            var path = Directory + "/" + assetName + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<FormalMatchScenarioPresetV4>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<FormalMatchScenarioPresetV4>();
                AssetDatabase.CreateAsset(preset, path);
            }

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("scenarioId").stringValue = definition.ScenarioId;
            serialized.FindProperty("formatVersion").intValue = definition.FormatVersionValue;
            serialized.FindProperty("matchContextJson").stringValue = ContractJson.SerializeV4(definition.Context);
            serialized.FindProperty("firstServingSide").intValue = (int)definition.FirstServingSide;
            serialized.FindProperty("homeInitialRotationOffset").intValue = definition.HomeInitialRotationOffset;
            serialized.FindProperty("awayInitialRotationOffset").intValue = definition.AwayInitialRotationOffset;
            serialized.FindProperty("configurationIdentity").stringValue = definition.ConfigurationIdentity;
            CopyTactics(serialized.FindProperty("homeTactics"), definition.HomeTactics);
            CopyTactics(serialized.FindProperty("awayTactics"), definition.AwayTactics);
            CopyAi(serialized.FindProperty("ai"), definition.Ai);
            serialized.FindProperty("initialServeFlightSeconds").floatValue =
                definition.InitialServeFlightSeconds;
            serialized.FindProperty("initialServeArrivalVerticalSpeed").floatValue =
                definition.InitialServeArrivalVerticalSpeed;
            serialized.FindProperty("initialServeTargetDepthOffsetMeters").floatValue =
                definition.InitialServeTargetDepthOffsetMeters;
            serialized.FindProperty("contentHash").stringValue = definition.ContentHash;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
        }

        private static void CopyTactics(SerializedProperty property, FormalMatchTacticInputV4 value)
        {
            property.FindPropertyRelative("SetRoute").intValue = (int)value.SetRoute;
            property.FindPropertyRelative("SpikeRoute").intValue = (int)value.SpikeRoute;
            property.FindPropertyRelative("SetterX").floatValue = value.SetterX;
            property.FindPropertyRelative("SetterZ").floatValue = value.SetterZ;
            property.FindPropertyRelative("AttackerX").floatValue = value.AttackerX;
            property.FindPropertyRelative("AttackerZ").floatValue = value.AttackerZ;
            property.FindPropertyRelative("DefenderX").floatValue = value.DefenderX;
            property.FindPropertyRelative("DefenderZ").floatValue = value.DefenderZ;
            property.FindPropertyRelative("Blocker").intValue = (int)value.Blocker;
            property.FindPropertyRelative("BlockX").floatValue = value.BlockX;
            property.FindPropertyRelative("BlockZ").floatValue = value.BlockZ;
            property.FindPropertyRelative("CoverReceiver").intValue = (int)value.CoverReceiver;
            property.FindPropertyRelative("CoverX").floatValue = value.CoverX;
            property.FindPropertyRelative("CoverZ").floatValue = value.CoverZ;
            property.FindPropertyRelative("SetRhythm").intValue = (int)value.SetRhythm;
            property.FindPropertyRelative("AttackFlightSeconds").floatValue = value.AttackFlightSeconds;
        }

        private static void CopyAi(SerializedProperty property, FormalMatchAiInputV4 value)
        {
            property.FindPropertyRelative("RolePreference").floatValue = value.RolePreference;
            property.FindPropertyRelative("Reachability").floatValue = value.Reachability;
            property.FindPropertyRelative("ApproachDistance").floatValue = value.ApproachDistance;
            property.FindPropertyRelative("DirectionTolerance").floatValue = value.DirectionTolerance;
        }
    }
}

using UnityEditor;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor
{
    public static class TrainingScenarioAssetCreatorV1
    {
        private const string Directory =
            "Assets/Volleyball/Match/Runtime/Resources/TrainingScenariosV1";

        public static void CreateAll()
        {
            EnsureDirectory();
            foreach (var scenarioId in
                     TrainingScenarioCatalogV1.ScenarioIds)
            {
                Create(scenarioId);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureDirectory()
        {
            const string runtime =
                "Assets/Volleyball/Match/Runtime";
            const string resources =
                "Assets/Volleyball/Match/Runtime/Resources";
            if (!AssetDatabase.IsValidFolder(resources))
            {
                AssetDatabase.CreateFolder(runtime, "Resources");
            }

            if (!AssetDatabase.IsValidFolder(Directory))
            {
                AssetDatabase.CreateFolder(resources, "TrainingScenariosV1");
            }
        }

        private static void Create(string scenarioId)
        {
            var definition = TrainingScenarioCatalogV1.Create(scenarioId);
            var path = Directory + "/" + scenarioId + ".asset";
            var preset =
                AssetDatabase.LoadAssetAtPath<TrainingScenarioPresetV1>(path);
            if (preset == null)
            {
                preset =
                    ScriptableObject.CreateInstance<TrainingScenarioPresetV1>();
                AssetDatabase.CreateAsset(preset, path);
            }

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("scenarioId").stringValue =
                definition.ScenarioId;
            serialized.FindProperty("displayName").stringValue =
                definition.DisplayName;
            serialized.FindProperty("source").stringValue = definition.Source;
            serialized.FindProperty("formatVersion").intValue =
                definition.FormatVersionValue;
            serialized.FindProperty("matchContextJson").stringValue =
                ContractJson.SerializeV4(definition.Context);
            serialized.FindProperty("firstServingSide").intValue =
                (int)definition.FirstServingSide;
            serialized.FindProperty("homeInitialRotationOffset").intValue =
                definition.HomeInitialRotationOffset;
            serialized.FindProperty("awayInitialRotationOffset").intValue =
                definition.AwayInitialRotationOffset;
            CopyTactics(
                serialized.FindProperty("homeTactics"),
                ToInput(definition.HomeTactics));
            CopyTactics(
                serialized.FindProperty("awayTactics"),
                ToInput(definition.AwayTactics));
            CopyAi(
                serialized.FindProperty("ai"),
                ToInput(definition.Ai));
            CopyPlayers(
                serialized.FindProperty("players"),
                definition);
            serialized.FindProperty("ballPosition").vector3Value =
                ToUnity(definition.BallPosition);
            serialized.FindProperty("ballVelocity").vector3Value =
                ToUnity(definition.BallVelocity);
            serialized.FindProperty("startRecipe").intValue =
                (int)definition.StartState.Recipe;
            serialized.FindProperty("sourceTeam").intValue =
                (int)definition.StartState.SourceTeam;
            serialized.FindProperty("lastLegalActorId").stringValue =
                definition.StartState.LastLegalActor?.Value ?? string.Empty;
            serialized.FindProperty("accessLevel").intValue =
                (int)definition.AccessLevel;
            serialized.FindProperty("contentHash").stringValue =
                definition.ContentHash;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
        }

        private static void CopyPlayers(
            SerializedProperty property,
            TrainingScenarioV1 definition)
        {
            property.arraySize = definition.Players.Count;
            for (var index = 0;
                 index < definition.Players.Count;
                 index++)
            {
                var source = definition.Players[index];
                var target = property.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("playerId").stringValue =
                    source.PlayerId.Value;
                target.FindPropertyRelative("position").vector3Value =
                    ToUnity(source.Position);
                target.FindPropertyRelative("forward").vector3Value =
                    ToUnity(source.Forward);
                target.FindPropertyRelative("pose").intValue =
                    (int)source.Pose;
            }
        }

        private static void CopyTactics(
            SerializedProperty property,
            FormalMatchTacticInputV4 value)
        {
            property.FindPropertyRelative("SetRoute").intValue =
                (int)value.SetRoute;
            property.FindPropertyRelative("SpikeRoute").intValue =
                (int)value.SpikeRoute;
            property.FindPropertyRelative("SetterX").floatValue =
                value.SetterX;
            property.FindPropertyRelative("SetterZ").floatValue =
                value.SetterZ;
            property.FindPropertyRelative("AttackerX").floatValue =
                value.AttackerX;
            property.FindPropertyRelative("AttackerZ").floatValue =
                value.AttackerZ;
            property.FindPropertyRelative("DefenderX").floatValue =
                value.DefenderX;
            property.FindPropertyRelative("DefenderZ").floatValue =
                value.DefenderZ;
            property.FindPropertyRelative("Blocker").intValue =
                (int)value.Blocker;
            property.FindPropertyRelative("BlockX").floatValue =
                value.BlockX;
            property.FindPropertyRelative("BlockZ").floatValue =
                value.BlockZ;
            property.FindPropertyRelative("CoverReceiver").intValue =
                (int)value.CoverReceiver;
            property.FindPropertyRelative("CoverX").floatValue =
                value.CoverX;
            property.FindPropertyRelative("CoverZ").floatValue =
                value.CoverZ;
            property.FindPropertyRelative("SetRhythm").intValue =
                (int)value.SetRhythm;
            property.FindPropertyRelative("AttackFlightSeconds").floatValue =
                value.AttackFlightSeconds;
        }

        private static void CopyAi(
            SerializedProperty property,
            FormalMatchAiInputV4 value)
        {
            property.FindPropertyRelative("RolePreference").floatValue =
                value.RolePreference;
            property.FindPropertyRelative("Reachability").floatValue =
                value.Reachability;
            property.FindPropertyRelative("ApproachDistance").floatValue =
                value.ApproachDistance;
            property.FindPropertyRelative("DirectionTolerance").floatValue =
                value.DirectionTolerance;
        }

        private static FormalMatchTacticInputV4 ToInput(
            TrainingTeamTacticV1 value)
        {
            return new FormalMatchTacticInputV4
            {
                SetRoute = value.SetRoute,
                SpikeRoute = value.SpikeRoute,
                SetterX = value.SetterX,
                SetterZ = value.SetterZ,
                AttackerX = value.AttackerX,
                AttackerZ = value.AttackerZ,
                DefenderX = value.DefenderX,
                DefenderZ = value.DefenderZ,
                Blocker = value.Blocker,
                BlockX = value.BlockX,
                BlockZ = value.BlockZ,
                CoverReceiver = value.CoverReceiver,
                CoverX = value.CoverX,
                CoverZ = value.CoverZ,
                SetRhythm = value.SetRhythm,
                AttackFlightSeconds = value.AttackFlightSeconds
            };
        }

        private static FormalMatchAiInputV4 ToInput(
            TrainingAiConfigurationV1 value)
        {
            return new FormalMatchAiInputV4
            {
                RolePreference = value.RolePreference,
                Reachability = value.Reachability,
                ApproachDistance = value.ApproachDistance,
                DirectionTolerance = value.DirectionTolerance
            };
        }

        private static Vector3 ToUnity(
            Volleyball.Domain.Simulation.SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }
}

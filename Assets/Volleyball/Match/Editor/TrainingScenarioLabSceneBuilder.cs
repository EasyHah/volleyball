using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.Editor
{
    public static class TrainingScenarioLabSceneBuilder
    {
        public const string ScenePath =
            "Assets/Volleyball/Match/Scenes/" +
            "FormalTrainingScenarioLab.unity";
        public const string FormalScenePath =
            "Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity";
        public const string PanelSettingsPath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLabPanelSettings.asset";
        public const string VisualTreePath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLab.uxml";
        public const string StyleSheetPath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLab.uss";
        public const string ThemePath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLabRuntimeTheme.tss";

        [MenuItem("Volleyball/Match/Rebuild formal training lab scene")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(
                PanelSettingsPath,
                ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                VisualTreePath,
                ImportAssetOptions.ForceSynchronousImport);
            var panel = Required<PanelSettings>(PanelSettingsPath);
            var tree = Required<VisualTreeAsset>(VisualTreePath);
            Required<StyleSheet>(StyleSheetPath);
            Required<ThemeStyleSheet>(ThemePath);
            if (!EditorUtility.IsPersistent(panel) ||
                !EditorUtility.IsPersistent(tree))
            {
                throw new InvalidOperationException(
                    "Training lab UI assets must be persistent project assets.");
            }
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                panel,
                out var panelGuid,
                out long panelFileId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                tree,
                out var treeGuid,
                out long treeFileId);
            if (panelFileId == 0 || treeFileId == 0)
            {
                throw new InvalidOperationException(
                    "Training lab UI assets have invalid local IDs: panel=" +
                    panelGuid + ":" + panelFileId + ", tree=" +
                    treeGuid + ":" + treeFileId);
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("Formal Training Scenario Lab");
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            var serializedDocument = new SerializedObject(document);
            serializedDocument.FindProperty("m_PanelSettings")
                .objectReferenceValue = panel;
            serializedDocument.FindProperty("sourceAsset")
                .objectReferenceValue = tree;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(document);
            root.AddComponent<TrainingScenarioLabView>();
            if (document.panelSettings != panel ||
                document.visualTreeAsset != tree)
            {
                throw new InvalidOperationException(
                    "Training lab UI document references did not persist.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "Failed to save the formal training lab scene.");
            if (document.panelSettings == null ||
                document.visualTreeAsset == null)
            {
                RepairBatchModeDocumentReferences(
                    panelGuid,
                    panelFileId,
                    treeGuid,
                    treeFileId);
            }

            var scenes = EditorBuildSettings.scenes
                .Where(value => !string.Equals(
                    value.path,
                    ScenePath,
                    StringComparison.Ordinal))
                .ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            var reopened = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var savedDocument = reopened.GetRootGameObjects()
                .Single()
                .GetComponent<UIDocument>();
            if (savedDocument.panelSettings == null ||
                savedDocument.visualTreeAsset == null)
            {
                throw new InvalidOperationException(
                    "Saved training lab scene lost its UI asset references.");
            }
            Debug.Log("Formal training lab scene rebuilt: " + ScenePath);
        }

        private static void RepairBatchModeDocumentReferences(
            string panelGuid,
            long panelFileId,
            string treeGuid,
            long treeFileId)
        {
            // Unity 6000.3 batch mode can clear newly assigned UIDocument
            // references while saving a brand-new scene. Repair only the
            // single generated document, then reopen and validate below.
            var yaml = File.ReadAllText(ScenePath);
            const string emptyPanel =
                "  m_PanelSettings: {fileID: 0}";
            const string emptyTree =
                "  sourceAsset: {fileID: 0}";
            if (Count(yaml, emptyPanel) != 1 ||
                Count(yaml, emptyTree) != 1)
            {
                throw new InvalidOperationException(
                    "Generated scene does not contain exactly one empty " +
                    "UIDocument reference pair.");
            }

            yaml = yaml.Replace(
                    emptyPanel,
                    "  m_PanelSettings: {fileID: " + panelFileId +
                    ", guid: " + panelGuid + ", type: 2}")
                .Replace(
                    emptyTree,
                    "  sourceAsset: {fileID: " + treeFileId +
                    ", guid: " + treeGuid + ", type: 3}");
            File.WriteAllText(
                ScenePath,
                yaml,
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                ScenePath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static int Count(string value, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(
                       token,
                       index,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static T Required<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new FileNotFoundException(
                    "Required formal training lab asset is missing.",
                    path);
        }
    }
}

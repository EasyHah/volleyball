using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Volleyball.Career.Presentation;

namespace Volleyball.Bootstrap.Editor
{
    public static class CareerVerticalSliceSceneBuilder
    {
        public const string ScenePath =
            "Assets/Volleyball/Career/Scenes/CareerVerticalSlice.unity";
        private const string PanelSettingsPath =
            "Assets/Volleyball/Career/Runtime/Presentation/CareerPanelSettings.asset";
        private const string ShellPath =
            "Assets/Volleyball/Career/Runtime/Presentation/CareerShell.uxml";
        private const string InputPath =
            "Assets/Volleyball/Career/Runtime/Presentation/Input/CareerMenu.inputactions";
        private const string RuntimeInputPath =
            "Assets/Volleyball/Career/Runtime/Presentation/Input/CareerMenuRuntime.asset";
        private const string ThemePath =
            "Assets/Volleyball/Career/Runtime/Presentation/CareerDefaultRuntimeTheme.tss";
        private const string ContextPath =
            "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-context.json";
        private const string ResultPath =
            "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-result.json";

        [MenuItem("Volleyball/Career/Rebuild vertical slice scene")]
        public static void Build()
        {
            EnsureFolders();
            var panelSettings = LoadOrCreatePanelSettings();
            var shell = Required<VisualTreeAsset>(ShellPath);
            var actions = BuildRuntimeInputAsset(Required<InputActionAsset>(InputPath));
            var context = Required<TextAsset>(ContextPath);
            var result = Required<TextAsset>(ResultPath);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("Career Vertical Slice");
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = shell;
            root.AddComponent<CareerUiShell>();
            var bootstrap = root.AddComponent<CareerVerticalSliceBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("menuActions").objectReferenceValue = actions;
            serialized.FindProperty("canonicalContextFixture").objectReferenceValue = context;
            serialized.FindProperty("canonicalResultFixture").objectReferenceValue = result;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save the Career vertical slice scene.");
            }

            var existing = EditorBuildSettings.scenes
                .Where(item => !string.Equals(item.path, ScenePath, StringComparison.Ordinal))
                .ToArray();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            }.Concat(existing).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Career vertical slice scene rebuilt: " + ScenePath);
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearColor = true;
            settings.colorClearValue = new Color32(15, 24, 36, 255);
            settings.themeStyleSheet = Required<ThemeStyleSheet>(ThemePath);

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static InputActionAsset BuildRuntimeInputAsset(InputActionAsset source)
        {
            var runtime = AssetDatabase.LoadAssetAtPath<InputActionAsset>(RuntimeInputPath);
            if (runtime == null)
            {
                runtime = InputActionAsset.FromJson(source.ToJson());
                runtime.name = "CareerMenuRuntime";
                AssetDatabase.CreateAsset(runtime, RuntimeInputPath);
            }
            else
            {
                runtime.LoadFromJson(source.ToJson());
                runtime.name = "CareerMenuRuntime";
                EditorUtility.SetDirty(runtime);
            }

            AssetDatabase.SaveAssetIfDirty(runtime);
            AssetDatabase.ImportAsset(RuntimeInputPath, ImportAssetOptions.ForceSynchronousImport);
            return Required<InputActionAsset>(RuntimeInputPath);
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new FileNotFoundException("Required Career scene asset is missing.", path);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Volleyball/Career", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}

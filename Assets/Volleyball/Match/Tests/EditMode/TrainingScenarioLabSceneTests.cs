using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingScenarioLabSceneTests
    {
        private const string ScenePath =
            "Assets/Volleyball/Match/Scenes/" +
            "FormalTrainingScenarioLab.unity";
        private const string PanelPath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLabPanelSettings.asset";
        private const string TreePath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLab.uxml";
        private const string StylePath =
            "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/" +
            "TrainingScenarioLab.uss";

        [Test]
        public void Scene_HasOneBoundMatchOwnedDocumentAndView()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            Assert.That(roots, Has.Length.EqualTo(1));
            var document = roots[0].GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(
                roots[0].GetComponent<TrainingScenarioLabView>(),
                Is.Not.Null);
            Assert.That(document.panelSettings, Is.EqualTo(
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath)));
            Assert.That(document.visualTreeAsset, Is.EqualTo(
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TreePath)));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath),
                Is.Not.Null);
        }

        [Test]
        public void VisualTree_UsesScenarioHubAndCanvasFirstWorkbench()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                TreePath);
            var root = tree.CloneTree();

            Assert.That(root.Q("scenario-hub"), Is.Not.Null);
            Assert.That(root.Q("continue-scenarios"), Is.Not.Null);
            Assert.That(root.Q("standard-scenarios"), Is.Not.Null);
            Assert.That(root.Q<Button>("hub-new-from-standard-button"),
                Is.Not.Null);
            Assert.That(root.Q("workbench-shell"), Is.Not.Null);
            Assert.That(root.Q("stage-rail"), Is.Not.Null);
            Assert.That(root.Q("workbench-content"), Is.Not.Null);
            Assert.That(root.Q("contextual-inspector"), Is.Not.Null);
            Assert.That(root.Q("context-scroll"), Is.Not.Null);
            Assert.That(root.Q("bottom-action-bar"), Is.Not.Null);
            Assert.That(root.Q<Button>("save-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("return-to-hub-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("more-button"), Is.Not.Null);
            Assert.That(root.Q("advanced-settings"), Is.Not.Null);
            Assert.That(root.Q<TextField>("display-name"), Is.Not.Null);
            Assert.That(root.Q<Label>("match-seed-label"), Is.Not.Null);
            Assert.That(root.Q("context-rotation"), Is.Not.Null);
            Assert.That(root.Q("rotation-board"), Is.Not.Null);
            Assert.That(root.Q("rotation-home-grid"), Is.Not.Null);
            Assert.That(root.Q("rotation-away-grid"), Is.Not.Null);
            Assert.That(root.Q("context-positioning"), Is.Not.Null);
            Assert.That(root.Q("context-serve-ball"), Is.Not.Null);
            Assert.That(root.Q("serve-view-selector"), Is.Not.Null);
            Assert.That(root.Q<Button>("serve-top-view-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("serve-side-view-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("serve-3d-preview-button"), Is.Not.Null);
            Assert.That(root.Q("serve-side-board"), Is.Not.Null);
            Assert.That(root.Q("preview-3d-modal"), Is.Not.Null);
            Assert.That(root.Q("preview-3d-viewport"), Is.Not.Null);
            Assert.That(root.Q<Button>("preview-bookmark-save-button"),
                Is.Not.Null);
            Assert.That(root.Q("preview-bookmark-list"), Is.Not.Null);
            Assert.That(root.Q("context-validation"), Is.Not.Null);
            Assert.That(root.Q("context-running"), Is.Not.Null);
            Assert.That(root.Q("left-panel"), Is.Null);
            Assert.That(root.Q("editor-controls"), Is.Null);
            Assert.That(root.Q<Button>("validate-button"), Is.Null);
            Assert.That(root.Q<Button>("context-validate-button"), Is.Null);
            Assert.That(root.Q("timeline-list"), Is.Not.Null);
            Assert.That(root.Q<Button>("run-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("pause-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("step-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("rerun-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("export-button"), Is.Null);
            Assert.That(root.Q<Button>("review-setter-button"), Is.Null);
        }

        [Test]
        public void BuildList_AppendsLabWithoutReorderingExistingScenes()
        {
            var paths = EditorBuildSettings.scenes
                .Select(value => value.path)
                .ToArray();
            Assert.That(paths.Last(), Is.EqualTo(ScenePath));
            Assert.That(
                paths.Take(5),
                Is.EqualTo(new[]
                {
                    "Assets/Volleyball/Career/Scenes/CareerVerticalSlice.unity",
                    "Assets/Volleyball/Match/Scenes/AiRallyPrototype.unity",
                    "Assets/Volleyball/Match/Scenes/PhysicsContactTraining.unity",
                    "Assets/Volleyball/Match/Scenes/Physical3v3Rally.unity",
                    "Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity"
                }));
        }

        [Test]
        public void WindowsBuildEntry_IsDevelopmentIl2CppWithLabFirst()
        {
            var editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(value =>
                    value.GetName().Name == "Volleyball.Match.Editor");
            var type = editorAssembly.GetType(
                "Volleyball.Editor." +
                "TrainingScenarioLabWindowsDevelopmentBuild",
                true);
            var method = type.GetMethod(
                "CreateOptions",
                BindingFlags.Public | BindingFlags.Static);
            var options = (BuildPlayerOptions)method.Invoke(
                null,
                new object[] { "Builds/Windows/test.exe" });

            Assert.That(
                options.target,
                Is.EqualTo(BuildTarget.StandaloneWindows64));
            Assert.That(
                options.options.HasFlag(BuildOptions.Development),
                Is.True);
            Assert.That(
                options.options.HasFlag(BuildOptions.AllowDebugging),
                Is.True);
            Assert.That(options.scenes[0], Is.EqualTo(ScenePath));
            Assert.That(
                options.scenes[1],
                Is.EqualTo(
                    "Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity"));
            var backend = (ScriptingImplementation)type
                .GetField(
                    "ScriptingBackend",
                    BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            Assert.That(
                backend,
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(
                File.Exists(ScenePath),
                Is.True);
        }

        [Test]
        public void Exporter_RemainsOutsidePlayerRuntimeAssembly()
        {
            var exporter = AppDomain.CurrentDomain.GetAssemblies()
                .Single(value =>
                    value.GetName().Name == "Volleyball.Match.Editor")
                .GetType(
                    "Volleyball.Editor." +
                    "TrainingDecisionSnapshotExporterV1",
                    true);
            Assert.That(
                exporter.Assembly,
                Is.Not.EqualTo(typeof(TrainingScenarioLabView).Assembly));
        }

        [Test]
        public void SetterReviewWindow_RemainsInEditorAssembly()
        {
            var reviewWindow = AppDomain.CurrentDomain.GetAssemblies()
                .Single(value =>
                    value.GetName().Name == "Volleyball.Match.AI.Editor")
                .GetType(
                    "Volleyball.Editor.AI.SetterTeacher." +
                    "SetterTeacherReviewWindowV1",
                    true);
            Assert.That(reviewWindow.Assembly, Is.Not.EqualTo(
                typeof(TrainingScenarioLabView).Assembly));
            Assert.That(reviewWindow.GetMethod("OpenForSnapshot"), Is.Not.Null);
        }
    }
}

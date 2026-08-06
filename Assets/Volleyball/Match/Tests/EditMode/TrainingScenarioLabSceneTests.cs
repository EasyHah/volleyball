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
        public void VisualTree_ContainsAllFourCommandAreasAndLifecycleActions()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                TreePath);
            var root = tree.CloneTree();

            Assert.That(root.Q("scenario-list"), Is.Not.Null);
            Assert.That(root.Q("world-viewport"), Is.Not.Null);
            Assert.That(root.Q("editor-controls"), Is.Not.Null);
            Assert.That(root.Q("timeline-list"), Is.Not.Null);
            Assert.That(root.Q<Button>("run-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("pause-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("step-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("rerun-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("export-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("review-setter-button"), Is.Not.Null);
            Assert.That(
                root.Q<Button>("review-setter-button").tooltip,
                Does.Contain("Editor"));
        }

        [Test]
        public void VisualTree_ExposesTacticalBoardAndPrecisionEditingViews()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                TreePath);
            var root = tree.CloneTree();

            Assert.That(root.Q("tactical-board"), Is.Not.Null);
            Assert.That(root.Q("tactical-token-layer"), Is.Not.Null);
            Assert.That(root.Q("position-fault-layer"), Is.Not.Null);
            Assert.That(root.Q<Button>("open-precision-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("open-observation-button"), Is.Not.Null);
            Assert.That(root.Q("free-observation"), Is.Not.Null);
            Assert.That(root.Q("observation-surface"), Is.Not.Null);
            Assert.That(root.Q<Button>("return-from-observation-button"),
                Is.Not.Null);
            Assert.That(root.Q("precision-xy-pane"), Is.Not.Null);
            Assert.That(root.Q("precision-zy-pane"), Is.Not.Null);
            Assert.That(root.Q("precision-xz-pane"), Is.Not.Null);
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

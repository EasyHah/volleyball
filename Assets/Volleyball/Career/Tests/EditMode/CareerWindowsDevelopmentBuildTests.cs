using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Volleyball.Bootstrap;
using Volleyball.Bootstrap.Editor;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWindowsDevelopmentBuildTests
    {
        [Test]
        public void CareerSceneSerializesRuntimeMenuActions()
        {
            var scene = EditorSceneManager.OpenScene(
                CareerVerticalSliceSceneBuilder.ScenePath,
                OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects()[0]
                    .GetComponentInChildren<CareerVerticalSliceBootstrap>(true);
                Assert.That(bootstrap, Is.Not.Null);
                var serialized = new SerializedObject(bootstrap);
                Assert.That(
                    serialized.FindProperty("menuActions").objectReferenceValue,
                    Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OptionsLockCareerSceneWindows64AndDevelopmentDebugging()
        {
            var options = CareerWindowsDevelopmentBuild.CreateOptions(
                CareerWindowsDevelopmentBuild.OutputPath);

            Assert.That(options.target, Is.EqualTo(BuildTarget.StandaloneWindows64));
            Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
            Assert.That(options.locationPathName,
                Is.EqualTo(CareerWindowsDevelopmentBuild.OutputPath));
            Assert.That(options.scenes, Is.EqualTo(new[]
            {
                CareerVerticalSliceSceneBuilder.ScenePath,
                CareerFormalSixVsSixMatchRunnerV4.FormalScenePath
            }));
            Assert.That(options.options.HasFlag(BuildOptions.Development), Is.True);
            Assert.That(options.options.HasFlag(BuildOptions.AllowDebugging), Is.True);
            Assert.That(CareerWindowsDevelopmentBuild.ScriptingBackend,
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(CareerWindowsDevelopmentBuild.WindowsX64Architecture,
                Is.EqualTo(1));
            Assert.That(CareerWindowsDevelopmentBuild.ManifestPath,
                Is.EqualTo("Builds/Windows/build-manifest.json"));

            var resolvedOutput = CareerWindowsDevelopmentBuild.ResolveProjectPath(
                CareerWindowsDevelopmentBuild.OutputPath);
            var projectRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                ".."));
            Assert.That(resolvedOutput, Is.EqualTo(Path.GetFullPath(Path.Combine(
                projectRoot,
                CareerWindowsDevelopmentBuild.OutputPath))));
        }
    }
}

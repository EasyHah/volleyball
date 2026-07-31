using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Volleyball.Editor
{
    public static class TrainingScenarioLabWindowsDevelopmentBuild
    {
        public const string OutputPath =
            "Builds/Windows/VolleyballTrainingLab.exe";
        public const string ManifestPath =
            "Builds/Windows/training-lab-build-manifest.json";
        public const int WindowsX64Architecture = 1;
        public const ScriptingImplementation ScriptingBackend =
            ScriptingImplementation.IL2CPP;

        [Serializable]
        private sealed class BuildManifest
        {
            public int schemaVersion = 1;
            public string generatedAtUtc;
            public string unityVersion;
            public string target;
            public string architecture;
            public string scriptingBackend;
            public bool development;
            public bool allowDebugging;
            public string executable;
            public string[] scenes;
            public ulong totalBytes;
        }

        [MenuItem("Volleyball/Match/Build training lab Windows x64 Development")]
        public static void Build()
        {
            var output = ResolveProjectPath(OutputPath);
            var manifest = ResolveProjectPath(ManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ??
                                      throw new InvalidOperationException(
                                          "Build output has no directory."));
            if (File.Exists(manifest)) File.Delete(manifest);

            var previousBackend = PlayerSettings.GetScriptingBackend(
                NamedBuildTarget.Standalone);
            var previousArchitecture = PlayerSettings.GetArchitecture(
                NamedBuildTarget.Standalone);
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingBackend(
                    NamedBuildTarget.Standalone,
                    ScriptingBackend);
                PlayerSettings.SetArchitecture(
                    NamedBuildTarget.Standalone,
                    WindowsX64Architecture);
                report = BuildPipeline.BuildPlayer(CreateOptions(output));
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(
                    NamedBuildTarget.Standalone,
                    previousBackend);
                PlayerSettings.SetArchitecture(
                    NamedBuildTarget.Standalone,
                    previousArchitecture);
                AssetDatabase.SaveAssets();
            }

            if (report == null ||
                report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Training lab Windows Development Build failed: " +
                    (report == null
                        ? "missing-report"
                        : report.summary.result.ToString()));
            }

            WriteManifest(report, manifest);
            Debug.Log(
                "Training lab Windows Development Build succeeded: " +
                OutputPath);
        }

        public static BuildPlayerOptions CreateOptions(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException(
                    "A build output path is required.",
                    nameof(outputPath));
            return new BuildPlayerOptions
            {
                scenes = new[]
                {
                    TrainingScenarioLabSceneBuilder.ScenePath,
                    TrainingScenarioLabSceneBuilder.FormalScenePath
                },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options =
                    BuildOptions.Development | BuildOptions.AllowDebugging
            };
        }

        public static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException(
                    "A project-relative path is required.",
                    nameof(relativePath));
            }

            var root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            var prefix = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var resolved = Path.GetFullPath(
                Path.Combine(root, relativePath));
            if (!resolved.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The build path must stay inside the Unity project.",
                    nameof(relativePath));
            }

            return resolved;
        }

        private static void WriteManifest(
            BuildReport report,
            string manifestPath)
        {
            var manifest = new BuildManifest
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                target = BuildTarget.StandaloneWindows64.ToString(),
                architecture = "x86_64",
                scriptingBackend = ScriptingBackend.ToString(),
                development = true,
                allowDebugging = true,
                executable = OutputPath.Replace('\\', '/'),
                scenes = CreateOptions(OutputPath).scenes,
                totalBytes = report.summary.totalSize
            };
            File.WriteAllText(
                manifestPath,
                JsonUtility.ToJson(manifest, true) + "\n",
                new UTF8Encoding(false));
        }
    }
}

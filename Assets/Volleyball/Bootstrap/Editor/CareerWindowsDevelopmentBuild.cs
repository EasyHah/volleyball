using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Volleyball.Bootstrap.Editor
{
    public static class CareerWindowsDevelopmentBuild
    {
        public const string OutputPath = "Builds/Windows/VolleyballCareer.exe";
        public const string ManifestPath = "Builds/Windows/build-manifest.json";
        public const int WindowsX64Architecture = 1;
        public const ScriptingImplementation ScriptingBackend =
            ScriptingImplementation.IL2CPP;

        [Serializable]
        private sealed class BuildManifest
        {
            public int schemaVersion = 1;
            public string generatedAtUtc;
            public string unityVersion;
            public string productVersion;
            public string target;
            public string architecture;
            public string scriptingBackend;
            public bool development;
            public bool allowDebugging;
            public string executable;
            public ulong totalBytes;
        }

        [MenuItem("Volleyball/Career/Build Windows x64 Development")]
        public static void Build()
        {
            var absoluteOutput = ResolveProjectPath(OutputPath);
            var absoluteManifest = ResolveProjectPath(ManifestPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutput);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("Windows build output has no directory.");
            }

            Directory.CreateDirectory(outputDirectory);
            InvalidateManifest(absoluteManifest);

            var previousBackend = PlayerSettings.GetScriptingBackend(
                NamedBuildTarget.Standalone);
            var previousArchitecture = PlayerSettings.GetArchitecture(
                NamedBuildTarget.Standalone);
            BuildReport report;
            try
            {
                ConfigureWindowsPlayer();
                report = BuildPipeline.BuildPlayer(CreateOptions(absoluteOutput));
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

            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                var result = report == null
                    ? "missing-report"
                    : report.summary.result.ToString();
                throw new InvalidOperationException(
                    "Career Windows Development Build failed: " + result);
            }

            WriteManifest(report, absoluteManifest);
            Debug.Log(
                "Career Windows Development Build succeeded: " + OutputPath +
                " (" + report.summary.totalSize + " bytes)");
        }

        public static BuildPlayerOptions CreateOptions(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("A build output path is required.", nameof(outputPath));
            }

            return new BuildPlayerOptions
            {
                scenes = new[]
                {
                    CareerVerticalSliceSceneBuilder.ScenePath,
                    FormalSixVsSixSceneRunnerV4.ScenePath
                },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };
        }

        public static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException(
                    "A project-relative path is required.",
                    nameof(relativePath));
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var rootPrefix = projectRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var resolved = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The build path must stay inside the Unity project.",
                    nameof(relativePath));
            }

            return resolved;
        }

        private static void ConfigureWindowsPlayer()
        {
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Standalone,
                ScriptingBackend);
            PlayerSettings.SetArchitecture(
                NamedBuildTarget.Standalone,
                WindowsX64Architecture);
        }

        private static void InvalidateManifest(string manifestPath)
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }

        private static void WriteManifest(BuildReport report, string manifestPath)
        {
            var manifest = new BuildManifest
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                productVersion = Application.version,
                target = BuildTarget.StandaloneWindows64.ToString(),
                architecture = "x86_64",
                scriptingBackend = ScriptingBackend.ToString(),
                development = true,
                allowDebugging = true,
                executable = OutputPath.Replace('\\', '/'),
                totalBytes = report.summary.totalSize
            };
            var json = JsonUtility.ToJson(manifest, true) + "\n";
            var temporary = manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                File.Move(temporary, manifestPath);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Presentation;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;

namespace Volleyball.PlayModeTests
{
    public sealed class AttackChainCalibrationPlayModeTests
    {
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Formal6v6_InSystemAttackChainMeetsInitialThresholds()
        {
            AttackChainCalibrationReport report = null;
            yield return RunInSystemFirstPasses(
                "FormalIndoor6v6",
                7351,
                100,
                value => report = value);

            Assert.That(report.AttackableSetRate, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(report.AGradeNoContactErrorRate, Is.LessThan(0.02f), report.NoContactDiagnostic);
            Assert.That(report.NormalSideSets, Is.Zero);
            Assert.That(report.NonAChainsWithoutReason, Is.Zero);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ThreeVsThree_InSystemAttackChainMeetsInitialThresholds()
        {
            AttackChainCalibrationReport report = null;
            yield return RunInSystemFirstPasses(
                "Physical3v3Rally",
                7351,
                100,
                value => report = value);

            Assert.That(report.AttackableSetRate, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(report.AGradeNoContactErrorRate, Is.LessThan(0.02f), report.NoContactDiagnostic);
            Assert.That(report.NormalSideSets, Is.Zero);
            Assert.That(report.NonAChainsWithoutReason, Is.Zero);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Formal6v6_TwentySymmetricSetsKeepBlueWinsWithinNineToEleven()
        {
            var blueWins = 0;
            var originalTimeScale = Time.timeScale;
            var originalLogging = Debug.unityLogger.logEnabled;
            try
            {
                Debug.unityLogger.logEnabled = false;
                for (var setIndex = 0; setIndex < 20; setIndex++)
                {
                    PhysicalMatchRallyDirector director = null;
                    yield return CreateCalibrationDirector(
                        "FormalIndoor6v6",
                        targetScore: 1,
                        firstServer: setIndex % 2 == 0 ? TeamSide.Home : TeamSide.Away,
                        value => director = value);
                    var timeout = Time.realtimeSinceStartup + 30f;
                    while (director.ResultV2 == null && Time.realtimeSinceStartup < timeout)
                    {
                        Time.timeScale = 8f;
                        yield return null;
                    }

                    Assert.That(director.ResultV2, Is.Not.Null, $"Calibration set {setIndex + 1} timed out.");
                    if (director.ResultV2.HomeScore > director.ResultV2.AwayScore)
                    {
                        blueWins++;
                    }
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = originalLogging;
                Time.timeScale = originalTimeScale;
            }

            Assert.That(blueWins, Is.InRange(9, 11));
        }

        private static IEnumerator RunInSystemFirstPasses(
            string sceneName,
            int seed,
            int targetInSystemSets,
            Action<AttackChainCalibrationReport> completed)
        {
            PhysicalMatchRallyDirector director = null;
            yield return CreateCalibrationDirector(
                sceneName,
                targetScore: 1000,
                firstServer: TeamSide.Home,
                value => director = value);
            Assert.That(director.MatchContextV2.Seed, Is.EqualTo(seed));

            var originalTimeScale = Time.timeScale;
            var originalLogging = Debug.unityLogger.logEnabled;
            var observedSets = 0;
            var nonAWithoutReason = 0;
            var timeout = Time.realtimeSinceStartup + 420f;
            try
            {
                Debug.unityLogger.logEnabled = false;
                while (director.InSystemSetterSets < targetInSystemSets &&
                       director.ResultV2 == null &&
                       Time.realtimeSinceStartup < timeout)
                {
                    Time.timeScale = 8f;
                    if (director.TotalSets > observedSets)
                    {
                        observedSets = director.TotalSets;
                        var quality = director.LastSetQualityAssessment;
                        if (quality.HasValue && quality.Value.Grade != SetQualityGrade.A &&
                            string.IsNullOrWhiteSpace(quality.Value.Reason))
                        {
                            nonAWithoutReason++;
                        }
                    }

                    yield return null;
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = originalLogging;
                Time.timeScale = originalTimeScale;
            }

            completed(new AttackChainCalibrationReport(
                director.InSystemAttackableSetRate,
                director.AGradeNoContactErrorRate,
                director.NormalSideSets,
                nonAWithoutReason,
                $"produced={director.TotalSets}; inSystem={director.InSystemSetterSets}; " +
                director.LastAGradeNoContactDiagnostic));
            Assert.That(director.InSystemSetterSets, Is.GreaterThanOrEqualTo(targetInSystemSets),
                $"{sceneName} produced only {director.InSystemSetterSets}/{targetInSystemSets} " +
                $"in-system set contacts ({director.TotalSets} total) before timeout.");
        }

        private static IEnumerator CreateCalibrationDirector(
            string sceneName,
            int targetScore,
            TeamSide firstServer,
            Action<PhysicalMatchRallyDirector> completed)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            var previous = UnityEngine.Object.FindFirstObjectByType<PhysicalMatchRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var score = UnityEngine.Object.FindFirstObjectByType<ScoreDisplay>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
            Assert.That(previous, Is.Not.Null);
            var context = previous.MatchContextV2;
            var rosterSize = previous.RosterSize;
            var host = previous.gameObject;
            UnityEngine.Object.Destroy(previous);
            yield return null;
            foreach (var player in players)
            {
                player.CancelScheduledContact();
            }

            var baseConfiguration = rosterSize == 6
                ? PhysicalMatchConfiguration.FormalIndoorSixVsSix
                : PhysicalMatchConfiguration.ThreeVsThree;
            var configuration = PhysicalMatchConfiguration.CreateCalibration(
                baseConfiguration,
                targetScore,
                1);
            PhysicalMatchRallyDirector director = rosterSize == 6
                ? host.AddComponent<FormalSixVsSixRallyDirector>()
                : host.AddComponent<ThreeVsThreeRallyDirector>();
            director.InitializeV2(
                ball,
                players,
                context,
                score,
                configuration: configuration,
                firstServingSide: firstServer);
            completed(director);
        }

        private sealed class AttackChainCalibrationReport
        {
            public AttackChainCalibrationReport(
                float attackableSetRate,
                float aGradeNoContactErrorRate,
                int normalSideSets,
                int nonAChainsWithoutReason,
                string noContactDiagnostic)
            {
                AttackableSetRate = attackableSetRate;
                AGradeNoContactErrorRate = aGradeNoContactErrorRate;
                NormalSideSets = normalSideSets;
                NonAChainsWithoutReason = nonAChainsWithoutReason;
                NoContactDiagnostic = noContactDiagnostic;
            }

            public float AttackableSetRate { get; }
            public float AGradeNoContactErrorRate { get; }
            public int NormalSideSets { get; }
            public int NonAChainsWithoutReason { get; }
            public string NoContactDiagnostic { get; }
        }
    }
}

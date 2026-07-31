using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.PlayModeTests
{
    public sealed class TrainingTimelinePlayModeTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Recorder_IsReadOnlyAndCapturesVisibleEvidence()
        {
            RunResult withoutRecorder = null;
            yield return Run(false, value => withoutRecorder = value);

            RunResult withRecorder = null;
            yield return Run(true, value => withRecorder = value);

            CollectionAssert.AreEqual(
                withoutRecorder.EventSignatures,
                withRecorder.EventSignatures,
                "Adding the recorder must not change formal event count or order.");
            Assert.That(withRecorder.Winner, Is.EqualTo(withoutRecorder.Winner));
            Assert.That(withRecorder.Reason, Is.EqualTo(withoutRecorder.Reason));

            var evidence = withRecorder.Evidence;
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.IsResolved, Is.True);
            Assert.That(evidence.WinningTeam, Is.EqualTo(withRecorder.Winner));
            Assert.That(evidence.ResolutionReason, Is.EqualTo(withRecorder.Reason));
            Assert.That(evidence.Timeline, Is.Not.Empty);
            Assert.That(evidence.Decisions, Is.Not.Empty);
            Assert.That(evidence.SetterTargets, Is.Not.Empty);
            Assert.That(evidence.SetterTargets.All(snapshot =>
                snapshot.Candidates.Any(candidate =>
                    candidate.IsFeasible &&
                    candidate.PlayerId.Equals(snapshot.SelectedAttacker))),
                Is.True);
            Assert.That(
                evidence.Timeline.Select(value => value.Sequence),
                Is.EqualTo(Enumerable.Range(0, evidence.Timeline.Count)));
            Assert.That(
                evidence.Timeline.Any(value =>
                    value.Kind == TrainingTimelineEventKindV1.NetContact),
                Is.True);
            Assert.That(
                evidence.Timeline.Any(value =>
                    value.Kind == TrainingTimelineEventKindV1.RallyResolved),
                Is.True);
            Assert.That(
                evidence.Timeline
                    .Where(value =>
                        value.Kind == TrainingTimelineEventKindV1.Decision)
                    .Select(value => value.Decision.SnapshotHash),
                Is.EqualTo(evidence.Decisions.Select(value =>
                    value.SnapshotHash)));
            Assert.That(
                evidence.Decisions.All(value =>
                    value.ScenarioId == evidence.ScenarioId &&
                    value.ScenarioHash == evidence.ScenarioHash),
                Is.True);
        }

        private static IEnumerator Run(
            bool recordEvidence,
            Action<RunResult> completed)
        {
            var id = TrainingScenarioCatalogV1.SecondTouchNetOwnSide;
            var preset = Resources.Load<TrainingScenarioPresetV1>(
                "TrainingScenariosV1/" + id);
            Assert.That(preset, Is.Not.Null);
            var draft = preset.CreateDraft();
            draft.ScenarioId = "training-v1/timeline-observer";
            draft.DisplayName = "Timeline Observer";
            draft.StartRecipe = RallyStartRecipeV3.ServeFlight;
            draft.SourceTeam = Volleyball.Shared.Contracts.TeamSide.Away;
            draft.FirstServingSide = Volleyball.Shared.Contracts.TeamSide.Away;
            draft.LastLegalActor = null;
            draft.BallPosition = new Volleyball.Domain.Simulation.SimVector3(
                0f, 2.1f, 5f);
            draft.BallVelocity = new Volleyball.Domain.Simulation.SimVector3(
                0f, 2f, -12f);
            var scenario = TrainingScenarioValidatorV1.Build(draft);
            var host = new GameObject(
                recordEvidence
                    ? "timeline-observer-on"
                    : "timeline-observer-off");
            var director =
                FormalSixVsSixRallyBootstrap.InitializeTrainingScenario(
                    host.transform,
                    scenario);
            var ball = host.GetComponentInChildren<SimulatedBall>();
            var players =
                host.GetComponentsInChildren<PrototypePlayerAgent>();
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));

            var signatures = new List<string>();
            ReplayRallyResolvedEvent resolution = null;
            director.ReplayDecisionPlanned += value =>
                signatures.Add(
                    "decision:" + value.Stage + ":" +
                    value.SelectedPlayer.Team + ":" +
                    value.SelectedPlayer.Role + ":" +
                    value.SelectedPlayer.RosterSlot);
            director.ReplayContactAccepted += value =>
                signatures.Add(
                    "contact:" + value.Team + ":" +
                    value.PlayerId?.Value + ":" + value.Action);
            director.ReceiveOrganizationAuthorityCommitted += value =>
                signatures.Add(
                    "gate-h:" + value.SourceSequence + ":" + value.Kind);
            director.GateISetIntentCommitted += value =>
                signatures.Add(
                    "gate-i-set:" + value.SourceSequence + ":" +
                    value.Intent.Organizer.Value);
            director.AttackDefenseAuthorityCommitted += value =>
                signatures.Add(
                    "gate-i-contact:" + value.SourceSequence + ":" +
                    value.Kind);
            director.ReplayNetCrossed += value =>
                signatures.Add(
                    "crossing:" + value.Team + ":" +
                    value.SimulationTimeSeconds.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
            director.ReplayDefenseAttemptRecorded += value =>
                signatures.Add(
                    "defense:" + value.Kind + ":" +
                    value.AttemptIdentity + ":" + value.Reason);
            director.ReplayRallyResolved += value =>
            {
                resolution = value;
                signatures.Add(
                    "resolved:" + value.Team + ":" + value.Reason);
            };
            ball.EnvironmentContact += value =>
                signatures.Add(
                    "environment:" + value.Kind + ":" +
                    ball.SimulationTime.ToString(
                        "R",
                        CultureInfo.InvariantCulture));

            TrainingTimelineRecorderV1 recorder = null;
            if (recordEvidence)
            {
                recorder = new TrainingTimelineRecorderV1(
                    director,
                    ball,
                    players);
            }

            var deadline = Time.realtimeSinceStartup + 12f;
            while (!director.TrainingSingleRallyCompleted &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(director.TrainingSingleRallyCompleted, Is.True);
            Assert.That(resolution, Is.Not.Null);
            var evidence = recorder?.Capture();
            recorder?.Dispose();
            var result = new RunResult(
                signatures.ToArray(),
                resolution.Team,
                resolution.Reason,
                evidence);
            UnityEngine.Object.Destroy(host);
            yield return null;
            completed(result);
        }

        private sealed class RunResult
        {
            public RunResult(
                string[] eventSignatures,
                TeamId winner,
                string reason,
                TrainingRunEvidenceV1 evidence)
            {
                EventSignatures = eventSignatures;
                Winner = winner;
                Reason = reason;
                Evidence = evidence;
            }

            public string[] EventSignatures { get; }
            public TeamId Winner { get; }
            public string Reason { get; }
            public TrainingRunEvidenceV1 Evidence { get; }
        }
    }
}

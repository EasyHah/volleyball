using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using Volleyball.Domain;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixReplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator Attach_BeforeInitializeV4RequiresNativeContext()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            var host = new GameObject("UninitializedFormalDirector");
            try
            {
                var director = host.AddComponent<FormalSixVsSixRallyDirector>();

                Assert.That(director.MatchContext, Is.Null);
                Assert.That(
                    () => MatchReplayRecorder.Attach(director, ball, players),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("initialized"));
                Assert.That(host.GetComponent<MatchReplayRecorder>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator Attach_AfterInitializeV4CreatesFormalOnlyRecorder()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            var recorder = MatchReplayRecorder.Attach(director, ball, players);

            Assert.That(recorder, Is.Not.Null);
            Assert.That(recorder.IsComplete, Is.False);
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<MatchReplayRecorder>(),
                Is.SameAs(recorder));
        }

        [UnityTest]
        public IEnumerator Capture_FirstFormalRallyProducesStrictNativeV4Replay()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalIndoor6v6",
                LoadSceneMode.Single);
            var director =
                UnityEngine.Object.FindFirstObjectByType<
                    FormalSixVsSixRallyDirector>();
            var ball =
                UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players =
                UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                    FindObjectsSortMode.None);
            var recorder = MatchReplayRecorder.Attach(
                director,
                ball,
                players);
            recorder.StartCapture();

            var timeout = Time.realtimeSinceStartup + 90f;
            while (!recorder.IsComplete &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                recorder.IsComplete,
                Is.True,
                "The first formal rally did not complete.");
            var replay = recorder.Complete();
            var json = ContractJson.SerializeV4(replay);
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(
                restored.FormatVersion,
                Is.EqualTo(ContractVersions.ReplayV4));
            Assert.That(
                restored.Context.ContractVersion,
                Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(
                restored.Context.RulesVersion,
                Is.EqualTo(RulesVersions.FullRallyV3));
            AssertV4Identity(restored.Context.ContextHash, "formal context");
            AssertV4Identity(restored.ReplayHash, "formal replay");
            Assert.That(restored.Events, Is.Not.Empty);
            foreach (var replayEvent in restored.Events)
            {
                Assert.That(replayEvent.TestedEnvelope, Is.Not.Null);
                Assert.That(
                    replayEvent.ExecutableEnvelope,
                    Is.Not.Null);
                Assert.That(replayEvent.Trajectory, Is.Not.Null);
                Assert.That(replayEvent.AbilityConsumptions, Is.Not.Empty);
                Assert.That(replayEvent.Classification, Is.Not.Null);
                Assert.That(
                    replayEvent.TestedEnvelope.Version,
                    Is.EqualTo(ContractVersions.ReplayV4));
                Assert.That(
                    replayEvent.ExecutableEnvelope.Version,
                    Is.EqualTo(ContractVersions.ReplayV4));
                AssertV4Identity(
                    replayEvent.TestedEnvelope.Identity,
                    "tested execution envelope");
                AssertV4Identity(
                    replayEvent.ExecutableEnvelope.Identity,
                    "executable execution envelope");
                AssertV4Identity(
                    replayEvent.TestedEnvelope.DerivedAttributesFingerprint,
                    "derived V4 attributes");
                AssertV4Identity(
                    replayEvent.Trajectory.ArtifactIdentity,
                    "trajectory artifact");
                AssertV4Identity(
                    replayEvent.Trajectory.CacheKey.Identity,
                    "trajectory cache key");
                Assert.That(
                    replayEvent.Trajectory.CacheKey.BallStateVersion,
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(replayEvent.RuleDecision.RulesVersion, Is.EqualTo(3));
                Assert.That(replayEvent.RuleDecision.Accepted, Is.True);
                Assert.That(replayEvent.Shadow, Is.Not.Null);
                Assert.That(
                    replayEvent.Shadow.SourceSequenceNumber,
                    Is.EqualTo(replayEvent.SequenceNumber + 1));
                Assert.That(
                    replayEvent.Shadow.ArtifactIdentity,
                    Is.EqualTo(replayEvent.Trajectory.ArtifactIdentity));
                Assert.That(replayEvent.Shadow.Home.TeamSide, Is.EqualTo("Home"));
                Assert.That(replayEvent.Shadow.Away.TeamSide, Is.EqualTo("Away"));
                Assert.That(replayEvent.Shadow.Home.PrimaryAssignments, Has.Count.EqualTo(6));
                Assert.That(replayEvent.Shadow.Away.PrimaryAssignments, Has.Count.EqualTo(6));
                Assert.That(replayEvent.Shadow.Coverage.Decision, Is.EqualTo("Covered"));
                foreach (var assignment in replayEvent.Shadow.Home.PrimaryAssignments
                    .Concat(replayEvent.Shadow.Away.PrimaryAssignments))
                {
                    Assert.That(assignment.PlayerId, Is.Not.Empty);
                    Assert.That(assignment.Task, Is.Not.Empty);
                    Assert.That(assignment.Condition, Is.Not.Empty);
                    Assert.That(assignment.SpatialClaim, Is.Not.Empty);
                    Assert.That(assignment.DeclaredBranch, Is.EqualTo("Primary"));
                    Assert.That(assignment.Value, Is.Not.NaN);
                    Assert.That(assignment.Rank, Is.GreaterThan(0));
                }
                Assert.That(
                    replayEvent.EventKind == "Attack",
                    Is.EqualTo(replayEvent.ObservedP6Geometry != null));
                if (replayEvent.EventKind == "Receive" ||
                    replayEvent.EventKind == "Set")
                {
                    Assert.That(
                        replayEvent.OrganizationAuthority,
                        Is.Not.Null);
                    Assert.That(
                        replayEvent.OrganizationAuthority
                            .ExecutableEnvelopeIdentity,
                        Is.EqualTo(
                            replayEvent.ExecutableEnvelope.Identity));
                    Assert.That(
                        replayEvent.OrganizationAuthority
                            .TrajectoryArtifactIdentity,
                        Is.EqualTo(
                            replayEvent.Trajectory.ArtifactIdentity));
                }
                else
                {
                    Assert.That(
                        replayEvent.OrganizationAuthority,
                        Is.Null);
                }
                if (replayEvent.EventKind == "Set")
                {
                    Assert.That(replayEvent.AttackDefenseAuthority, Is.Not.Null);
                    Assert.That(replayEvent.AttackDefenseAuthority.Phase,
                        Is.EqualTo("SetIntentPlanned"));
                }
                if (replayEvent.AttackDefenseAuthority != null)
                {
                    Assert.That(replayEvent.AttackDefenseAuthority.PlanRevision,
                        Is.GreaterThanOrEqualTo(0));
                    Assert.That(replayEvent.AttackDefenseAuthority.SourceSequenceNumber,
                        Is.GreaterThan(0));
                    Assert.That(replayEvent.AttackDefenseAuthority.TestedEnvelopeIdentity,
                        Is.EqualTo(replayEvent.TestedEnvelope.Identity));
                    Assert.That(replayEvent.AttackDefenseAuthority.ExecutableEnvelopeIdentity,
                        Is.EqualTo(replayEvent.ExecutableEnvelope.Identity));
                    Assert.That(replayEvent.AttackDefenseAuthority.TrajectoryArtifactIdentity,
                        Is.EqualTo(replayEvent.Trajectory.ArtifactIdentity));
                }
            }
            Assert.That(restored.Events.Any(replayEvent =>
                replayEvent.AttackDefenseAuthority != null), Is.True);
            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
            Assert.That(restored.ReplayHash, Is.EqualTo(replay.ReplayHash));
        }

        [UnityTest]
        public IEnumerator AcceptedFormalContact_RecordsOneReadOnlyTwelvePlayerShadowPlan()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            RallyPlanV3 recordedPlan = null;
            ContactObservation observation = default;
            var verifiedObservation = false;

            director.ReplayShadowPlanRecorded += plan =>
            {
                Assert.That(recordedPlan, Is.Null, "The first accepted contact records one revision.");
                recordedPlan = plan;
                observation = Observe(director, players);
            };
            director.ReplayContactAccepted += _ =>
            {
                if (recordedPlan == null || verifiedObservation)
                {
                    return;
                }

                Assert.That(Observe(director, players), Is.EqualTo(observation));
                verifiedObservation = true;
            };

            var timeout = Time.realtimeSinceStartup + 30f;
            while (!verifiedObservation && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(verifiedObservation, Is.True, "The formal fixture did not accept a contact.");
            Assert.That(recordedPlan, Is.Not.Null);
            Assert.That(recordedPlan.Revision, Is.EqualTo(recordedPlan.SourceSequence));
            Assert.That(recordedPlan.WorldSnapshot.Players, Has.Count.EqualTo(12));
            Assert.That(recordedPlan.HomePlan.Assignments, Has.Count.EqualTo(6));
            Assert.That(recordedPlan.AwayPlan.Assignments, Has.Count.EqualTo(6));
            Assert.That(
                recordedPlan.HomePlan.CandidateEvidence.Single(),
                Is.EqualTo("artifact=" + recordedPlan.ArtifactIdentity));
            Assert.That(
                recordedPlan.AwayPlan.CandidateEvidence.Single(),
                Is.EqualTo("artifact=" + recordedPlan.ArtifactIdentity));
            Assert.That(
                recordedPlan.WorldSnapshot.LatestEvent.CoverageReason,
                Is.EqualTo(PlanCoverageReason.WithinConditionalEnvelope));
            Assert.That(
                recordedPlan.CoverageDecision.ActivatedDeclaredBranch,
                Is.EqualTo(RallyPlanBranchV3.Primary));
            Assert.That(
                recordedPlan.WorldSnapshot.LatestEvent.ContactGroup,
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                recordedPlan.WorldSnapshot.Court.HalfLength,
                Is.EqualTo(director.CourtHalfLength));
        }

        [UnityTest]
        public IEnumerator ThrowingShadowObserver_DoesNotAbortAcceptedFormalContact()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var acceptedContacts = 0;
            director.ReplayShadowPlanRecorded += _ =>
                throw new InvalidOperationException("shadow observer failure");
            director.ReplayContactAccepted += _ => acceptedContacts++;

            var timeout = Time.realtimeSinceStartup + 30f;
            while (acceptedContacts == 0 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(acceptedContacts, Is.GreaterThan(0));
            Assert.That(director.SuccessfulContacts, Is.EqualTo(acceptedContacts));
        }

        [UnityTest]
        public IEnumerator ShadowListener_DoesNotChangeFixedSeedAcceptedContactSequence()
        {
            ContactSequence withoutShadow = default;
            ContactSequence withShadow = default;
            for (var run = 0; run < 2; run++)
            {
                yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
                var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
                var transitions = new List<string>();
                if (run == 1)
                {
                    director.ReplayShadowPlanRecorded += _ => { };
                }

                director.ReplayContactAccepted += replayEvent =>
                    transitions.Add(
                        replayEvent.RuleTransition.Accepted + ":" +
                        replayEvent.RuleTransition.RejectionReason + ":" +
                        replayEvent.RuleTransition.After.CountedHits);
                var timeout = Time.realtimeSinceStartup + 30f;
                while (transitions.Count < 3 && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                Assert.That(transitions, Has.Count.EqualTo(3));
                var observation = new ContactSequence(
                    director.HomeScore,
                    director.AwayScore,
                    director.SuccessfulContacts,
                    director.V3RuleTransitions,
                    transitions.ToArray());
                if (run == 0)
                {
                    withoutShadow = observation;
                }
                else
                {
                    withShadow = observation;
                }
            }

            Assert.That(withShadow, Is.EqualTo(withoutShadow));
        }

        [UnityTest]
        public IEnumerator Capture_StartedAfterV3TransitionBindsNextShadowWithoutInterruptingRally()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            MatchReplayRecorder recorder = null;
            var captureBase = -1;

            director.ReplayContactAccepted += _ =>
            {
                if (recorder != null || director.V3RuleTransitions < 1)
                {
                    return;
                }

                captureBase = director.V3RuleTransitions;
                recorder = MatchReplayRecorder.Attach(director, ball, players);
                recorder.StartCapture();
            };

            var timeout = Time.realtimeSinceStartup + 90f;
            while ((recorder == null || !recorder.IsComplete) &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(captureBase, Is.GreaterThanOrEqualTo(1));
            Assert.That(recorder, Is.Not.Null);
            Assert.That(recorder.IsComplete, Is.True, "Mid-rally capture interrupted the live rally.");
            var replay = recorder.Complete();
            Assert.That(replay.Events, Is.Not.Empty);
            Assert.That(director.V3RuleTransitions, Is.GreaterThan(captureBase));
            for (var index = 0; index < replay.Events.Count; index++)
            {
                Assert.That(replay.Events[index].Shadow, Is.Not.Null);
                Assert.That(
                    replay.Events[index].Shadow.SourceSequenceNumber,
                    Is.EqualTo(captureBase + index + 1));
            }
        }

        [UnityTest]
        public IEnumerator Capture_StartedMidRallyWithoutContactInvalidatesWithoutInterruptingResolution()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            var recorder = MatchReplayRecorder.Attach(director, ball, players);
            var resolved = 0;
            director.ReplayRallyResolved += _ => resolved++;
            recorder.StartCapture();

            var resolveRally = typeof(PhysicalMatchRallyDirector).GetMethod(
                "ResolveRally",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolveRally, Is.Not.Null);
            Assert.DoesNotThrow(() => resolveRally.Invoke(
                director,
                new object[]
                {
                    new RallyOutcome(TeamSide.Home, true, "test zero-contact capture"),
                    null,
                    null,
                    "test zero-contact capture"
                }));

            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(director.HomeScore, Is.EqualTo(1));
            Assert.That(recorder.IsComplete, Is.False);
            Assert.That(recorder.CaptureFailureReason, Does.Contain("at least one contact"));
            Assert.That(() => recorder.Complete(), Throws.TypeOf<InvalidOperationException>());
        }

        [UnityTest]
        public IEnumerator Capture_UnresolvedShadowInvalidatesReplayWithoutInterruptingRally()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            var recorder = MatchReplayRecorder.Attach(director, ball, players);
            var recordShadowPlan = typeof(MatchReplayRecorder).GetMethod(
                "RecordShadowPlan",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var injected = false;
            var resolvedRallies = 0;

            director.ReplayShadowPlanRecorded += plan =>
            {
                if (injected)
                {
                    return;
                }

                injected = true;
                var unresolvedPlan = new RallyPlanV3(
                    plan.WorldSnapshot,
                    plan.HomePlan,
                    plan.AwayPlan,
                    plan.ArtifactIdentity,
                    plan.Revision,
                    plan.SourceSequence + 100,
                    plan.CoverageDecision);
                recordShadowPlan.Invoke(recorder, new object[] { unresolvedPlan });
            };
            director.ReplayRallyResolved += _ => resolvedRallies++;
            recorder.StartCapture();

            var timeout = Time.realtimeSinceStartup + 90f;
            while (resolvedRallies == 0 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(injected, Is.True, "The formal fixture did not record a shadow plan.");
            Assert.That(resolvedRallies, Is.EqualTo(1), "Invalid capture interrupted the live rally.");
            Assert.That(recorder.IsComplete, Is.False);
            Assert.That(recorder.CaptureFailureReason, Does.Contain("Unmatched shadow revisions"));
            Assert.That(() => recorder.Complete(), Throws.TypeOf<InvalidOperationException>());
        }

        [UnityTest]
        public IEnumerator Capture_TwoIndependentGateIFixedSeedRunsAreByteStable()
        {
            var payloads = new byte[2][];
            var acceptedSequences = new string[2][];
            var v3Transitions = new int[2];
            MatchReplayV4 first = null;
            MatchReplayV4 second = null;
            for (var run = 0; run < 2; run++)
            {
                yield return SceneManager.LoadSceneAsync(
                    "FormalIndoor6v6",
                    LoadSceneMode.Single);
                var director =
                    UnityEngine.Object.FindFirstObjectByType<
                        FormalSixVsSixRallyDirector>();
                var ball =
                    UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
                var players =
                    UnityEngine.Object.FindObjectsByType<
                        PrototypePlayerAgent>(
                        FindObjectsSortMode.None);
                var recorder = MatchReplayRecorder.Attach(
                    director,
                    ball,
                    players);
                recorder.StartCapture();

                var timeout = Time.realtimeSinceStartup + 90f;
                while (!recorder.IsComplete &&
                       Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                Assert.That(
                    recorder.IsComplete,
                    Is.True,
                    "Independent formal run " + run +
                    " did not complete its first rally.");
                var replay = recorder.Complete();
                payloads[run] = Encoding.UTF8.GetBytes(
                    ContractJson.SerializeV4(replay));
                acceptedSequences[run] = replay.Events
                    .Select(replayEvent =>
                        replayEvent.SequenceNumber + ":" +
                        replayEvent.ActorPlayerId + ":" +
                        replayEvent.EventKind + ":" +
                        replayEvent.RuleDecision.ReasonCode)
                    .ToArray();
                v3Transitions[run] = director.V3RuleTransitions;
                if (run == 0)
                {
                    first = replay;
                }
                else
                {
                    second = replay;
                }
            }

            CollectionAssert.AreEqual(payloads[0], payloads[1]);
            CollectionAssert.AreEqual(acceptedSequences[0], acceptedSequences[1]);
            Assert.That(v3Transitions[0], Is.GreaterThan(0));
            Assert.That(v3Transitions[1], Is.EqualTo(v3Transitions[0]));
            Assert.That(second.ReplayHash, Is.EqualTo(first.ReplayHash));
            Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
            for (var eventIndex = 0;
                 eventIndex < first.Events.Count;
                 eventIndex++)
            {
                var left = first.Events[eventIndex];
                var right = second.Events[eventIndex];
                Assert.That(left.SequenceNumber, Is.EqualTo(eventIndex));
                Assert.That(right.SequenceNumber, Is.EqualTo(eventIndex));
                Assert.That(
                    right.TestedEnvelope.Identity,
                    Is.EqualTo(left.TestedEnvelope.Identity));
                Assert.That(
                    right.ExecutableEnvelope.Identity,
                    Is.EqualTo(left.ExecutableEnvelope.Identity));
                Assert.That(
                    right.Trajectory.ArtifactIdentity,
                    Is.EqualTo(left.Trajectory.ArtifactIdentity));
                Assert.That(
                    right.Trajectory.CacheKey.Identity,
                    Is.EqualTo(left.Trajectory.CacheKey.Identity));
                Assert.That(
                    right.Classification.Kind,
                    Is.EqualTo(left.Classification.Kind));
                Assert.That(right.HomeScore, Is.EqualTo(left.HomeScore));
                Assert.That(right.AwayScore, Is.EqualTo(left.AwayScore));
                Assert.That(right.ActorPlayerId, Is.EqualTo(left.ActorPlayerId));
                Assert.That(right.EventKind, Is.EqualTo(left.EventKind));
                Assert.That(right.Shadow, Is.Not.Null);
                Assert.That(left.Shadow, Is.Not.Null);
                Assert.That(
                    right.Shadow.ArtifactIdentity,
                    Is.EqualTo(left.Shadow.ArtifactIdentity));
                Assert.That(right.AttackDefenseAuthority == null,
                    Is.EqualTo(left.AttackDefenseAuthority == null));
                if (left.AttackDefenseAuthority != null)
                {
                    Assert.That(right.AttackDefenseAuthority.PlanRevision,
                        Is.EqualTo(left.AttackDefenseAuthority.PlanRevision));
                    Assert.That(right.AttackDefenseAuthority.SourceSequenceNumber,
                        Is.EqualTo(left.AttackDefenseAuthority.SourceSequenceNumber));
                    Assert.That(right.AttackDefenseAuthority.Phase,
                        Is.EqualTo(left.AttackDefenseAuthority.Phase));
                    Assert.That(right.AttackDefenseAuthority.SelectedCandidateIdentity,
                        Is.EqualTo(left.AttackDefenseAuthority.SelectedCandidateIdentity));
                    Assert.That(right.AttackDefenseAuthority.Coverage.Decision,
                        Is.EqualTo(left.AttackDefenseAuthority.Coverage.Decision));
                    Assert.That(right.AttackDefenseAuthority.TestedEnvelopeIdentity,
                        Is.EqualTo(left.AttackDefenseAuthority.TestedEnvelopeIdentity));
                    Assert.That(right.AttackDefenseAuthority.TrajectoryArtifactIdentity,
                        Is.EqualTo(left.AttackDefenseAuthority.TrajectoryArtifactIdentity));
                }
                Assert.That(
                    right.AbilityConsumptions.Count,
                    Is.EqualTo(left.AbilityConsumptions.Count));
                for (var consumptionIndex = 0;
                     consumptionIndex <
                     left.AbilityConsumptions.Count;
                     consumptionIndex++)
                {
                    var leftConsumption =
                        left.AbilityConsumptions[consumptionIndex];
                    var rightConsumption =
                        right.AbilityConsumptions[consumptionIndex];
                    Assert.That(
                        rightConsumption.AttributeName,
                        Is.EqualTo(leftConsumption.AttributeName));
                    Assert.That(
                        rightConsumption.Value,
                        Is.EqualTo(leftConsumption.Value));
                    Assert.That(
                        rightConsumption.EvidenceKind,
                        Is.EqualTo("ExecutionEnvelopeFactoryRead"));
                }
            }
        }

        private static void AssertV4Identity(string value, string subject)
        {
            Assert.That(value, Is.Not.Null.And.Length.EqualTo(64), subject);
        }

        private static ContactObservation Observe(
            FormalSixVsSixRallyDirector director,
            IEnumerable<PrototypePlayerAgent> players)
        {
            return new ContactObservation(
                director.HomeScore,
                director.AwayScore,
                director.SuccessfulContacts,
                players.OrderBy(player => player.StableId.Value)
                    .Select(player => player.ReplayScheduledAction + ":" +
                                      player.ScheduledMovementTarget)
                    .ToArray());
        }

        private readonly struct ContactObservation : IEquatable<ContactObservation>
        {
            private readonly int _homeScore;
            private readonly int _awayScore;
            private readonly int _contacts;
            private readonly string[] _players;

            public ContactObservation(int homeScore, int awayScore, int contacts, string[] players)
            {
                _homeScore = homeScore;
                _awayScore = awayScore;
                _contacts = contacts;
                _players = players;
            }

            public bool Equals(ContactObservation other)
            {
                return _homeScore == other._homeScore &&
                       _awayScore == other._awayScore &&
                       _contacts == other._contacts &&
                       _players.SequenceEqual(other._players);
            }

            public override bool Equals(object obj)
            {
                return obj is ContactObservation other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _contacts;
            }
        }

        private readonly struct ContactSequence : IEquatable<ContactSequence>
        {
            private readonly int _homeScore;
            private readonly int _awayScore;
            private readonly int _contacts;
            private readonly int _transitions;
            private readonly string[] _acceptedSequence;

            public ContactSequence(
                int homeScore,
                int awayScore,
                int contacts,
                int transitions,
                string[] acceptedSequence)
            {
                _homeScore = homeScore;
                _awayScore = awayScore;
                _contacts = contacts;
                _transitions = transitions;
                _acceptedSequence = acceptedSequence;
            }

            public bool Equals(ContactSequence other)
            {
                return _homeScore == other._homeScore &&
                       _awayScore == other._awayScore &&
                       _contacts == other._contacts &&
                       _transitions == other._transitions &&
                       _acceptedSequence.SequenceEqual(other._acceptedSequence);
            }

            public override bool Equals(object obj)
            {
                return obj is ContactSequence other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _contacts;
            }
        }
    }
}

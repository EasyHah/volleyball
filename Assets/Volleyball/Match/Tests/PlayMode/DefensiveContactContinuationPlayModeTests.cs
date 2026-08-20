using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.PlayModeTests
{
    // These tests only select a complete startup asset before FormalIndoor6v6
    // loads. Once the scene is running, the probe is strictly observational.
    public sealed class DefensiveContactContinuationPlayModeTests
    {
        private const int RallyFrameLimit = 4200;

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CommittedFloorDefense_DigsReachableSpike()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe("ReachableFloorDefense", value => probe = value);

            yield return probe.WaitFor(() =>
                probe.HasAcceptedCommittedReceiveWithOrganization ||
                probe.HasRallyResult);

            Assert.That(probe.FloorDefenses, Is.Not.Empty,
                probe.Evidence("Gate I did not commit FloorDefense."));
            Assert.That(probe.HasAcceptedCommittedReceive, Is.True,
                probe.Evidence("A committed FloorDefense did not become a physical Receive."));
            Assert.That(probe.HasAcceptedReceiveWithV3AndReplay, Is.True,
                probe.Evidence("The physical Receive was not accepted by V3 and replay."));
            Assert.That(probe.HasAcceptedCommittedReceiveWithOrganization, Is.True,
                probe.Evidence("The committed Receive did not continue to a teammate Set."));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ServeNetDeflection_ReplansAndReceivesActualBall()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(
                "ServeNetDeflection",
                value => probe = value);

            yield return probe.WaitFor(() =>
                probe.HasAcceptedPostNetServeReceive ||
                probe.HasRallyResult);

            Assert.That(probe.NetContactTimes, Is.Not.Empty,
                probe.Evidence("The opening serve did not physically contact the net."));
            Assert.That(probe.Director.NetDeflectionDispatches,
                Is.EqualTo(1),
                probe.Evidence("One serve flight must publish one net deflection dispatch."));
            Assert.That(probe.Director.SuppressedNetDeflectionDispatches,
                Is.Zero,
                probe.Evidence("The single deflection scenario unexpectedly suppressed a dispatch."));
            Assert.That(probe.Director.ServeNetReceiveReplans,
                Is.EqualTo(1),
                probe.Evidence("The serve net contact did not create one replacement plan."));
            Assert.That(probe.PrimaryReceiveReceipts
                    .Select(receipt => receipt.PlanRevision)
                    .Distinct()
                    .Count(),
                Is.GreaterThanOrEqualTo(2),
                probe.Evidence("The net deflection did not replace the stale Gate H plan."));
            Assert.That(probe.PrimaryReceiveReceipts
                    .Select(receipt => receipt.SourceSequence)
                    .Distinct()
                    .Count(),
                Is.GreaterThanOrEqualTo(2),
                probe.Evidence("The replacement plan reused the stale source sequence."));
            Assert.That(probe.NetCrossings.Any(crossing =>
                    crossing.SimulationTimeSeconds >= probe.NetContactTimes[0]),
                Is.True,
                probe.Evidence("The deflected serve never crossed legally."));
            Assert.That(probe.HasAcceptedPostNetServeReceive, Is.True,
                probe.Evidence("The replanned ServeReceive did not become a physical contact."));
            var postNetReceive = probe.PostNetServeReceives[0];
            Assert.That(probe.NetCrossings.Any(crossing =>
                    crossing.SimulationTimeSeconds <=
                    postNetReceive.ContactSimulationTime),
                Is.True,
                probe.Evidence("The Receive became physical before legal net crossing."));
            Assert.That(probe.HasAcceptedPostNetServeReceiveWithV3AndReplay, Is.True,
                probe.Evidence("The post-net Receive was not accepted by V3 and replay."));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ServeNetDeflectionMiss_StillLetsGroundRefereeScore()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(
                "ServeNetDeflectionMiss",
                value => probe = value);

            yield return probe.WaitFor(() => probe.HasRallyResult);

            Assert.That(probe.NetContactTimes, Is.Not.Empty,
                probe.Evidence("The opening serve did not physically contact the net."));
            Assert.That(probe.Director.NetDeflectionDispatches,
                Is.EqualTo(1),
                probe.Evidence("One serve flight must publish one net deflection dispatch."));
            Assert.That(probe.NetCrossings.Any(crossing =>
                    crossing.SimulationTimeSeconds >= probe.NetContactTimes[0]),
                Is.True,
                probe.Evidence("The missed deflection never crossed to the receiving side."));
            Assert.That(probe.Director.ServeNetReceiveReplans,
                Is.EqualTo(1),
                probe.Evidence("The receiving team did not react to the legal deflection."));
            Assert.That(probe.PostNetServeReceives, Is.Empty,
                probe.Evidence("An unreachable receiver gained a magnetic contact."));
            Assert.That(probe.GroundEvents, Has.Count.EqualTo(1),
                probe.Evidence("The missed deflection must reach the ground once."));
            Assert.That(probe.Results, Has.Count.EqualTo(1),
                probe.Evidence("The missed deflection must resolve once."));
            Assert.That(probe.Results[0].Team,
                Is.EqualTo(Volleyball.Domain.Prototype.TeamId.Blue),
                probe.Evidence("The legal missed serve awarded the wrong team."));
            Assert.That(probe.Results[0].Reason,
                Is.EqualTo("legal opponent-court landing"));
            Assert.That(probe.Results[0].PlayerId, Is.Not.Null,
                probe.Evidence("The legal missed serve did not retain scorer attribution."));
            Assert.That(probe.Results[0].ErrorPlayerId, Is.Null,
                probe.Evidence("An untouched legal serve charged a receiver error."));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ServeNetRebound_DoesNotOpenReceivingCandidate()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(
                "ServeNetRebound",
                value => probe = value);

            yield return probe.WaitFor(() => probe.HasRallyResult);

            Assert.That(probe.NetContactTimes, Is.Not.Empty,
                probe.Evidence("The opening serve did not physically contact the net."));
            Assert.That(probe.Director.NetDeflectionDispatches,
                Is.EqualTo(1),
                probe.Evidence("One serve flight must publish one net deflection dispatch."));
            Assert.That(probe.NetCrossings.Where(crossing =>
                    crossing.SimulationTimeSeconds >= probe.NetContactTimes[0]),
                Is.Empty,
                probe.Evidence("A net-face rebound must not be reported as a legal crossing."));
            Assert.That(probe.PostNetServeReceives, Is.Empty,
                probe.Evidence("A serve rebounding to the serving side opened a Receive."));
            Assert.That(probe.Director.ServeNetReceiveReplans, Is.Zero,
                probe.Evidence("A net-face rebound created a replacement Receive plan."));
            Assert.That(probe.PrimaryReceiveReceipts
                    .Select(receipt => receipt.PlanRevision)
                    .Distinct()
                    .Count(),
                Is.EqualTo(1),
                probe.Evidence("A net-face rebound published a new Gate H revision."));
            Assert.That(probe.Results, Has.Count.EqualTo(1));
            Assert.That(probe.Results[0].Team,
                Is.EqualTo(Volleyball.Domain.Prototype.TeamId.Orange),
                probe.Evidence("A serve rebounding onto the serving side awarded the wrong team."));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ServeNetNoContactOutcomes_AreStableAcrossIndependentRuns()
        {
            var scenarios = new[]
            {
                "ServeNetDeflectionMiss",
                "ServeNetRebound"
            };

            foreach (var scenario in scenarios)
            {
                ServeNetOutcomeSnapshot first = null;
                ServeNetOutcomeSnapshot second = null;
                yield return CaptureServeNetOutcome(
                    scenario,
                    value => first = value);
                yield return CaptureServeNetOutcome(
                    scenario,
                    value => second = value);

                Assert.That(second, Is.EqualTo(first),
                    scenario + " outcome changed across independent runs.");
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator LateFloorDefense_DoesNotCreateMagicDig()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe("LateFloorDefense", value => probe = value);

            yield return probe.WaitFor(() =>
                probe.ObservedMissedFloorDefense || probe.HasRallyResult);

            Assert.That(probe.ObservedMissedFloorDefense, Is.True,
                probe.Evidence("The scenario did not reach a missed committed defense."));
            Assert.That(probe.MissedFloorDefenseReceiveCount, Is.Zero,
                probe.Evidence("A late defender must not gain a magnetic Receive."));
            Assert.That(probe.MissedFloorDefenseHadEvidence, Is.True,
                probe.Evidence("The missed defense did not emit rejection or expiration evidence."));
            Assert.That(probe.MissedFloorDefenseGroundCount, Is.EqualTo(1),
                probe.Evidence("The miss must reach the ground referee exactly once."));
            Assert.That(probe.MissedFloorDefenseResultCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BlockReboundToAttackingSide_AllowsAttackCoverage()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe("AttackSideBlockRebound", value => probe = value);

            yield return probe.WaitFor(() =>
                (probe.HasResolvedAttackCoverage &&
                 probe.HasAcceptedAttackCoverReceiveWithOrganization) ||
                probe.HasRallyResult);

            Assert.That(probe.Blocks, Is.Not.Empty,
                probe.Evidence("The scenario did not reach a physical block."));
            Assert.That(probe.HasResolvedAttackCoverage, Is.True,
                probe.Evidence("The post-block continuation did not resolve to attack coverage."));
            Assert.That(probe.HasAcceptedAttackCoverReceive, Is.True,
                probe.Evidence("The declared AttackCover did not produce a physical Receive."));
            Assert.That(probe.HasAcceptedAttackCoverReceiveWithOrganization, Is.True,
                probe.Evidence("The AttackCover Receive did not continue to a teammate Set."));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BlockReboundToDefendingSide_AllowsBlockRecovery()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe("BlockingSideBlockRebound", value => probe = value);

            yield return probe.WaitFor(() =>
                (probe.HasResolvedBlockingRecovery &&
                 probe.HasAcceptedBlockingRecoveryReceiveWithOrganization) ||
                probe.HasRallyResult);

            Assert.That(probe.Blocks, Is.Not.Empty,
                probe.Evidence("The scenario did not reach a physical block."));
            Assert.That(probe.HasResolvedBlockingRecovery, Is.True,
                probe.Evidence("The post-block continuation did not resolve to blocker-side recovery."));
            Assert.That(probe.HasAcceptedBlockingRecoveryReceive, Is.True,
                probe.Evidence("The declared blocker-side recovery did not produce a physical Receive."));
            Assert.That(probe.HasAcceptedBlockingRecoveryReceiveWithOrganization, Is.True,
                probe.Evidence("The blocker-side Receive did not continue to a teammate Set."));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PostBlockMiss_StillLetsGroundRefereeScore()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe("PostBlockMiss", value => probe = value);

            yield return probe.WaitFor(() =>
                probe.ObservedPostBlockMiss || probe.HasRallyResult);

            Assert.That(probe.Blocks, Is.Not.Empty,
                probe.Evidence("The scenario did not reach a physical block."));
            Assert.That(probe.ObservedPostBlockMiss, Is.True,
                probe.Evidence("The scenario did not reach a post-block miss."));
            Assert.That(probe.PostBlockMissReceiveCount, Is.Zero,
                probe.Evidence("A post-block miss must not create a Receive."));
            Assert.That(probe.PostBlockMissHadEvidence, Is.True,
                probe.Evidence("The missed post-block receive lacks diagnostic evidence."));
            Assert.That(probe.PostBlockMissGroundCount, Is.EqualTo(1));
            Assert.That(probe.PostBlockMissResultCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OverlappingDefenders_AcceptOnlyOneReceive()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe("OverlappingDefenders", value => probe = value);

            yield return probe.WaitFor(() =>
                probe.CommittedReceiveContacts.Count > 0 ||
                probe.HasRallyResult);

            Assert.That(probe.FloorDefenses.Count, Is.GreaterThanOrEqualTo(2),
                probe.Evidence("The scenario did not commit overlapping defense responsibilities."));
            Assert.That(probe.CommittedReceiveContacts, Has.Count.EqualTo(1),
                probe.Evidence("One geometry group must select one stable Receive winner."));
            var physical = probe.CommittedReceiveContacts[0];
            Assert.That(probe.AcceptedReplayReceives.Any(replay =>
                    replay.RuleTransition.After.LastContactGroup ==
                    physical.Hit.ContactGroupId),
                Is.True,
                probe.Evidence("The stable physical winner must be the V3/replay winner."));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CompleteScenarioReplays_AreStableAcrossIndependentRuns()
        {
            var scenarios = new[]
            {
                new KeyValuePair<string, string>(
                    "ReachableFloorDefense", "reachable-floor-defense"),
                new KeyValuePair<string, string>(
                    "LateFloorDefense", "late-floor-defense"),
                new KeyValuePair<string, string>(
                    "AttackSideBlockRebound", "attack-side-block-rebound"),
                new KeyValuePair<string, string>(
                    "BlockingSideBlockRebound", "blocking-side-block-rebound"),
                new KeyValuePair<string, string>(
                    "PostBlockMiss", "post-block-miss"),
                new KeyValuePair<string, string>(
                    "OverlappingDefenders", "overlapping-defenders"),
                new KeyValuePair<string, string>(
                    "ServeNetDeflection", "serve-net-deflection")
            };

            foreach (var scenario in scenarios)
            {
                ScenarioReplaySnapshot first = null;
                ScenarioReplaySnapshot second = null;
                yield return CaptureScenarioReplay(
                    scenario.Key,
                    scenario.Value,
                    value => first = value);
                yield return CaptureScenarioReplay(
                    scenario.Key,
                    scenario.Value,
                    value => second = value);

                Assert.That(second.Json, Is.EqualTo(first.Json),
                    scenario.Key + " canonical replay JSON changed.");
                Assert.That(second.Html, Is.EqualTo(first.Html),
                    scenario.Key + " canonical HTML changed.");
                Assert.That(second.ReplayHash, Is.EqualTo(first.ReplayHash),
                    scenario.Key + " replay hash changed.");
                CollectionAssert.AreEqual(
                    first.AcceptedContacts,
                    second.AcceptedContacts,
                    scenario.Key + " accepted contact order changed.");
                CollectionAssert.AreEqual(
                    first.DefenseAttempts,
                    second.DefenseAttempts,
                    scenario.Key + " continuation diagnostics changed.");
                Assert.That(second.Result, Is.EqualTo(first.Result),
                    scenario.Key + " result changed.");
                Assert.That(second.V3Transitions,
                    Is.EqualTo(first.V3Transitions),
                    scenario.Key + " V3 transition count changed.");
            }
        }

        private static IEnumerator CaptureServeNetOutcome(
            string scenarioName,
            Action<ServeNetOutcomeSnapshot> captured)
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(
                scenarioName,
                value => probe = value);
            yield return probe.WaitFor(() => probe.HasRallyResult);

            Assert.That(probe.Results, Has.Count.EqualTo(1),
                probe.Evidence(scenarioName + " did not resolve exactly once."));
            captured(new ServeNetOutcomeSnapshot(
                probe.Director.ServeNetContacts,
                probe.Director.ServeNetReceiveReplans,
                probe.NetCrossings.Count,
                probe.PostNetServeReceives.Count,
                probe.GroundEvents.Count,
                probe.Results[0].Team,
                probe.Results[0].Reason));
        }

        private static IEnumerator CaptureScenarioReplay(
            string scenarioName,
            string expectedScenarioId,
            Action<ScenarioReplaySnapshot> captured)
        {
            var scenario = Resources.Load<FormalMatchScenarioPresetV4>(
                "FormalMatchScenariosV4/" + scenarioName);
            Assert.That(scenario, Is.Not.Null,
                "Missing complete formal scenario asset.");
            var definition = scenario.ToDefinition();
            Assert.That(definition.ScenarioId, Is.EqualTo(expectedScenarioId));
            FormalMatchScenarioStartupV4.PrepareNextFormalStart(definition);

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
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));

            ReplayRallyResolvedEvent result = null;
            director.ReplayRallyResolved += value => result = value;
            var recorder = MatchReplayRecorder.Attach(
                director,
                ball,
                players);
            recorder.StartCapture();
            for (var frame = 0;
                 frame < RallyFrameLimit &&
                 !recorder.IsComplete &&
                 string.IsNullOrEmpty(recorder.CaptureFailureReason);
                 frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(recorder.CaptureFailureReason, Is.Null.Or.Empty,
                scenarioName + " replay capture failed.");
            Assert.That(recorder.IsComplete, Is.True,
                scenarioName + " did not complete its first formal rally.");
            Assert.That(result, Is.Not.Null,
                scenarioName + " completed without a result event.");
            var replay = recorder.Complete();
            Assert.That(replay.Scenario.ScenarioId,
                Is.EqualTo(definition.ScenarioId));
            Assert.That(replay.Scenario.ContentHash,
                Is.EqualTo(definition.ContentHash));
            Assert.That(replay.Events, Is.Not.Empty);
            if (!definition.ScenarioId.StartsWith(
                    "serve-net-",
                    StringComparison.Ordinal))
            {
                Assert.That(replay.DefenseAttempts, Is.Not.Empty);
            }

            captured(new ScenarioReplaySnapshot(
                ContractJson.SerializeV4(replay),
                MatchReplayArtifactWriter.Render(replay),
                replay.ReplayHash,
                replay.Events.Select(replayEvent =>
                    replayEvent.SequenceNumber + ":" +
                    replayEvent.ActorPlayerId + ":" +
                    replayEvent.EventKind + ":" +
                    replayEvent.RuleDecision.ReasonCode).ToArray(),
                replay.DefenseAttempts.Select(attempt =>
                    attempt.Kind + ":" +
                    attempt.AttemptIdentity + ":" +
                    attempt.ContinuationState + ":" +
                    attempt.Reason).ToArray(),
                result.Team + ":" +
                (result.PlayerId.HasValue
                    ? result.PlayerId.Value.Value
                    : "none") + ":" +
                (result.ErrorPlayerId.HasValue
                    ? result.ErrorPlayerId.Value.Value
                    : "none") + ":" +
                result.Reason,
                director.V3RuleTransitions));
        }

        private static IEnumerator LoadFormalProbe(
            string scenarioName,
            Action<DefensiveContactProbe> loaded)
        {
            var scenario = Resources.Load<FormalMatchScenarioPresetV4>(
                "FormalMatchScenariosV4/" + scenarioName);
            Assert.That(scenario, Is.Not.Null, "Missing complete formal scenario asset.");
            var definition = scenario.ToDefinition();
            FormalMatchScenarioStartupV4.PrepareNextFormalStart(definition);

            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            Assert.That(director.FormalScenarioProvenance, Is.Not.Null);
            Assert.That(director.FormalScenarioProvenance.ScenarioId, Is.EqualTo(definition.ScenarioId));
            Assert.That(director.FormalScenarioProvenance.ContentHash, Is.EqualTo(definition.ContentHash));
            loaded(new DefensiveContactProbe(director, ball, players));
        }

        private sealed class ScenarioReplaySnapshot
        {
            public ScenarioReplaySnapshot(
                string json,
                string html,
                string replayHash,
                string[] acceptedContacts,
                string[] defenseAttempts,
                string result,
                int v3Transitions)
            {
                Json = json;
                Html = html;
                ReplayHash = replayHash;
                AcceptedContacts = acceptedContacts;
                DefenseAttempts = defenseAttempts;
                Result = result;
                V3Transitions = v3Transitions;
            }

            public string Json { get; }
            public string Html { get; }
            public string ReplayHash { get; }
            public string[] AcceptedContacts { get; }
            public string[] DefenseAttempts { get; }
            public string Result { get; }
            public int V3Transitions { get; }
        }

        private sealed class ServeNetOutcomeSnapshot : IEquatable<ServeNetOutcomeSnapshot>
        {
            public ServeNetOutcomeSnapshot(
                int netContacts,
                int receiveReplans,
                int netCrossings,
                int receives,
                int groundContacts,
                Volleyball.Domain.Prototype.TeamId winner,
                string reason)
            {
                NetContacts = netContacts;
                ReceiveReplans = receiveReplans;
                NetCrossings = netCrossings;
                Receives = receives;
                GroundContacts = groundContacts;
                Winner = winner;
                Reason = reason;
            }

            public int NetContacts { get; }
            public int ReceiveReplans { get; }
            public int NetCrossings { get; }
            public int Receives { get; }
            public int GroundContacts { get; }
            public Volleyball.Domain.Prototype.TeamId Winner { get; }
            public string Reason { get; }

            public bool Equals(ServeNetOutcomeSnapshot other) =>
                other != null &&
                NetContacts == other.NetContacts &&
                ReceiveReplans == other.ReceiveReplans &&
                NetCrossings == other.NetCrossings &&
                Receives == other.Receives &&
                GroundContacts == other.GroundContacts &&
                Winner == other.Winner &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                Equals(obj as ServeNetOutcomeSnapshot);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = NetContacts;
                    hash = (hash * 397) ^ ReceiveReplans;
                    hash = (hash * 397) ^ NetCrossings;
                    hash = (hash * 397) ^ Receives;
                    hash = (hash * 397) ^ GroundContacts;
                    hash = (hash * 397) ^ (int)Winner;
                    hash = (hash * 397) ^
                        (Reason == null
                            ? 0
                            : StringComparer.Ordinal.GetHashCode(Reason));
                    return hash;
                }
            }
        }

        private sealed class DefensiveContactProbe
        {
            private readonly Dictionary<RuntimePlayerId, StablePlayerId> _stableIds;

            public DefensiveContactProbe(
                FormalSixVsSixRallyDirector director,
                SimulatedBall ball,
                IEnumerable<PrototypePlayerAgent> players)
            {
                Director = director;
                _stableIds = players.ToDictionary(player => player.Id, player => player.StableId);
                director.AttackDefenseAuthorityCommitted += receipt => Receipts.Add(receipt);
                director.ReceiveOrganizationAuthorityCommitted +=
                    receipt => OrganizationReceipts.Add(receipt);
                director.ReplayContactAccepted += contact => ReplayContacts.Add(contact);
                director.ReplayDefenseAttemptRecorded += RecordDefenseAttempt;
                director.ReplayNetCrossed += crossing => NetCrossings.Add(crossing);
                director.ReplayGroundContact += RecordGround;
                director.ReplayRallyResolved += RecordRallyResult;
                ball.PlayerContact += RecordPhysicalContact;
                ball.EnvironmentContact += contact =>
                {
                    if (contact.Kind == EnvironmentContactKind.Net &&
                        director.ServeNetContacts > NetContactTimes.Count)
                    {
                        NetContactTimes.Add(ball.SimulationTime);
                    }
                };
            }

            public FormalSixVsSixRallyDirector Director { get; }
            public List<AttackDefenseAuthorityReceipt> Receipts { get; } = new();
            public List<ReceiveOrganizationAuthorityReceipt>
                OrganizationReceipts { get; } = new();
            public List<PlayerBallContactEvent> PhysicalContacts { get; } = new();
            public List<ReplayContactEvent> ReplayContacts { get; } = new();
            public List<ReplayDefenseAttemptEvent> DefenseAttempts { get; } = new();
            public List<ReplaySimpleEvent> NetCrossings { get; } = new();
            public List<ReplaySimpleEvent> GroundEvents { get; } = new();
            public List<ReplayRallyResolvedEvent> Results { get; } = new();
            public List<float> NetContactTimes { get; } = new();

            public List<AttackDefenseAuthorityReceipt> FloorDefenses => Receipts.Where(
                receipt => receipt.Kind == AttackDefenseCommandKind.FloorDefense).ToList();
            public List<ReceiveOrganizationAuthorityReceipt>
                PrimaryReceiveReceipts => OrganizationReceipts.Where(receipt =>
                    receipt.Kind ==
                    ReceiveOrganizationCommandKind.PrimaryReceive).ToList();
            public List<PlayerBallContactEvent> PostNetServeReceives =>
                NetContactTimes.Count == 0
                    ? new List<PlayerBallContactEvent>()
                    : PhysicalContacts.Where(contact =>
                        contact.Candidate.Action == TechniqueAction.Receive &&
                        contact.ContactSimulationTime >= NetContactTimes[0] &&
                        (!Blocks.Any() ||
                         contact.ContactSimulationTime <
                         Blocks[0].ContactSimulationTime)).ToList();
            public bool HasAcceptedPostNetServeReceive =>
                PostNetServeReceives.Count > 0;
            public bool HasAcceptedPostNetServeReceiveWithV3AndReplay =>
                PostNetServeReceives.Any(physical =>
                    ReplayContacts.Any(replay =>
                        replay.Action == TechniqueAction.Receive &&
                        replay.RuleTransition != null &&
                        replay.OrganizationAuthority != null &&
                        replay.AttackDefenseAuthority == null &&
                        replay.RuleTransition.After.LastContactGroup ==
                        physical.Hit.ContactGroupId));
            public List<PlayerBallContactEvent> Blocks => PhysicalContacts.Where(
                contact => contact.Candidate.Action == TechniqueAction.Block).ToList();
            public List<PlayerBallContactEvent> PostBlockReceives => PhysicalContacts.Where(
                contact => contact.Candidate.Action == TechniqueAction.Receive &&
                    Blocks.Count > 0 &&
                    contact.ContactSimulationTime >= Blocks[0].ContactSimulationTime).ToList();
            public List<PlayerBallContactEvent> CommittedReceiveContacts => PhysicalContacts.Where(contact =>
                contact.Candidate.Action == TechniqueAction.Receive &&
                IsCommittedReceive(contact)).ToList();
            public List<ReplayContactEvent> AcceptedReplayReceives => ReplayContacts.Where(contact =>
                contact.Action == TechniqueAction.Receive &&
                contact.RuleTransition != null &&
                contact.OrganizationAuthority != null &&
                contact.AttackDefenseAuthority != null).ToList();
            public bool HasRallyResult => Results.Count > 0;
            public bool HasAcceptedCommittedReceive => CommittedReceiveContacts.Count > 0;
            public bool HasAcceptedReceiveWithV3AndReplay => CommittedReceiveContacts.Any(physical =>
                HasAcceptedReplay(physical));
            public bool HasAcceptedCommittedReceiveWithOrganization =>
                CommittedReceiveContacts.Any(physical =>
                    HasAcceptedReplay(physical) &&
                    HasTeammateSetAfter(physical));
            public bool HasResolvedAttackCoverage => DefenseAttempts.Any(attempt =>
                attempt.Kind == "PostBlockContinuationResolved" &&
                attempt.Reason == PostAttackContinuationStateV4.AttackingSideCoverage.ToString());
            public bool HasResolvedBlockingRecovery => DefenseAttempts.Any(attempt =>
                attempt.Kind == "PostBlockContinuationResolved" &&
                attempt.Reason == PostAttackContinuationStateV4.BlockingSideRecovery.ToString());
            public bool HasAcceptedAttackCoverReceive =>
                HasAcceptedPostBlockReceive(
                    AttackDefenseCommandKind.AttackCover,
                    PostAttackContinuationStateV4.AttackingSideCoverage,
                    requireOrganization: false);
            public bool HasAcceptedAttackCoverReceiveWithOrganization =>
                HasAcceptedPostBlockReceive(
                    AttackDefenseCommandKind.AttackCover,
                    PostAttackContinuationStateV4.AttackingSideCoverage,
                    requireOrganization: true);
            public bool HasAcceptedBlockingRecoveryReceive =>
                HasAcceptedPostBlockReceive(
                    AttackDefenseCommandKind.FloorDefense,
                    PostAttackContinuationStateV4.BlockingSideRecovery,
                    requireOrganization: false);
            public bool HasAcceptedBlockingRecoveryReceiveWithOrganization =>
                HasAcceptedPostBlockReceive(
                    AttackDefenseCommandKind.FloorDefense,
                    PostAttackContinuationStateV4.BlockingSideRecovery,
                    requireOrganization: true);
            public bool ObservedMissedFloorDefense { get; private set; }
            public bool MissedFloorDefenseHadEvidence { get; private set; }
            public int MissedFloorDefenseReceiveCount { get; private set; }
            public int MissedFloorDefenseGroundCount { get; private set; }
            public int MissedFloorDefenseResultCount { get; private set; }
            public bool ObservedPostBlockMiss { get; private set; }
            public bool PostBlockMissHadEvidence { get; private set; }
            public int PostBlockMissReceiveCount { get; private set; }
            public int PostBlockMissGroundCount { get; private set; }
            public int PostBlockMissResultCount { get; private set; }

            private bool _missedFloorDefenseEvidenceThisRally;
            private bool _postBlockEvidenceThisRally;
            private bool _blockThisRally;
            private int _committedReceivesThisRally;
            private int _postBlockReceivesThisRally;
            private int _groundsThisRally;

            public IEnumerator WaitFor(Func<bool> predicate)
            {
                for (var frame = 0; frame < RallyFrameLimit && !predicate(); frame++)
                {
                    yield return new WaitForFixedUpdate();
                }
            }

            public string Evidence(string message) =>
                $"{message} receipts={Receipts.Count}; physical={PhysicalContacts.Count}; " +
                $"organization={OrganizationReceipts.Count}; netHits={NetContactTimes.Count}; " +
                $"serveLead={Director.LastServeNetGroundLeadSeconds:0.000}; " +
                $"replay={ReplayContacts.Count}; attempts={DefenseAttempts.Count}; " +
                $"floor={FloorDefenses.Count}; pendingNet={NetCrossings.Count}; " +
                $"ground={GroundEvents.Count}; results={Results.Count}; " +
                $"continuation={Director.PostAttackContinuationState}; " +
                $"attemptDetails=[{AttemptDetails()}]; " +
                $"contactDetails=[{ContactDetails()}]";

            private string AttemptDetails() =>
                string.Join(" | ", DefenseAttempts.Select(attempt =>
                {
                    var ball = attempt.BallPosition;
                    var velocity = attempt.BallVelocity;
                    return $"{attempt.Kind}:{attempt.Receipt.Kind}:" +
                           $"{attempt.Receipt.Actor.Value}:{attempt.Reason}:" +
                           $"at={attempt.SimulationTimeSeconds:0.00}:" +
                           $"window=({attempt.WindowStartSimulationTime:0.00}," +
                           $"{attempt.WindowEndSimulationTime:0.00}):" +
                           $"ball=({ball.X:0.00},{ball.Y:0.00},{ball.Z:0.00}):" +
                           $"velocity=({velocity.X:0.00},{velocity.Y:0.00}," +
                           $"{velocity.Z:0.00})";
                }));

            private string ContactDetails() =>
                string.Join(" | ", PhysicalContacts.Select(contact =>
                {
                    var actor = contact.Candidate.Actor;
                    return $"{contact.Candidate.Action}:" +
                           $"{(actor.HasValue ? actor.Value.ToString() : "none")}:" +
                           $"at={contact.ContactSimulationTime:0.00}:" +
                           $"group={contact.Hit.ContactGroupId}";
                }));

            private bool HasAcceptedPostBlockReceive(
                AttackDefenseCommandKind kind,
                PostAttackContinuationStateV4 state,
                bool requireOrganization)
            {
                var resolutionAttempts = DefenseAttempts.Where(attempt =>
                    attempt.Kind == "PostBlockContinuationResolved" &&
                    attempt.Reason == state.ToString() &&
                    attempt.Receipt.Kind == kind &&
                    attempt.Receipt.Execution != null);
                foreach (var attempt in resolutionAttempts)
                {
                    var group = attempt.Receipt.Execution.ContactGroupId;
                    var matchingPhysical = PhysicalContacts.Where(contact =>
                        contact.Candidate.Action == TechniqueAction.Receive &&
                        contact.Hit.ContactGroupId == group).ToArray();
                    if (matchingPhysical.Length == 0 ||
                        !HasAcceptedReplay(matchingPhysical[0]))
                    {
                        continue;
                    }

                    if (!requireOrganization ||
                        HasTeammateSetAfter(matchingPhysical[0]))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool HasAcceptedReplay(PlayerBallContactEvent physical) =>
                AcceptedReplayReceives.Any(replay =>
                    replay.PlayerId.HasValue &&
                    physical.Candidate.Actor.HasValue &&
                    replay.PlayerId.Value.Equals(
                        StableId(physical.Candidate.Actor.Value)) &&
                    replay.RuleTransition.After.LastContactGroup ==
                    physical.Hit.ContactGroupId);

            private bool HasTeammateSetAfter(PlayerBallContactEvent receive)
            {
                if (!receive.Candidate.Actor.HasValue)
                {
                    return false;
                }

                var receiver = receive.Candidate.Actor.Value;
                return PhysicalContacts.Any(contact =>
                    contact.Candidate.Action == TechniqueAction.Set &&
                    contact.Candidate.Actor.HasValue &&
                    contact.Candidate.Actor.Value.Team == receiver.Team &&
                    !contact.Candidate.Actor.Value.Equals(receiver) &&
                    contact.ContactSimulationTime >
                    receive.ContactSimulationTime);
            }

            private bool IsCommittedReceive(PlayerBallContactEvent contact) =>
                contact.Candidate.Actor.HasValue &&
                Receipts.Any(receipt =>
                    receipt.Execution != null &&
                    receipt.Execution.ContactGroupId ==
                    contact.Hit.ContactGroupId &&
                    receipt.Actor.Equals(StableId(contact.Candidate.Actor.Value)) &&
                    (receipt.Kind == AttackDefenseCommandKind.FloorDefense ||
                     receipt.Kind == AttackDefenseCommandKind.AttackCover));

            private StablePlayerId StableId(RuntimePlayerId player) => _stableIds[player];

            private void RecordPhysicalContact(PlayerBallContactEvent contact)
            {
                PhysicalContacts.Add(contact);
                if (contact.Candidate.Action == TechniqueAction.Block)
                {
                    _blockThisRally = true;
                    _postBlockEvidenceThisRally = false;
                    _postBlockReceivesThisRally = 0;
                    return;
                }

                if (contact.Candidate.Action != TechniqueAction.Receive)
                {
                    return;
                }

                if (IsCommittedReceive(contact))
                {
                    _committedReceivesThisRally++;
                }

                if (_blockThisRally)
                {
                    _postBlockReceivesThisRally++;
                }
            }

            private void RecordDefenseAttempt(ReplayDefenseAttemptEvent attempt)
            {
                DefenseAttempts.Add(attempt);
                var isMissEvidence =
                    attempt.Kind == "DefenseContactRejected" ||
                    attempt.Kind == "DefenseAttemptExpired";
                if (!isMissEvidence)
                {
                    return;
                }

                if (attempt.Receipt.Kind == AttackDefenseCommandKind.FloorDefense)
                {
                    _missedFloorDefenseEvidenceThisRally = true;
                }

                if (_blockThisRally)
                {
                    _postBlockEvidenceThisRally = true;
                }
            }

            private void RecordGround(ReplaySimpleEvent ground)
            {
                GroundEvents.Add(ground);
                _groundsThisRally++;
            }

            private void RecordRallyResult(ReplayRallyResolvedEvent result)
            {
                Results.Add(result);
                if (!ObservedMissedFloorDefense &&
                    _missedFloorDefenseEvidenceThisRally)
                {
                    ObservedMissedFloorDefense = true;
                    MissedFloorDefenseHadEvidence = true;
                    MissedFloorDefenseReceiveCount =
                        _committedReceivesThisRally;
                    MissedFloorDefenseGroundCount = _groundsThisRally;
                    MissedFloorDefenseResultCount = 1;
                }

                if (!ObservedPostBlockMiss &&
                    _blockThisRally &&
                    _postBlockEvidenceThisRally &&
                    _postBlockReceivesThisRally == 0)
                {
                    ObservedPostBlockMiss = true;
                    PostBlockMissHadEvidence = true;
                    PostBlockMissReceiveCount = _postBlockReceivesThisRally;
                    PostBlockMissGroundCount = _groundsThisRally;
                    PostBlockMissResultCount = 1;
                }

                _missedFloorDefenseEvidenceThisRally = false;
                _postBlockEvidenceThisRally = false;
                _blockThisRally = false;
                _committedReceivesThisRally = 0;
                _postBlockReceivesThisRally = 0;
                _groundsThisRally = 0;
            }
        }
    }
}

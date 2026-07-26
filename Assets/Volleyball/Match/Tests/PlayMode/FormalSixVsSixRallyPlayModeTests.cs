using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using BitConverter = System.BitConverter;
using MatchContextV4 = Volleyball.Shared.Contracts.MatchContextV4;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixRallyPlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FormalReceiveAndOrganization_UseOnePlanAuthorityWriter()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalIndoor6v6",
                LoadSceneMode.Single);
            var director =
                Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            Assert.That(director, Is.Not.Null);
            var traces = new List<ReceiveOrganizationAuthorityReceipt>();
            var acceptedActions = new List<TechniqueAction>();
            director.ReceiveOrganizationAuthorityCommitted += traces.Add;
            director.ReplayContactAccepted += replayEvent =>
                acceptedActions.Add(replayEvent.Action);

            var timeout = Time.realtimeSinceStartup + 90f;
            while (!acceptedActions.Contains(TechniqueAction.Attack) &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            var primary = traces.First(trace =>
                trace.Kind ==
                ReceiveOrganizationCommandKind.PrimaryReceive);
            var organization = traces.First(trace =>
                trace.Kind ==
                ReceiveOrganizationCommandKind.OrganizationContact);
            var attackPreparation = traces.First(trace =>
                trace.Kind ==
                ReceiveOrganizationCommandKind.AttackPreparation &&
                trace.SourceSequence == organization.SourceSequence);
            Assert.That(director.GateHAuthorityEnabled, Is.True);
            Assert.That(primary.Actor, Is.EqualTo(primary.Evidence.Plan.PrimaryReceiver));
            Assert.That(
                primary.ExecutionClassification,
                Is.Not.Null);
            Assert.That(primary.TrajectoryArtifact, Is.Not.Null);
            Assert.That(
                organization.Actor.Equals(
                    organization.Evidence.Plan.RegisteredSetter) ||
                organization.Evidence.Plan.BackupOrganizers.Contains(
                    organization.Actor),
                Is.True);
            Assert.That(
                organization.Evidence.Phase,
                Is.EqualTo(
                    ReceiveOrganizationAuthorityPhaseV3.OrganizationPlanned));
            Assert.That(
                attackPreparation.Actor,
                Is.Not.EqualTo(organization.Actor));
            Assert.That(
                traces.Select(trace => trace.PlanRevision),
                Is.Ordered.Ascending);
            Assert.That(
                traces.GroupBy(trace => string.Join(
                        ":",
                        trace.PlanRevision,
                        trace.SourceSequence,
                        trace.Kind,
                        trace.Actor.Value))
                    .All(group => group.Count() == 1),
                Is.True);
            Assert.That(
                acceptedActions.Take(3),
                Is.EqualTo(new[]
                {
                    TechniqueAction.Receive,
                    TechniqueAction.Set,
                    TechniqueAction.Attack
                }));
            Assert.That(director.SuccessfulContacts, Is.GreaterThanOrEqualTo(3));
            Assert.That(director.V3RuleTransitions, Is.GreaterThanOrEqualTo(3));
            Assert.That(director.GateHLegacyWriterInvocations, Is.Zero);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Formal6v6_ReplayDiagnosticRecordingPreservesFixedSeedAuthority()
        {
            FormalAuthoritySummary baseline = null;
            FormalAuthoritySummary recorded = null;

            yield return RunFixedSeedFormalFixture(
                recordDiagnostics: false,
                value => baseline = value);
            yield return RunFixedSeedFormalFixture(
                recordDiagnostics: true,
                value => recorded = value);

            Assert.That(baseline, Is.Not.Null);
            Assert.That(baseline.HasDiagnosticReplay, Is.False);
            Assert.That(baseline.DiagnosticRecordCount, Is.Zero);
            Assert.That(recorded, Is.Not.Null);
            Assert.That(recorded.HasDiagnosticReplay, Is.True);
            Assert.That(recorded.DiagnosticRecordCount, Is.EqualTo(recorded.AcceptedContacts));
            Assert.That(recorded.WinnerTeamId, Is.EqualTo(baseline.WinnerTeamId));
            Assert.That(recorded.HomeScore, Is.EqualTo(baseline.HomeScore));
            Assert.That(recorded.AwayScore, Is.EqualTo(baseline.AwayScore));
            Assert.That(recorded.AcceptedContacts, Is.EqualTo(baseline.AcceptedContacts));
            Assert.That(recorded.V3TransitionCount, Is.EqualTo(baseline.V3TransitionCount));
            CollectionAssert.AreEqual(baseline.V3ReasonCodes, recorded.V3ReasonCodes);
            CollectionAssert.AreEqual(baseline.AcceptedBallStateVersions, recorded.AcceptedBallStateVersions);
            CollectionAssert.AreEqual(
                baseline.AcceptedAuthorityFingerprints,
                recorded.AcceptedAuthorityFingerprints);

            Debug.Log(
                "[Formal6v6AuthorityInvariance] " +
                "winner=" + baseline.WinnerTeamId +
                ";score=" + baseline.HomeScore + "-" + baseline.AwayScore +
                ";contacts=" + baseline.AcceptedContacts +
                ";v3Transitions=" + baseline.V3TransitionCount +
                ";reasons=" + string.Join(",", baseline.V3ReasonCodes) +
                ";ballVersions=" + string.Join(",", baseline.AcceptedBallStateVersions) +
                ";authorityFingerprints=" +
                string.Join(",", baseline.AcceptedAuthorityFingerprints));
        }

        [UnityTest]
        public IEnumerator FormalScene_CompletesTwentyFivePointSetWithTwelvePlayers()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var cameras = Object.FindFirstObjectByType<RallyCameraController>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            Assert.That(director.PlayerCount, Is.EqualTo(12));
            Assert.That(director.RosterSize, Is.EqualTo(6));
            Assert.That(director.TargetScore, Is.EqualTo(25));
            Assert.That(director.CourtHalfLength, Is.EqualTo(9f));
            Assert.That(director.MatchContext, Is.Not.Null);
            Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Authority));
            Assert.That(players, Has.Some.Matches<PrototypePlayerAgent>(
                player => player.Id.Role == PlayerRole.MiddleBlocker &&
                          player.Ability.PlannedAttackContactHeightMeters > 3.4f));
            AssertRoster(players, director);

            var resolvedRallies = 0;
            var replayedContacts = 0;
            director.ReplayRallyResolved += _ => resolvedRallies++;
            director.ReplayContactAccepted += _ => replayedContacts++;
            var initialServer = director.CurrentServer;
            var originalTimeScale = Time.timeScale;
            var aiSource = new ImmediateWeightSource();
            director.ConfigureAiDecisionSource(
                aiSource,
                realTimeTimeoutSeconds: 0.5f,
                restoreDurationSeconds: 0.04f);
            Assert.That(
                Object.FindObjectsByType<AiDecisionTimeController>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            var timeout = Time.realtimeSinceStartup + 300f;
            var sawOutsideOwnCourt = false;
            var minimumSameTeamSeparation = float.PositiveInfinity;
            var awaitingFirstPostRotationRally = false;
            var verifiedPostRotationV3Eligibility = false;
            while (director.Result == null && Time.realtimeSinceStartup < timeout)
            {
                foreach (var player in players)
                {
                    sawOutsideOwnCourt |= !player.IsWithinOwnCourt;
                }

                minimumSameTeamSeparation = Mathf.Min(
                    minimumSameTeamSeparation,
                    MinimumSameTeamSeparation(players));
                if (!verifiedPostRotationV3Eligibility &&
                    director.HomeRotationOffset + director.AwayRotationOffset > 0 &&
                    !director.IsLoopRunning)
                {
                    awaitingFirstPostRotationRally = true;
                }
                if (awaitingFirstPostRotationRally && director.IsLoopRunning)
                {
                    AssertV3EligibilityMatchesLiveRotation(director, players);
                    verifiedPostRotationV3Eligibility = true;
                    awaitingFirstPostRotationRally = false;
                }
                yield return null;
            }

            Assert.That(director.Result, Is.Not.Null, "Formal 6v6 set did not complete in real time.");
            Assert.That(Mathf.Max(director.Result.HomeScore, director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(25));
            Assert.That(Mathf.Abs(director.Result.HomeScore - director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(director.Result.PlayerStats, Has.Count.EqualTo(12));
            Assert.DoesNotThrow(() => director.Result.ValidateAgainst(director.MatchContext));
            Assert.That(director.MatchContext.ContractVersion, Is.EqualTo(4));
            Assert.That(director.MatchContext.RulesVersion, Is.EqualTo(3));
            Assert.That(director.Result.ContractVersion, Is.EqualTo(4));
            Assert.That(director.Result.ContextHash, Is.EqualTo(director.MatchContext.ContextHash));
            Assert.That(director.Result.ResultHash, Is.Not.Null.And.Length.EqualTo(64));
            Assert.That(director.Result.AcceptedContacts, Is.EqualTo(director.SuccessfulContacts));
            Assert.That(director.Result.V3RuleTransitionCount, Is.EqualTo(director.V3RuleTransitions));
            Assert.That(director.V3RuleTransitions, Is.GreaterThan(0));
            Assert.That(director.V3RuleTransitions, Is.EqualTo(director.SuccessfulContacts));
            Assert.That(director.V3RuleParityMatches, Is.EqualTo(director.V3RuleTransitions));
            Assert.That(director.V3RuleIntentionalCorrections, Is.Zero);
            Assert.That(director.V3RuleUnexpectedMismatches, Is.Zero);
            Assert.That(
                director.V3RuleParityMatches +
                director.V3RuleIntentionalCorrections +
                director.V3RuleUnexpectedMismatches,
                Is.EqualTo(director.V3RuleTransitions));
            Assert.That(director.LastV3RuleDiagnostic, Is.Not.Empty);
            Assert.That(replayedContacts, Is.EqualTo(director.SuccessfulContacts));
            Assert.That(
                resolvedRallies,
                Is.EqualTo(director.Result.HomeScore + director.Result.AwayScore),
                "Each completed rally must advance the score exactly once.");
            Assert.That(
                director.Result.PlayerStats.Sum(stat => stat.Contacts),
                Is.EqualTo(
                    director.SuccessfulContacts +
                    director.Result.HomeScore +
                    director.Result.AwayScore));
            Assert.That(director.IsLoopRunning, Is.False);
            Assert.That(director.GroundResolvedRallies, Is.GreaterThan(0));
            Assert.That(director.ScheduledMultiBlockUnits, Is.GreaterThan(0));
            Assert.That(director.ScheduledBackRowBlockers, Is.Zero);
            Assert.That(director.BlueAttackContacts, Is.GreaterThan(0));
            Assert.That(director.OrangeAttackContacts, Is.GreaterThan(0));
            Assert.That(director.HomeRotationOffset + director.AwayRotationOffset,
                Is.GreaterThan(0));
            Assert.That(
                verifiedPostRotationV3Eligibility,
                Is.True,
                "No post-side-out rally exposed refreshed V3 eligibility.");
            Assert.That(director.CurrentServer, Is.Not.EqualTo(initialServer));
            Assert.That(aiSource.RequestCount, Is.EqualTo(director.AiDecisionRequests));
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale).Within(0.001f));
            Assert.That(sawOutsideOwnCourt, Is.False);
            Assert.That(minimumSameTeamSeparation, Is.GreaterThan(0.08f));
            Assert.That(ball.Diagnostics.NonFiniteStates, Is.Zero);

            cameras.SetView(RallyCameraView.Tactical);
            yield return null;
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(Camera.main.orthographicSize, Is.GreaterThanOrEqualTo(12f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AuthorityBlockRebound_EitherSideReceivesThreeFreshCountedContacts(
            bool retainedByBlockingSide)
        {
            var adapter = CreateAuthorityAdapter(
                out var set,
                out var homeRotation,
                out var awayRotation);
            var blockingActor = homeRotation[1];
            var block = adapter.CommitContact(
                blockingActor,
                TeamSide.Home,
                RallyContactClassificationV3.BlockContact,
                901);
            var receivingRotation = retainedByBlockingSide ? homeRotation : awayRotation;
            var receivingSide = retainedByBlockingSide ? TeamSide.Home : TeamSide.Away;
            var countedActors = retainedByBlockingSide
                ? new[] { blockingActor, receivingRotation[2], receivingRotation[3] }
                : new[] { receivingRotation[0], receivingRotation[1], receivingRotation[2] };

            Assert.That(block.Accepted, Is.True);
            for (var index = 0; index < countedActors.Length; index++)
            {
                var transition = adapter.CommitContact(
                    countedActors[index],
                    receivingSide,
                    RallyContactClassificationV3.TeamContact,
                    902 + index);
                Assert.That(transition.Accepted, Is.True, $"counted contact {index + 1}");
                Assert.That(transition.After.CountedHits, Is.EqualTo(index + 1));
            }

            var fourth = adapter.EvaluateContact(
                receivingRotation[4],
                receivingSide,
                RallyContactClassificationV3.TeamContact,
                905);
            Assert.That(
                fourth.RejectionReason,
                Is.EqualTo(RuleRejectionReasonV3.FourthCountedContact));
            Assert.That(set, Is.Not.Null);
        }

        [Test]
        public void AuthorityFourthCountedContact_FaultsBeforePhysicalVelocityResponse()
        {
            var adapter = CreateAuthorityAdapter(
                out _,
                out var homeRotation,
                out _);
            for (var index = 0; index < 3; index++)
            {
                Assert.That(
                    adapter.CommitContact(
                        homeRotation[index],
                        TeamSide.Home,
                        RallyContactClassificationV3.TeamContact,
                        911 + index).Accepted,
                    Is.True);
            }

            var gameObject = new GameObject("formal-authority-fourth-contact");
            try
            {
                gameObject.transform.position = new Vector3(0f, 1.3f, 0f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                ball.RegisterContactSource(new FourthContactSource());
                ball.ContactCandidateResolver = (_, hit, __) => Map(
                    adapter.EvaluateContact(
                        homeRotation[3],
                        TeamSide.Home,
                        RallyContactClassificationV3.TeamContact,
                        hit.ContactGroupId));
                ball.SelectedContactCommitter = (_, hit, __) => Map(
                    adapter.CommitContact(
                        homeRotation[3],
                        TeamSide.Home,
                        RallyContactClassificationV3.TeamContact,
                        hit.ContactGroupId));
                PlayerContactRejectedEvent rejected = default;
                var acceptedContacts = 0;
                ball.PlayerContactRejected += value => rejected = value;
                ball.PlayerContact += _ => acceptedContacts++;
                ball.Launch(new Vector3(0f, -40f, 0f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(rejected.Reason, Is.EqualTo("FourthCountedContact"));
                Assert.That(acceptedContacts, Is.Zero);
                Assert.That(ball.State.LastContactGroupId, Is.Null);
                Assert.That(ball.State.Velocity.Y, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void AssertRoster(
            PrototypePlayerAgent[] players,
            FormalSixVsSixRallyDirector director)
        {
            var stableIds = new HashSet<string>();
            var blue = 0;
            var orange = 0;
            var frontBlue = 0;
            var frontOrange = 0;
            var roles = new HashSet<PlayerRole>();
            foreach (var player in players)
            {
                Assert.That(stableIds.Add(player.StableId.Value), Is.True);
                roles.Add(player.Id.Role);
                if (player.Id.Team == TeamId.Blue)
                {
                    blue++;
                    frontBlue += director.IsFrontRow(player.Id) ? 1 : 0;
                }
                else
                {
                    orange++;
                    frontOrange += director.IsFrontRow(player.Id) ? 1 : 0;
                }
            }

            Assert.That(blue, Is.EqualTo(6));
            Assert.That(orange, Is.EqualTo(6));
            Assert.That(frontBlue, Is.EqualTo(3));
            Assert.That(frontOrange, Is.EqualTo(3));
            Assert.That(roles, Does.Contain(PlayerRole.Setter));
            Assert.That(roles, Does.Contain(PlayerRole.OutsideHitter));
            Assert.That(roles, Does.Contain(PlayerRole.Opposite));
            Assert.That(roles, Does.Contain(PlayerRole.MiddleBlocker));
            Assert.That(roles, Does.Contain(PlayerRole.Defender));
        }

        private static float MinimumSameTeamSeparation(PrototypePlayerAgent[] players)
        {
            var minimum = float.PositiveInfinity;
            for (var first = 0; first < players.Length; first++)
            {
                for (var second = first + 1; second < players.Length; second++)
                {
                    if (players[first].Id.Team != players[second].Id.Team)
                    {
                        continue;
                    }

                    minimum = Mathf.Min(
                        minimum,
                        Vector3.Distance(
                            players[first].transform.position,
                            players[second].transform.position));
                }
            }

            return minimum;
        }

        private static void AssertV3EligibilityMatchesLiveRotation(
            FormalSixVsSixRallyDirector director,
            PrototypePlayerAgent[] players)
        {
            var adapter = GetPrivateField<FullRallyV3RulesRuntimeAdapter>(
                director,
                "_v3RulesAdapter");
            var eligibility = GetPrivateField<OnCourtEligibilitySnapshot>(
                adapter,
                "_eligibility");
            var set = GetPrivateField<MatchSet>(director, "_set");

            Assert.That(adapter, Is.Not.Null);
            Assert.That(eligibility.Players, Has.Count.EqualTo(12));
            foreach (var player in players)
            {
                Assert.That(
                    eligibility.For(player.StableId).RotationPosition,
                    Is.EqualTo(director.RotationPositionFor(player.Id)),
                    player.StableId.Value);
            }

            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                var currentServers = eligibility.Players
                    .Where(player => player.Side == side && player.IsCurrentServer)
                    .ToArray();
                Assert.That(currentServers, Has.Length.EqualTo(1), side.ToString());
                Assert.That(currentServers[0].PlayerId, Is.EqualTo(set.ServerFor(side)));
            }

            Assert.That(
                director.CurrentServer,
                Is.EqualTo(set.ServerFor(set.ServingSide)));
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(target);
                }
            }

            Assert.Fail($"Missing private field {fieldName}.");
            return default;
        }

        private static FullRallyV3RulesRuntimeAdapter CreateAuthorityAdapter(
            out MatchSet set,
            out StablePlayerId[] homeRotation,
            out StablePlayerId[] awayRotation)
        {
            var createContext = typeof(FormalSixVsSixRallyBootstrap).GetMethod(
                "CreateSandboxContext",
                BindingFlags.Static | BindingFlags.NonPublic);
            var context = (MatchContextV4)createContext.Invoke(null, null);
            set = new MatchSet(context, TeamSide.Home);
            homeRotation = RotationFor(set, TeamSide.Home);
            awayRotation = RotationFor(set, TeamSide.Away);
            var eligibility = OnCourtLineupRulesV3.Create(
                context,
                homeRotation,
                awayRotation,
                set.ServerFor(TeamSide.Home),
                set.ServerFor(TeamSide.Away),
                System.Array.Empty<LiberoReplacementV3>());
            return new FullRallyV3RulesRuntimeAdapter(
                context.RulesVersion,
                eligibility,
                TeamSide.Home,
                V3RulesMode.Authority);
        }

        private static StablePlayerId[] RotationFor(MatchSet set, TeamSide side)
        {
            var rotation = new StablePlayerId[6];
            for (var position = 1; position <= rotation.Length; position++)
            {
                rotation[position - 1] = set.PlayerAtRotationPosition(side, position);
            }

            return rotation;
        }

        private static BallContactResolution Map(RuleTransitionV3 transition)
        {
            if (transition.Accepted)
            {
                return BallContactResolution.Accept();
            }

            return transition.RejectionReason == RuleRejectionReasonV3.DuplicateContactGroup ||
                   transition.RejectionReason == RuleRejectionReasonV3.RallyClosed
                ? BallContactResolution.Ignore()
                : BallContactResolution.Fault(transition.RejectionReason.ToString());
        }

        private static IEnumerator RunFixedSeedFormalFixture(
            bool recordDiagnostics,
            System.Action<FormalAuthoritySummary> completed)
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var previous = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var score = Object.FindFirstObjectByType<ScoreDisplay>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
            Assert.That(previous, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(score, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));

            var context = previous.MatchContext;
            var host = previous.gameObject;
            Object.Destroy(previous);
            yield return null;
            foreach (var player in players)
            {
                player.CancelScheduledContact();
            }

            var director = host.AddComponent<FormalSixVsSixRallyDirector>();
            director.InitializeV4(
                ball,
                players,
                context,
                score,
                configuration: PhysicalMatchConfiguration.CreateCalibration(
                    PhysicalMatchConfiguration.FormalIndoorSixVsSix,
                    targetScore: 1,
                    minimumLead: 1));
            director.ConfigureV3Rules(V3RulesMode.Authority);

            var v3ReasonCodes = new List<string>();
            var acceptedBallStateVersions = new List<long>();
            var acceptedAuthorityFingerprints = new List<string>();
            director.ReplayContactAccepted += replayEvent =>
            {
                Assert.That(replayEvent.RuleTransition, Is.Not.Null);
                Assert.That(replayEvent.TrajectoryArtifact, Is.Not.Null);
                Assert.That(replayEvent.PlayerId, Is.Not.Null);
                Assert.That(replayEvent.ExecutionClassification, Is.Not.Null);
                v3ReasonCodes.Add(replayEvent.RuleTransition.RejectionReason.ToString());
                acceptedBallStateVersions.Add(
                    replayEvent.TrajectoryArtifact.Key.BallStateVersion);
                acceptedAuthorityFingerprints.Add(
                    AcceptedAuthorityFingerprint(replayEvent));
            };

            MatchReplayRecorder recorder = null;
            if (recordDiagnostics)
            {
                recorder = MatchReplayRecorder.Attach(director, ball, players);
                recorder.StartCapture();
            }

            var originalTimeScale = Time.timeScale;
            try
            {
                var timeout = Time.realtimeSinceStartup + 75f;
                while (director.Result == null && Time.realtimeSinceStartup < timeout)
                {
                    Time.timeScale = 12f;
                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }

            Assert.That(director.Result, Is.Not.Null, "Fixed-seed formal fixture timed out.");
            Assert.That(v3ReasonCodes, Has.Count.EqualTo(director.SuccessfulContacts));
            Assert.That(acceptedBallStateVersions, Has.Count.EqualTo(director.SuccessfulContacts));
            Assert.That(
                acceptedAuthorityFingerprints,
                Has.Count.EqualTo(director.SuccessfulContacts));
            if (recorder != null)
            {
                Assert.That(recorder.IsComplete, Is.True);
            }

            completed(new FormalAuthoritySummary(
                director.Result.WinnerTeamId.Value,
                director.Result.HomeScore,
                director.Result.AwayScore,
                director.SuccessfulContacts,
                director.V3RuleTransitions,
                v3ReasonCodes,
                acceptedBallStateVersions,
                acceptedAuthorityFingerprints,
                recorder != null,
                recorder == null ? 0 : recorder.Complete().Events.Count));
        }

        private static string AcceptedAuthorityFingerprint(ReplayContactEvent replayEvent)
        {
            var classification = replayEvent.ExecutionClassification;
            var testedEnvelope = classification.TestedEnvelope;
            var executableEnvelope = classification.ExecutableEnvelope;
            var sample = classification.ExecutableSample;
            Assert.That(testedEnvelope, Is.Not.Null);
            Assert.That(executableEnvelope, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            return string.Join(
                "|",
                replayEvent.PlayerId.Value.Value,
                replayEvent.Action.ToString(),
                classification.Kind.ToString(),
                testedEnvelope.Identity,
                executableEnvelope.Identity,
                sample.EnvelopeIdentity,
                sample.SamplingKey,
                sample.CandidateCategory.ToString(),
                testedEnvelope.SourceIntentIdentity,
                testedEnvelope.CandidateCategory.ToString(),
                Bits(sample.Target.X),
                Bits(sample.Target.Y),
                Bits(sample.Target.Z),
                Bits(sample.Velocity.X),
                Bits(sample.Velocity.Y),
                Bits(sample.Velocity.Z),
                Bits(sample.Effort));
        }

        private static string Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("x8");
        }

        private sealed class FormalAuthoritySummary
        {
            public FormalAuthoritySummary(
                string winnerTeamId,
                int homeScore,
                int awayScore,
                int acceptedContacts,
                int v3TransitionCount,
                IReadOnlyList<string> v3ReasonCodes,
                IReadOnlyList<long> acceptedBallStateVersions,
                IReadOnlyList<string> acceptedAuthorityFingerprints,
                bool hasDiagnosticReplay,
                int diagnosticRecordCount)
            {
                WinnerTeamId = winnerTeamId;
                HomeScore = homeScore;
                AwayScore = awayScore;
                AcceptedContacts = acceptedContacts;
                V3TransitionCount = v3TransitionCount;
                V3ReasonCodes = v3ReasonCodes;
                AcceptedBallStateVersions = acceptedBallStateVersions;
                AcceptedAuthorityFingerprints = acceptedAuthorityFingerprints;
                HasDiagnosticReplay = hasDiagnosticReplay;
                DiagnosticRecordCount = diagnosticRecordCount;
            }

            public string WinnerTeamId { get; }
            public int HomeScore { get; }
            public int AwayScore { get; }
            public int AcceptedContacts { get; }
            public int V3TransitionCount { get; }
            public IReadOnlyList<string> V3ReasonCodes { get; }
            public IReadOnlyList<long> AcceptedBallStateVersions { get; }
            public IReadOnlyList<string> AcceptedAuthorityFingerprints { get; }
            public bool HasDiagnosticReplay { get; }
            public int DiagnosticRecordCount { get; }
        }

        private sealed class FourthContactSource : IBallContactSource
        {
            public void CollectContacts(
                float simulationTime,
                float deltaSeconds,
                ICollection<BallContactCandidate> contacts)
            {
                var frame = new ContactSurfaceFrame(
                    new SimVector3(0f, 1f, 0f),
                    SimVector3.Up,
                    new SimVector3(1f, 0f, 0f),
                    new SimVector3(0f, 0f, 1f),
                    1f,
                    1f);
                contacts.Add(new BallContactCandidate(
                    new ContactSurfaceSnapshot(frame, frame, true, 914),
                    TechniqueAction.Receive,
                    new PlayerId(TeamId.Blue, PlayerRole.OutsideHitter, 4),
                    0.8f,
                    new SimVector3(0f, 8f, 2f),
                    SimVector3.Up,
                    new ContactResponseParameters(0.85f, 1f, 0.1f, 0.08f)));
            }
        }

        private sealed class ImmediateWeightSource : IRallyTacticalWeightSource
        {
            public int RequestCount { get; private set; }

            public Task<RallyTacticalWeightProposal> RequestAsync(
                RallyTacticalWeightRequest request,
                CancellationToken cancellationToken)
            {
                RequestCount++;
                return Task.FromResult(new RallyTacticalWeightProposal(1f, 1.15f, 1f, 1f));
            }
        }
    }
}

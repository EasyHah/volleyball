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
using DominantHandV4 = Volleyball.Shared.Contracts.DominantHandV4;
using MatchAttributeDerivationConfigV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationConfigV4;
using MatchAttributeDerivationV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationV4;
using MatchContextV4 = Volleyball.Shared.Contracts.MatchContextV4;
using PhysicalBaseAttributesV4 = Volleyball.Shared.Contracts.PhysicalBaseAttributesV4;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using TechnicalBaseAttributesV4 = Volleyball.Shared.Contracts.TechnicalBaseAttributesV4;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixRallyPlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Formal6v6_CalibratedToolRecovery_UsesPhysicalBlockAndNonAttackerSave()
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
            var blueFrontRow = new HashSet<StablePlayerId>(players
                .Where(player => player.Id.Team == TeamId.Blue &&
                    previous.IsFrontRow(player.Id))
                .Select(player => player.StableId));
            // Capture the V3 rotation state before replacing the director.
            var orangeBackRow = new HashSet<StablePlayerId>(players
                .Where(player => player.Id.Team == TeamId.Orange &&
                    !previous.IsFrontRow(player.Id))
                .Select(player => player.StableId));
            var recovery = players.Single(player =>
                player.Id.Team == TeamId.Orange &&
                player.Id.Role == PlayerRole.Setter);
            var prepared = players.First(player => player.Id.Team == TeamId.Orange &&
                player.Id.Role == PlayerRole.MiddleBlocker);
            var recoveryProfiles = new HashSet<StablePlayerId>(orangeBackRow)
            {
                recovery.StableId
            };
            var host = previous.gameObject;
            Object.Destroy(previous);
            yield return null;
            foreach (var player in players)
            {
                player.CancelScheduledContact();
                player.SetAbility(CreateCalibratedToolAbility(
                    player.Id.Team == TeamId.Blue &&
                    player.Id.Role == PlayerRole.MiddleBlocker,
                    player.Id.Team == TeamId.Orange && recoveryProfiles.Contains(player.StableId),
                    player == prepared));
            }
            var blockerIndex = 0;
            foreach (var blocker in players.Where(player =>
                         blueFrontRow.Contains(player.StableId)))
            {
                blocker.transform.position = new Vector3(
                    prepared.transform.position.x + ((blockerIndex++ - 1) * .25f),
                    0f, -2.05f);
            }
            var recoveryIndex = 0;
            foreach (var teammate in players.Where(player =>
                         player.Id.Team == TeamId.Orange && player != prepared))
            {
                var offset = recoveryProfiles.Contains(teammate.StableId)
                    ? ((recoveryIndex++ - 1) * .15f)
                    : ((recoveryIndex++ - 2) * .5f);
                teammate.transform.position = new Vector3(prepared.transform.position.x + offset, 0f, 3f);
            }

            var director = host.AddComponent<FormalSixVsSixRallyDirector>();
            director.InitializeV4(ball, players, context, score,
                configuration: PhysicalMatchConfiguration.CreateCalibration(
                    PhysicalMatchConfiguration.FormalIndoorSixVsSix,
                    targetScore: 1, minimumLead: 1));
            director.ConfigureV3Rules(V3RulesMode.Authority);
            // t=0 calibration after the director has installed its formation,
            // before the first simulated frame/contact is advanced.
            blockerIndex = 0;
            foreach (var blocker in players.Where(player => blueFrontRow.Contains(player.StableId)))
                blocker.transform.position = new Vector3(prepared.transform.position.x +
                    ((blockerIndex++ - 1) * .25f), 0f, -2.05f);
            recoveryIndex = 0;
            foreach (var teammate in players.Where(player =>
                         player.Id.Team == TeamId.Orange && player != prepared))
            {
                var offset = recoveryProfiles.Contains(teammate.StableId)
                    ? ((recoveryIndex++ - 1) * .15f)
                    : ((recoveryIndex++ - 2) * .5f);
                teammate.transform.position = new Vector3(prepared.transform.position.x + offset, 0f, 3f);
            }
            Assert.That(players.Where(player => player.Id.Team == TeamId.Orange)
                .Select(player => player.Ability.Derived.Attributes.Attack.PowerCapacity),
                Has.All.LessThan(.45f));
            Assert.That(players.Where(player => player.Id.Team == TeamId.Blue &&
                    player.Id.Role == PlayerRole.MiddleBlocker)
                .Select(player => player.Ability.Derived.Attributes.Block.ReachHeightMeters),
                Has.All.GreaterThanOrEqualTo(2.60f));

            var authority = new List<AttackDefenseAuthorityReceipt>();
            var accepted = new List<ReplayContactEvent>();
            var phases = new List<AttackDefenseAuthorityPhaseV3>();
            ToolRecoveryEvidenceV3 selectedTool = null;
            director.AttackDefenseAuthorityCommitted += receipt =>
            {
                authority.Add(receipt);
                // Once the immutable attack command exists, step the short
                // physical flight one rendered frame at a time for diagnostic
                // observation of the actual net crossing.
                if (receipt.Kind == AttackDefenseCommandKind.AttackContact)
                    Time.timeScale = 1f;
            };
            director.ReplayContactAccepted += contact =>
            {
                accepted.Add(contact);
            };

            var originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            var timeout = Time.realtimeSinceStartup + 75f;
            while (!authority.Any(receipt => receipt.Kind ==
                       AttackDefenseCommandKind.AttackContact) &&
                   director.Result == null &&
                   Time.realtimeSinceStartup < timeout)
            {
                phases.Add(director.GateIAuthorityPhase);
                yield return null;
            }

            var firstAttackCommit = authority.FirstOrDefault(receipt =>
                receipt.Kind == AttackDefenseCommandKind.AttackContact);
            Assert.That(firstAttackCommit, Is.Not.Null,
                "Calibrated fixture ended before Gate I committed an attack.");
            Assert.That(firstAttackCommit.Evidence.Plan.SelectedAction.ActionClass,
                Is.EqualTo(AttackActionClassV3.BlockToolRecovery),
                "Calibrated first Gate I attack must select BlockToolRecovery.");

            selectedTool = firstAttackCommit.Evidence.Plan.SelectedAction.ToolRecoveryEvidence;
            var recoveryTimeout = Time.realtimeSinceStartup + 15f;
            while (!accepted.Any(contact => contact.Action == TechniqueAction.Receive &&
                       contact.AttackDefenseAuthority?.Evidence.Plan?.SelectedAction?.ActionClass ==
                           AttackActionClassV3.BlockToolRecovery &&
                       contact.AttackDefenseAuthority.Phase ==
                           AttackDefenseAuthorityPhaseV3.ReorganizationPlanned) &&
                   director.Result == null && Time.realtimeSinceStartup < recoveryTimeout)
            {
                phases.Add(director.GateIAuthorityPhase);
                yield return null;
            }
            Time.timeScale = originalTimeScale;

            var toolAttack = accepted.SingleOrDefault(contact =>
                contact.Action == TechniqueAction.Attack &&
                contact.AttackDefenseAuthority?.Evidence.Plan?.SelectedAction?.ActionClass ==
                    AttackActionClassV3.BlockToolRecovery);
            Assert.That(toolAttack, Is.Not.Null,
                "RED: the calibrated legal fixture must expose a selected physical tool branch.");
            var tool = toolAttack.AttackDefenseAuthority.Evidence.Plan.SelectedAction.ToolRecoveryEvidence;
            Assert.That(tool, Is.Not.Null);
            CollectionAssert.AreEquivalent(new[] { "Set.SoftTouch" },
                toolAttack.AttackDefenseAuthority.ExecutionClassification
                    .ExecutableEnvelope.AbilityConsumptions
                    .Select(consumption => consumption.AttributeName));
            Assert.That(tool.RecoveryActor, Is.Not.EqualTo(toolAttack.PlayerId));
            Assert.That(authority.Any(receipt => receipt.Kind == AttackDefenseCommandKind.BlockContact &&
                receipt.Actor.Equals(tool.Blocker)), Is.True);

            var physicalBlock = accepted.SingleOrDefault(contact =>
                contact.Action == TechniqueAction.Block &&
                contact.AttackDefenseAuthority?.ToolRecoveryActualObservation != null);
            var physicalRecovery = accepted.SingleOrDefault(contact =>
                contact.Action == TechniqueAction.Receive &&
                contact.AttackDefenseAuthority?.Evidence.Plan?.SelectedAction?.ToolRecoveryEvidence != null &&
                contact.PlayerId.HasValue && contact.PlayerId.Value.Equals(tool.RecoveryActor));
            Assert.That(physicalBlock, Is.Not.Null);
            Assert.That(physicalRecovery, Is.Not.Null);
            Assert.That(physicalBlock.AttackDefenseAuthority.Actor, Is.EqualTo(tool.Blocker));
            Assert.That(physicalBlock.AttackDefenseAuthority.ToolRecoveryActualObservation.ReboundSide,
                Is.EqualTo(TeamSide.Away));
            Assert.That(physicalRecovery.AttackDefenseAuthority.Actor, Is.EqualTo(tool.RecoveryActor));
            Assert.That(physicalRecovery.AttackDefenseAuthority.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
            yield return null;
            Assert.That(director.GateIAuthorityPhase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.Idle));
            Assert.That(phases, Does.Contain(AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock));
            Assert.That(phases, Does.Contain(AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive));
            Assert.That(accepted.Select(contact => contact.AttackDefenseAuthority?.SourceSequence ?? -1)
                .Where(sequence => sequence >= 0), Is.Ordered.Ascending);
            Assert.That(director.GateILegacyWriterInvocations, Is.Zero);
            Assert.That(director.GateHLegacyWriterInvocations, Is.Zero);
            Assert.That(director.AcceptedSetContactWriterCount,
                Is.EqualTo(accepted.Count(contact => contact.Action == TechniqueAction.Set)));
            Assert.That(director.MaximumAppliedMovementCorrection, Is.LessThanOrEqualTo(.70f));
            Assert.That(players.All(player => player.IsWithinOwnCourt), Is.True);
        }

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
            Assert.That(traces.Any(trace => trace.Kind ==
                ReceiveOrganizationCommandKind.AttackPreparation &&
                trace.SourceSequence == organization.SourceSequence), Is.False);
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
        [Timeout(120000)]
        public IEnumerator FormalAttackDefense_UsesOneAuthorityWriter()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var traces = new List<AttackDefenseAuthorityReceipt>();
            var intents = new List<GateISetIntentReceiptV3>();
            var accepted = new List<ReplayContactEvent>();
            director.AttackDefenseAuthorityCommitted += traces.Add;
            director.GateISetIntentCommitted += intents.Add;
            director.ReplayContactAccepted += accepted.Add;
            var timeout = Time.realtimeSinceStartup + 90f;
            while (!traces.Any(trace => trace.Kind == AttackDefenseCommandKind.AttackContact) &&
                   Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(director.GateIAuthorityEnabled, Is.True);
            Assert.That(director.GateILegacyWriterInvocations, Is.Zero);
            var acceptedSets = accepted.Count(contact =>
                contact.Action == TechniqueAction.Set);
            Assert.That(intents, Is.Not.Empty);
            Assert.That(acceptedSets, Is.GreaterThanOrEqualTo(1));
            Assert.That(intents.Count, Is.GreaterThanOrEqualTo(acceptedSets));
            Assert.That(director.AcceptedSetContactWriterCount,
                Is.EqualTo(acceptedSets));
            Assert.That(traces, Has.Some.Property("Kind").EqualTo(AttackDefenseCommandKind.AttackContact));
            Assert.That(traces.Select(trace => trace.PlanRevision), Is.Ordered.Ascending);
            Assert.That(intents.Select(intent => intent.SourceSequence), Is.Ordered.Ascending);
            Assert.That(intents.Select(intent => intent.EvidenceIdentity)
                    .Distinct().Count(),
                Is.EqualTo(intents.Count));
            Assert.That(traces.Select(trace => trace.SourceSequence), Is.Ordered.Ascending);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Formal6v6_GateJAuthorityIsRecorderInvariant()
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
            CollectionAssert.AreEqual(
                baseline.GateIAuthorityFingerprints,
                recorded.GateIAuthorityFingerprints);
            CollectionAssert.AreEqual(
                baseline.GateJPerceptionFingerprints,
                recorded.GateJPerceptionFingerprints);
            Assert.That(recorded.GateIAuthorityFingerprints, Is.Not.Empty);
            Assert.That(recorded.GateJPerceptionFingerprints, Is.Not.Empty);
            Assert.That(recorded.GateILegacyWriterInvocations, Is.Zero);
            Assert.That(baseline.GateILegacyWriterInvocations, Is.Zero);
            Assert.That(recorded.GateHLegacyWriterInvocations, Is.Zero);
            Assert.That(baseline.GateHLegacyWriterInvocations, Is.Zero);
            Assert.That(recorded.MaximumAppliedMovementCorrection,
                Is.LessThanOrEqualTo(0.70f));
            Assert.That(baseline.MaximumAppliedMovementCorrection,
                Is.LessThanOrEqualTo(0.70f));

            Debug.Log(
                "[Formal6v6AuthorityInvariance] " +
                "winner=" + baseline.WinnerTeamId +
                ";score=" + baseline.HomeScore + "-" + baseline.AwayScore +
                ";contacts=" + baseline.AcceptedContacts +
                ";v3Transitions=" + baseline.V3TransitionCount +
                ";reasons=" + string.Join(",", baseline.V3ReasonCodes) +
                ";ballVersions=" + string.Join(",", baseline.AcceptedBallStateVersions) +
                ";authorityFingerprints=" +
                string.Join(",", baseline.AcceptedAuthorityFingerprints) +
                ";gateIFingerprints=" +
                string.Join(",", baseline.GateIAuthorityFingerprints));
        }

        [UnityTest]
        public IEnumerator Formal6v6_GateJLowAndHighAwarenessKeepRuleAuthority()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalIndoor6v6", LoadSceneMode.Single);
            var director =
                Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            var home = players.Where(player =>
                    player.Id.Team == TeamId.Blue)
                .OrderBy(player => player.StableId.Value)
                .ToArray();
            var create = typeof(PhysicalMatchRallyDirector).GetMethod(
                "CreateGateJPerceptionReceipt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(director.GateJPerceptionEnabled, Is.True);
            Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Authority));

            foreach (var player in home)
                player.SetAbility(CreateAwarenessAbility(0f));
            var low = (PerceptionReceiptV3)create.Invoke(director,
                new object[] { TeamId.Blue, 7L, 9L, "shared-artifact",
                    null, home.Select(player => player.StableId).ToArray(),
                    home[0].StableId,
                    false });

            foreach (var player in home)
                player.SetAbility(CreateAwarenessAbility(1f));
            var high = (PerceptionReceiptV3)create.Invoke(director,
                new object[] { TeamId.Blue, 7L, 9L, "shared-artifact",
                    null, home.Select(player => player.StableId).ToArray(),
                    home[0].StableId,
                    false });

            Assert.That(high.RecognitionDelaySeconds,
                Is.LessThan(low.RecognitionDelaySeconds));
            Assert.That(high.ObservedBall.Uncertainty,
                Is.LessThan(low.ObservedBall.Uncertainty));
            Assert.That(high.AuthoritativeArtifactIdentity,
                Is.EqualTo(low.AuthoritativeArtifactIdentity));
            Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Authority));
            Assert.That(director.GateHLegacyWriterInvocations, Is.Zero);
            Assert.That(director.GateILegacyWriterInvocations, Is.Zero);
        }

        [UnityTest]
        [Timeout(360000)]
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
            var acceptedContactGroups = new List<int>();
            director.ReplayRallyResolved += _ => resolvedRallies++;
            director.ReplayContactAccepted += _ => replayedContacts++;
            ball.PlayerContact += contact => acceptedContactGroups.Add(
                contact.Hit.ContactGroupId);
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

            // Gate I deliberately permits longer multi-contact rallies than the
            // Gate H baseline. Keep this real-time lifecycle test unaccelerated,
            // but leave enough headroom for a legal 25-point fixed-seed set.
            var timeout = Time.realtimeSinceStartup + 360f;
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
            // Gate I block-first continuation is a documented V3 correction;
            // the accounting invariant below includes it while rejecting any
            // unexpected parity divergence.
            Assert.That(director.V3RuleUnexpectedMismatches, Is.Zero,
                director.LastV3RuleDiagnostic);
            Assert.That(
                director.V3RuleParityMatches +
                director.V3RuleIntentionalCorrections +
                director.V3RuleUnexpectedMismatches,
                Is.EqualTo(director.V3RuleTransitions));
            Assert.That(director.LastV3RuleDiagnostic, Is.Not.Empty);
            Assert.That(replayedContacts, Is.EqualTo(director.SuccessfulContacts));
            Assert.That(acceptedContactGroups.Distinct().Count(),
                Is.EqualTo(acceptedContactGroups.Count),
                "A physical command contact group may be accepted only once.");
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
            var gateIAuthorityFingerprints = new List<string>();
            var gateJPerceptionFingerprints = new List<string>();
            var maximumAcceptedContactCorrection = 0f;
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
                if (replayEvent.GateISetIntentAuthority != null)
                {
                    gateIAuthorityFingerprints.Add(
                        "set:" + replayEvent.GateISetIntentAuthority.PlanRevision + ":" +
                        replayEvent.GateISetIntentAuthority.SourceSequence + ":" +
                        replayEvent.GateISetIntentAuthority.EvidenceIdentity);
                }
                if (replayEvent.AttackDefenseAuthority != null)
                {
                    var receipt = replayEvent.AttackDefenseAuthority;
                    gateIAuthorityFingerprints.Add(
                        receipt.Kind + ":" + receipt.PlanRevision + ":" +
                        receipt.SourceSequence + ":" + receipt.Actor.Value + ":" +
                        receipt.Evidence.CoverageDecision.Kind);
                }
                var perception = replayEvent.OrganizationAuthority?.Perception ??
                                 replayEvent.AttackDefenseAuthority?.Perception ??
                                 replayEvent.AttackDefenseAuthority?.Evidence
                                     ?.Perception;
                if (perception != null)
                {
                    gateJPerceptionFingerprints.Add(
                        perception.ViewIdentity + ":" +
                        perception.Revision + ":" +
                        perception.SourceSequence + ":" +
                        perception.AuthoritativeArtifactIdentity + ":" +
                        perception.SupportDecision.SelectedPlayer.Value);
                }
                var player = players.Single(value =>
                    replayEvent.PlayerId.HasValue &&
                    value.StableId.Equals(replayEvent.PlayerId.Value));
                maximumAcceptedContactCorrection = Mathf.Max(
                    maximumAcceptedContactCorrection,
                    player.MaximumAppliedContactCorrection);
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
            Assert.That(gateIAuthorityFingerprints, Is.Not.Empty);
            Assert.That(maximumAcceptedContactCorrection,
                Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance + .0001f));
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
                gateIAuthorityFingerprints,
                gateJPerceptionFingerprints,
                recorder != null,
                recorder == null ? 0 : recorder.Complete().Events.Count,
                director.GateILegacyWriterInvocations,
                director.GateHLegacyWriterInvocations,
                director.MaximumAppliedMovementCorrection));
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

        private static PlayerAbilityProfile CreateCalibratedToolAbility(
            bool isExactBlocker, bool isRecovery, bool isPreparedAttacker)
        {
            // No power candidate can clear Gate I's .45 capacity reliability
            // gate.  The tool branch remains an ordinary fallback comparison:
            // its value comes solely from the selected real blocker and saver.
            // Gate I's set intent must still be a genuine above-net attacking
            // opportunity; reducing power capacity does not mean lowering the
            // contact geometry below the route solver's 2.60m gate.
            var jump = isExactBlocker ? 1f : .50f;
            var standingReach = isExactBlocker ? 3.10f : 2.45f;
            return new PlayerAbilityProfile(MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(1.90f, standingReach, jump,
                    isPreparedAttacker ? .3f : isRecovery ? 1f : .2f,
                    isRecovery ? 1f : .2f, isRecovery ? 1f : 0f),
                new TechnicalBaseAttributesV4(
                    attackTechnique: 0f,
                    attackPower: 0f,
                    blockTechnique: isExactBlocker ? 1f : 0f,
                    defenseTechnique: isRecovery ? 1f : .1f,
                    receiveTechnique: isRecovery ? 1f : .1f,
                    setTechnique: 1f,
                    serveTechnique: .7f,
                    softTouch: 1f,
                    courtAwareness: isExactBlocker ? 1f : 0f),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1));
        }

        private static PlayerAbilityProfile CreateAwarenessAbility(
            float awareness)
        {
            return new PlayerAbilityProfile(MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(
                    1.90f, 2.47f, .8f, .8f, .8f, .8f),
                new TechnicalBaseAttributesV4(
                    attackTechnique: .8f,
                    attackPower: .8f,
                    blockTechnique: .8f,
                    defenseTechnique: .8f,
                    receiveTechnique: .8f,
                    setTechnique: .8f,
                    serveTechnique: .8f,
                    softTouch: .8f,
                    courtAwareness: awareness),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1));
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
                IReadOnlyList<string> gateIAuthorityFingerprints,
                IReadOnlyList<string> gateJPerceptionFingerprints,
                bool hasDiagnosticReplay,
                int diagnosticRecordCount,
                int gateILegacyWriterInvocations,
                int gateHLegacyWriterInvocations,
                float maximumAppliedMovementCorrection)
            {
                WinnerTeamId = winnerTeamId;
                HomeScore = homeScore;
                AwayScore = awayScore;
                AcceptedContacts = acceptedContacts;
                V3TransitionCount = v3TransitionCount;
                V3ReasonCodes = v3ReasonCodes;
                AcceptedBallStateVersions = acceptedBallStateVersions;
                AcceptedAuthorityFingerprints = acceptedAuthorityFingerprints;
                GateIAuthorityFingerprints = gateIAuthorityFingerprints;
                GateJPerceptionFingerprints = gateJPerceptionFingerprints;
                HasDiagnosticReplay = hasDiagnosticReplay;
                DiagnosticRecordCount = diagnosticRecordCount;
                GateILegacyWriterInvocations = gateILegacyWriterInvocations;
                GateHLegacyWriterInvocations = gateHLegacyWriterInvocations;
                MaximumAppliedMovementCorrection = maximumAppliedMovementCorrection;
            }

            public string WinnerTeamId { get; }
            public int HomeScore { get; }
            public int AwayScore { get; }
            public int AcceptedContacts { get; }
            public int V3TransitionCount { get; }
            public IReadOnlyList<string> V3ReasonCodes { get; }
            public IReadOnlyList<long> AcceptedBallStateVersions { get; }
            public IReadOnlyList<string> AcceptedAuthorityFingerprints { get; }
            public IReadOnlyList<string> GateIAuthorityFingerprints { get; }
            public IReadOnlyList<string> GateJPerceptionFingerprints { get; }
            public bool HasDiagnosticReplay { get; }
            public int DiagnosticRecordCount { get; }
            public int GateILegacyWriterInvocations { get; }
            public int GateHLegacyWriterInvocations { get; }
            public float MaximumAppliedMovementCorrection { get; }
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

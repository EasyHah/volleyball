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
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.PlayModeTests
{
    // These scenarios intentionally exercise the scene-owned contact sources.  They do not
    // synthesize a rules transition: a Receive must originate at a PrototypePlayerAgent surface.
    public sealed class DefensiveContactContinuationPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrototypePlayerAgent_CollectContacts_ProducesReceiveThroughSimulatedBall()
        {
            var ballObject = new GameObject("DefensiveContinuationBall");
            var playerObject = new GameObject("DefensiveContinuationReceiver");
            try
            {
                ballObject.transform.position = new Vector3(0f, 1.3f, 0f);
                var ball = ballObject.AddComponent<SimulatedBall>();
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new RuntimePlayerId(TeamId.Blue, PlayerRole.Defender), Color.blue, "3");
                player.ScheduleContact(
                    TechniqueAction.Receive,
                    0f,
                    new SimVector3(0f, 7f, 2f),
                    new SkillExecutionError(0f, SimVector3.Zero, SimVector3.Zero, 0f, 1f,
                        SimVector3.Zero, 1f),
                    9101,
                    plannedContactCenter: new SimVector3(0f, 1f, 0f));
                ball.RegisterContactSource(player);
                PlayerBallContactEvent accepted = default;
                ball.PlayerContact += contact => accepted = contact;
                ball.Launch(new Vector3(0f, -40f, 0f));

                ball.AdvanceSimulation(SimulatedBall.DefaultFixedStep);
                yield return null;

                Assert.That(accepted.Candidate.Action, Is.EqualTo(TechniqueAction.Receive));
                Assert.That(accepted.Candidate.Actor, Is.EqualTo(player.Id));
                Assert.That(ball.State.LastContactGroupId, Is.EqualTo(9101));
            }
            finally
            {
                UnityEngine.Object.Destroy(ballObject);
                UnityEngine.Object.Destroy(playerObject);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CommittedFloorDefense_DigsReachableSpike()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(value => probe = value);

            yield return probe.WaitFor(
                () => probe.FloorDefenses.Count > 0 || probe.Director.Result != null,
                30f);

            Assert.That(probe.FloorDefenses, Is.Not.Empty,
                probe.Evidence("Gate I never committed a reachable FloorDefense."));
            yield return probe.WaitFor(
                () => probe.AcceptedReceives.Any(contact =>
                          contact.Candidate.Actor.HasValue &&
                          probe.IsCommittedFloorDefender(contact.Candidate.Actor.Value)) ||
                      probe.Director.Result != null,
                10f);

            Assert.That(probe.AcceptedReceives.Any(contact =>
                    contact.Candidate.Actor.HasValue &&
                    probe.IsCommittedFloorDefender(contact.Candidate.Actor.Value)), Is.True,
                probe.Evidence("Committed FloorDefense must expose a real receive surface and dig the spike."));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LateFloorDefense_DoesNotCreateMagicDig()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(value => probe = value);

            yield return probe.WaitFor(() => probe.Director.Result != null, 30f);

            Assert.That(probe.GroundEvents, Is.Not.Empty,
                probe.Evidence("A missed or late defense must resolve through a physical ground event."));
            Assert.That(probe.Director.Result, Is.Not.Null);
            Assert.That(probe.ReplayResults, Is.Not.Empty,
                probe.Evidence("A missed post-attack defense must resolve the rally."));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator BlockReboundToAttackingSide_AllowsAttackCoverage()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(value => probe = value);

            yield return probe.WaitFor(
                () => probe.AcceptedBlocks.Count > 0 || probe.Director.Result != null,
                35f);

            Assert.That(probe.AcceptedBlocks, Is.Not.Empty,
                probe.Evidence("Fixture did not reach a physical block."));
            var block = probe.AcceptedBlocks.First();
            yield return probe.WaitFor(
                () => probe.AcceptedReceives.Any(contact =>
                          contact.Candidate.Actor.HasValue &&
                          contact.Candidate.Actor.Value.Team != block.Candidate.Actor.Value.Team) ||
                      probe.Director.Result != null,
                10f);

            Assert.That(probe.AcceptedReceives.Any(contact =>
                    contact.Candidate.Actor.HasValue &&
                    contact.Candidate.Actor.Value.Team != block.Candidate.Actor.Value.Team), Is.True,
                probe.Evidence("A rebound crossing to the attacking side needs an AttackCover receive window."));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator BlockReboundToDefendingSide_AllowsBlockRecovery()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(value => probe = value);

            yield return probe.WaitFor(
                () => probe.AcceptedBlocks.Count > 0 || probe.Director.Result != null,
                35f);

            Assert.That(probe.AcceptedBlocks, Is.Not.Empty,
                probe.Evidence("Fixture did not reach a physical block."));
            var block = probe.AcceptedBlocks.First();
            yield return probe.WaitFor(() => probe.Director.Result != null, 10f);

            Assert.That(probe.AcceptedReceives, Is.Not.Empty,
                probe.Evidence("The rally must continue through physical contacts or a ground decision."));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PostBlockMiss_StillLetsGroundRefereeScore()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(value => probe = value);

            yield return probe.WaitFor(
                () => probe.AcceptedBlocks.Count > 0 || probe.Director.Result != null,
                35f);
            yield return probe.WaitFor(() => probe.Director.Result != null, 20f);

            Assert.That(probe.AcceptedBlocks, Is.Not.Empty,
                probe.Evidence("Fixture did not reach a physical block before the rally ended."));
            Assert.That(probe.GroundEvents, Is.Not.Empty,
                probe.Evidence("Post-block miss must remain visible to the ground referee."));
            Assert.That(probe.ReplayResults, Is.Not.Empty,
                probe.Evidence("Ground resolution must emit a rally result."));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OverlappingDefenders_AcceptOnlyOneReceive()
        {
            DefensiveContactProbe probe = null;
            yield return LoadFormalProbe(value => probe = value);

            yield return probe.WaitFor(
                () => probe.FloorDefenses.Count >= 2 || probe.Director.Result != null,
                35f);

            Assert.That(probe.FloorDefenses.Count, Is.GreaterThanOrEqualTo(2),
                probe.Evidence("Fixture did not commit overlapping defender responsibilities."));
            yield return probe.WaitFor(
                () => probe.AcceptedReceives.Count > 0 || probe.Director.Result != null,
                10f);

            Assert.That(probe.AcceptedReceives, Has.Count.EqualTo(1),
                probe.Evidence("RED: one physical ball event must select exactly one overlapping Receive."));
        }

        private static IEnumerator LoadFormalProbe(Action<DefensiveContactProbe> loaded)
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            loaded(new DefensiveContactProbe(director, ball, players));
        }

        private sealed class DefensiveContactProbe
        {
            private readonly Dictionary<StablePlayerId, RuntimePlayerId> _runtimeIds;

            public DefensiveContactProbe(
                FormalSixVsSixRallyDirector director,
                SimulatedBall ball,
                IEnumerable<PrototypePlayerAgent> players)
            {
                Director = director;
                _runtimeIds = players.ToDictionary(player => player.StableId, player => player.Id);
                director.AttackDefenseAuthorityCommitted += receipt =>
                {
                    if (receipt.Kind == AttackDefenseCommandKind.FloorDefense)
                        FloorDefenses.Add(receipt);
                };
                director.ReplayContactAccepted += contact => ReplayContacts.Add(contact);
                director.ReplayGroundContact += ground => GroundEvents.Add(ground);
                director.ReplayRallyResolved += result => ReplayResults.Add(result);
                ball.PlayerContact += contact => PhysicalContacts.Add(contact);
            }

            public FormalSixVsSixRallyDirector Director { get; }
            public List<AttackDefenseAuthorityReceipt> FloorDefenses { get; } = new List<AttackDefenseAuthorityReceipt>();
            public List<PlayerBallContactEvent> PhysicalContacts { get; } = new List<PlayerBallContactEvent>();
            public List<ReplayContactEvent> ReplayContacts { get; } = new List<ReplayContactEvent>();
            public List<ReplaySimpleEvent> GroundEvents { get; } = new List<ReplaySimpleEvent>();
            public List<ReplayRallyResolvedEvent> ReplayResults { get; } = new List<ReplayRallyResolvedEvent>();

            public List<PlayerBallContactEvent> AcceptedBlocks => PhysicalContacts.Where(
                contact => contact.Candidate.Action == TechniqueAction.Block).ToList();
            public List<PlayerBallContactEvent> AcceptedReceives => PhysicalContacts.Where(
                contact => contact.Candidate.Action == TechniqueAction.Receive).ToList();

            public bool HasAcceptedReceive(StablePlayerId actor) =>
                _runtimeIds.TryGetValue(actor, out var runtimeActor) &&
                HasAcceptedReceive(runtimeActor);

            public bool HasAcceptedReceive(RuntimePlayerId actor) =>
                PhysicalContacts.Any(contact => contact.Candidate.Action == TechniqueAction.Receive &&
                    contact.Candidate.Actor.HasValue && contact.Candidate.Actor.Value.Equals(actor));

            public bool IsCommittedFloorDefender(RuntimePlayerId actor) =>
                _runtimeIds.Any(pair => pair.Value.Equals(actor) &&
                    FloorDefenses.Any(receipt => receipt.Actor.Equals(pair.Key)));

            public IEnumerator WaitFor(Func<bool> predicate, float seconds)
            {
                var previousTimeScale = Time.timeScale;
                Time.timeScale = 8f;
                var deadline = Time.realtimeSinceStartup + seconds;
                try
                {
                    while (!predicate() && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                finally
                {
                    Time.timeScale = previousTimeScale;
                }
            }

            public void AssertAcceptedReceiveWithRuleAndReplay(
                StablePlayerId actor, string message)
            {
                Assert.That(HasAcceptedReceive(actor), Is.True, Evidence(message));
                Assert.That(ReplayContacts.Any(contact => contact.Action == TechniqueAction.Receive &&
                    contact.PlayerId.HasValue && contact.PlayerId.Value.Equals(actor) &&
                    contact.RuleTransition != null), Is.True, Evidence(message));
            }

            public string Evidence(string message) =>
                $"{message} commands={FloorDefenses.Count}; physical={PhysicalContacts.Count}; " +
                $"receives={AcceptedReceives.Count}; replay={ReplayContacts.Count}; " +
                $"ground={GroundEvents.Count}; resolved={ReplayResults.Count}; " +
                $"result={(Director.Result == null ? "pending" : "complete")}";
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.PlayModeTests
{
    public sealed class TrainingNetContinuationPlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CatalogScenarios_UseOnePhysicalContinuationAndRepeatExactly()
        {
            foreach (var id in TrainingScenarioCatalogV1.ScenarioIds)
            {
                TrainingRunSnapshot first = null;
                yield return Run(id, value => first = value);
                AssertPhysicalContract(id, first);

                TrainingRunSnapshot second = null;
                yield return Run(id, value => second = value);
                Assert.That(
                    second,
                    Is.EqualTo(first),
                    id + " must repeat with the same scenario hash and seed.");
            }
        }

        private static IEnumerator Run(
            string id,
            Action<TrainingRunSnapshot> completed)
        {
            var preset = Resources.Load<TrainingScenarioPresetV1>(
                "TrainingScenariosV1/" + id);
            Assert.That(preset, Is.Not.Null, "Missing training scenario " + id);
            var definition = preset.ToDefinition();
            TrainingScenarioStartupV1.PrepareNextTrainingStart(definition);

            yield return SceneManager.LoadSceneAsync(
                "FormalIndoor6v6",
                LoadSceneMode.Single);
            var director =
                UnityEngine.Object.FindFirstObjectByType<
                    FormalSixVsSixRallyDirector>();
            var ball =
                UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);

            var contacts = new List<ReplayContactEvent>();
            var crossings = new List<ReplaySimpleEvent>();
            var attempts = new List<ReplayDefenseAttemptEvent>();
            var resolved = new List<ReplayRallyResolvedEvent>();
            var firstNetTime = -1f;
            director.ReplayContactAccepted += contacts.Add;
            director.ReplayNetCrossed += crossings.Add;
            director.ReplayDefenseAttemptRecorded += attempts.Add;
            director.ReplayRallyResolved += resolved.Add;
            ball.EnvironmentContact += hit =>
            {
                if (hit.Kind == EnvironmentContactKind.Net &&
                    firstNetTime < 0f)
                {
                    firstNetTime = ball.SimulationTime;
                }
            };

            var deadline = Time.realtimeSinceStartup + 12f;
            while (!director.TrainingSingleRallyCompleted &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(
                director.TrainingSingleRallyCompleted,
                Is.True,
                id + " did not reach the real referee.");
            Assert.That(resolved, Has.Count.EqualTo(1));
            completed(new TrainingRunSnapshot(
                id,
                definition.ContentHash,
                definition.Context.Seed,
                ball.Diagnostics.NetContacts,
                firstNetTime,
                director.NetDeflectionDispatches,
                director.SuppressedNetDeflectionDispatches,
                director.PostBlockContinuations,
                director.PrematurePostBlockReceiveWindows,
                contacts.Select(value => new ContactSnapshot(
                    value.SimulationTimeSeconds,
                    value.Team,
                    value.PlayerId?.Value ?? string.Empty,
                    value.Action,
                    value.RuleTransition?.Before?.CountedHits ?? -1,
                    value.RuleTransition?.After?.CountedHits ?? -1))
                    .ToArray(),
                crossings.Select(value => value.SimulationTimeSeconds)
                    .ToArray(),
                attempts.Select(value =>
                        value.Kind + ":" + value.AttemptIdentity + ":" +
                        value.ContinuationState + ":" + value.Reason)
                    .ToArray(),
                resolved[0].Team,
                resolved[0].Reason));
        }

        private static void AssertPhysicalContract(
            string id,
            TrainingRunSnapshot run)
        {
            Assert.That(run.NetContacts, Is.GreaterThanOrEqualTo(1), id);
            Assert.That(run.FirstNetTime, Is.GreaterThanOrEqualTo(0f), id);
            switch (id)
            {
                case TrainingScenarioCatalogV1.SecondTouchNetOwnSide:
                    Assert.That(run.NetDispatches, Is.EqualTo(1));
                    Assert.That(run.Contacts, Is.Not.Empty);
                    Assert.That(
                        run.Contacts[0].SimulationTime,
                        Is.GreaterThanOrEqualTo(run.FirstNetTime));
                    Assert.That(run.Contacts[0].BeforeHits, Is.EqualTo(2));
                    Assert.That(run.Contacts[0].AfterHits, Is.EqualTo(3));
                    break;
                case TrainingScenarioCatalogV1.ThirdTouchNetOwnSide:
                    Assert.That(run.NetDispatches, Is.EqualTo(1));
                    Assert.That(run.Contacts, Is.Empty);
                    break;
                case TrainingScenarioCatalogV1.ThirdTouchNetCross:
                    Assert.That(run.NetDispatches, Is.EqualTo(1));
                    Assert.That(run.Crossings, Is.Not.Empty);
                    Assert.That(run.Attempts, Is.Not.Empty);
                    if (run.Contacts.Length > 0)
                    {
                        Assert.That(
                            run.Contacts[0].Team,
                            Is.EqualTo(TeamId.Orange));
                        Assert.That(
                            run.Contacts[0].SimulationTime,
                            Is.GreaterThanOrEqualTo(run.Crossings[0]));
                    }
                    break;
                case TrainingScenarioCatalogV1.PostBlockNet:
                    Assert.That(run.NetDispatches, Is.Zero);
                    Assert.That(run.PostBlockContinuations, Is.EqualTo(1));
                    Assert.That(run.PrematurePostBlockWindows, Is.Zero);
                    break;
                case TrainingScenarioCatalogV1.NetDeflectionSingleDispatch:
                    Assert.That(run.NetContacts, Is.GreaterThanOrEqualTo(2));
                    Assert.That(run.NetDispatches, Is.EqualTo(1));
                    Assert.That(run.SuppressedNetDispatches,
                        Is.GreaterThanOrEqualTo(1));
                    break;
                case TrainingScenarioCatalogV1.SecondTouchNetUnreachable:
                    Assert.That(run.NetDispatches, Is.EqualTo(1));
                    Assert.That(
                        run.Contacts.Count(value =>
                            value.Action == TechniqueAction.Receive),
                        Is.Zero);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        private sealed class TrainingRunSnapshot : IEquatable<TrainingRunSnapshot>
        {
            public TrainingRunSnapshot(
                string scenarioId,
                string hash,
                int seed,
                int netContacts,
                float firstNetTime,
                int netDispatches,
                int suppressedNetDispatches,
                int postBlockContinuations,
                int prematurePostBlockWindows,
                ContactSnapshot[] contacts,
                float[] crossings,
                string[] attempts,
                TeamId winner,
                string reason)
            {
                ScenarioId = scenarioId;
                Hash = hash;
                Seed = seed;
                NetContacts = netContacts;
                FirstNetTime = firstNetTime;
                NetDispatches = netDispatches;
                SuppressedNetDispatches = suppressedNetDispatches;
                PostBlockContinuations = postBlockContinuations;
                PrematurePostBlockWindows = prematurePostBlockWindows;
                Contacts = contacts;
                Crossings = crossings;
                Attempts = attempts;
                Winner = winner;
                Reason = reason;
            }

            public string ScenarioId { get; }
            public string Hash { get; }
            public int Seed { get; }
            public int NetContacts { get; }
            public float FirstNetTime { get; }
            public int NetDispatches { get; }
            public int SuppressedNetDispatches { get; }
            public int PostBlockContinuations { get; }
            public int PrematurePostBlockWindows { get; }
            public ContactSnapshot[] Contacts { get; }
            public float[] Crossings { get; }
            public string[] Attempts { get; }
            public TeamId Winner { get; }
            public string Reason { get; }

            public bool Equals(TrainingRunSnapshot other)
            {
                return other != null &&
                       ScenarioId == other.ScenarioId &&
                       Hash == other.Hash &&
                       Seed == other.Seed &&
                       NetContacts == other.NetContacts &&
                       FirstNetTime.Equals(other.FirstNetTime) &&
                       NetDispatches == other.NetDispatches &&
                       SuppressedNetDispatches ==
                       other.SuppressedNetDispatches &&
                       PostBlockContinuations ==
                       other.PostBlockContinuations &&
                       PrematurePostBlockWindows ==
                       other.PrematurePostBlockWindows &&
                       Contacts.SequenceEqual(other.Contacts) &&
                       Crossings.SequenceEqual(other.Crossings) &&
                       Attempts.SequenceEqual(other.Attempts) &&
                       Winner == other.Winner &&
                       Reason == other.Reason;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as TrainingRunSnapshot);
            }

            public override int GetHashCode()
            {
                return Hash.GetHashCode();
            }
        }

        private readonly struct ContactSnapshot : IEquatable<ContactSnapshot>
        {
            public ContactSnapshot(
                float simulationTime,
                TeamId team,
                string playerId,
                TechniqueAction action,
                int beforeHits,
                int afterHits)
            {
                SimulationTime = simulationTime;
                Team = team;
                PlayerId = playerId;
                Action = action;
                BeforeHits = beforeHits;
                AfterHits = afterHits;
            }

            public float SimulationTime { get; }
            public TeamId Team { get; }
            public string PlayerId { get; }
            public TechniqueAction Action { get; }
            public int BeforeHits { get; }
            public int AfterHits { get; }

            public bool Equals(ContactSnapshot other)
            {
                return SimulationTime.Equals(other.SimulationTime) &&
                       Team == other.Team &&
                       PlayerId == other.PlayerId &&
                       Action == other.Action &&
                       BeforeHits == other.BeforeHits &&
                       AfterHits == other.AfterHits;
            }
        }
    }
}

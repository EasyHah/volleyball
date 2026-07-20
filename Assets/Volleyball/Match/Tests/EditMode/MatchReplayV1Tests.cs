using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Domain.Replay;

namespace Volleyball.EditModeTests
{
    public sealed class MatchReplayV1Tests
    {
        [Test]
        public void Json_RoundTripsASealedReplay()
        {
            var replay = ReplayFixture.CreateValid();

            var restored = MatchReplayJson.Deserialize(MatchReplayJson.Serialize(replay));

            Assert.That(restored.ContentChecksum, Is.EqualTo(replay.ContentChecksum));
            Assert.That(restored.Players, Has.Count.EqualTo(12));
            Assert.That(restored.Events[0].Kind, Is.EqualTo("RallyResolved"));
        }

        [Test]
        public void Validate_RejectsATamperedChecksum()
        {
            var replay = ReplayFixture.CreateValid();
            replay.ContentChecksum = new string('0', 64);

            Assert.Throws<MatchReplayValidationException>(() => replay.Validate());
        }

        [Test]
        public void Validate_RejectsNonMonotonicEventTime()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events.Add(new MatchReplayEventV1
            {
                Kind = "Serve",
                SimulationTimeSeconds = -0.1f,
                SnapshotIndex = 0,
                Team = "Blue"
            });

            Assert.Throws<MatchReplayValidationException>(() => replay.Validate());
        }

        [Test]
        public void Validate_RejectsAnEventWhoseSnapshotDoesNotExist()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events[0].SnapshotIndex = 2;

            Assert.Throws<MatchReplayValidationException>(() => replay.Validate());
        }

        [Test]
        public void Validate_RejectsAnEventThatReferencesASnapshotAtAnotherTime()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Snapshots.Add(ReplayFixture.CreateSnapshot(1f, eventSequence: 1));
            replay.Events[0].SimulationTimeSeconds = 1f;

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("same simulation time"));
        }

        [Test]
        public void Validate_AllowsDistinctOrderedEventsAtTheSameTime()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Snapshots.Add(ReplayFixture.CreateSnapshot(0f, eventSequence: 1));
            replay.Events.Add(new MatchReplayEventV1
            {
                Kind = "Serve",
                SimulationTimeSeconds = 0f,
                SnapshotIndex = 1,
                Team = "Blue"
            });
            replay.Seal();

            Assert.DoesNotThrow(() => replay.Validate());
        }

        [Test]
        public void Validate_RejectsSameTimeEventsThatDoNotAdvanceSnapshotOrder()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events.Add(new MatchReplayEventV1
            {
                Kind = "Serve",
                SimulationTimeSeconds = 0f,
                SnapshotIndex = 0,
                Team = "Blue"
            });

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("snapshot order"));
        }

        [Test]
        public void Validate_RejectsSameTimeSnapshotsWithEqualEventSequence()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Snapshots.Add(ReplayFixture.CreateSnapshot(0f, eventSequence: 0));

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("EventSequence"));
        }

        [Test]
        public void Validate_RejectsSameTimeEventsWhoseSnapshotsAreInReverseEventSequence()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Snapshots.Add(ReplayFixture.CreateSnapshot(0f, eventSequence: 2));
            replay.Events.Add(new MatchReplayEventV1
            {
                Kind = "Serve",
                SimulationTimeSeconds = 0f,
                SnapshotIndex = 1,
                Team = "Blue"
            });
            replay.Snapshots[0].EventSequence = 3;
            replay.Events[0].SnapshotIndex = 0;
            replay.Events[1].SnapshotIndex = 1;

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("EventSequence"));
        }

        [Test]
        public void Seal_UsesTheSameChecksumWhenDecisionWeightsHaveDifferentInsertionOrder()
        {
            var first = ReplayFixture.CreateValidWithDecisionWeights("attack", "reachability");
            var second = ReplayFixture.CreateValidWithDecisionWeights("reachability", "attack");

            Assert.That(second.ContentChecksum, Is.EqualTo(first.ContentChecksum));
        }

        [Test]
        public void Deserialize_RejectsJsonMissingFormatVersion()
        {
            var json = MatchReplayJson.Serialize(ReplayFixture.CreateValid());
            var withoutVersion = json.Replace("\"formatVersion\":1,", string.Empty);

            Assert.That(withoutVersion, Is.Not.EqualTo(json));
            Assert.That(
                () => MatchReplayJson.Deserialize(withoutVersion),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("formatVersion is required"));
        }

        private static class ReplayFixture
        {
            public static MatchReplayV1 CreateValid()
            {
                var players = new List<MatchReplayPlayerV1>();
                var samples = new List<MatchReplayPlayerStateV1>();
                for (var index = 0; index < 12; index++)
                {
                    var playerId = "player-" + (index + 1);
                    players.Add(new MatchReplayPlayerV1
                    {
                        PlayerId = playerId,
                        PrototypeId = "prototype-" + (index + 1),
                        DisplayName = "Player " + (index + 1),
                        Team = index < 6 ? "Blue" : "Orange",
                        Role = index == 0 ? "Setter" : "Outside",
                        RosterSlot = (index % 6) + 1,
                        Ability = new MatchReplayAbilityV1
                        {
                            Receive = 0.7f,
                            Set = 0.7f,
                            Attack = 0.7f,
                            Block = 0.7f,
                            Serve = 0.7f,
                            Speed = 0.7f
                        }
                    });
                    samples.Add(new MatchReplayPlayerStateV1
                    {
                        PlayerId = playerId,
                        Position = new MatchReplayVector3V1 { X = index, Y = 0f, Z = 0f },
                        YawDegrees = 0f,
                        ScheduledAction = "Idle",
                        MovementTarget = new MatchReplayVector3V1 { X = index, Y = 0f, Z = 0f }
                    });
                }

                var replay = new MatchReplayV1
                {
                    SourceScene = "FormalIndoor6v6",
                    CapturedAtUtc = "2026-07-20T00:00:00.0000000Z",
                    Court = new MatchReplayCourtV1 { WidthMeters = 9f, LengthMeters = 18f },
                    TargetScore = 25,
                    Players = players,
                    InitialState = new MatchReplayInitialStateV1
                    {
                        HomeScore = 0,
                        AwayScore = 0,
                        ServingTeam = "Blue",
                        HomeRotationOffset = 0,
                        AwayRotationOffset = 0
                    },
                    Snapshots = new List<MatchReplaySnapshotV1>
                    {
                        CreateSnapshot(0f, 0, samples)
                    },
                    Events = new List<MatchReplayEventV1>
                    {
                        new MatchReplayEventV1
                        {
                            Kind = "RallyResolved",
                            SimulationTimeSeconds = 0f,
                            SnapshotIndex = 0,
                            Team = "Blue"
                        }
                    },
                    IsComplete = true
                };
                replay.Seal();
                return replay;
            }

            public static MatchReplayV1 CreateValidWithDecisionWeights(string firstWeight, string secondWeight)
            {
                var replay = CreateValid();
                replay.Events[0].Decision = new MatchReplayDecisionV1
                {
                    Stage = "Attack",
                    Team = "Blue",
                    Action = "Spike",
                    PredictedBallTarget = new MatchReplayVector3V1 { X = 1f, Y = 2f, Z = 3f },
                    AvailableSeconds = 0.5f,
                    SelectedPlayerId = "player-1",
                    SelectedAction = "Spike"
                };
                replay.Events[0].Decision.Weights.Add(firstWeight, WeightValue(firstWeight));
                replay.Events[0].Decision.Weights.Add(secondWeight, WeightValue(secondWeight));
                replay.Seal();
                return replay;
            }

            public static MatchReplaySnapshotV1 CreateSnapshot(float simulationTimeSeconds, int eventSequence)
            {
                return CreateSnapshot(simulationTimeSeconds, eventSequence, CreatePlayerSamples());
            }

            private static MatchReplaySnapshotV1 CreateSnapshot(
                float simulationTimeSeconds,
                int eventSequence,
                List<MatchReplayPlayerStateV1> samples)
            {
                return new MatchReplaySnapshotV1
                {
                    SimulationTimeSeconds = simulationTimeSeconds,
                    EventSequence = eventSequence,
                    HomeScore = 1,
                    AwayScore = 0,
                    ServingTeam = "Blue",
                    HomeRotationOffset = 0,
                    AwayRotationOffset = 0,
                    RallyPhase = "Resolved",
                    PossessionTeam = "",
                    LastTouchPlayerId = "",
                    Ball = new MatchReplayBallStateV1
                    {
                        Position = new MatchReplayVector3V1 { X = 0f, Y = 1f, Z = 0f },
                        Velocity = new MatchReplayVector3V1 { X = 0f, Y = 0f, Z = 0f }
                    },
                    Players = samples
                };
            }

            private static List<MatchReplayPlayerStateV1> CreatePlayerSamples()
            {
                var samples = new List<MatchReplayPlayerStateV1>();
                for (var index = 0; index < 12; index++)
                {
                    var playerId = "player-" + (index + 1);
                    samples.Add(new MatchReplayPlayerStateV1
                    {
                        PlayerId = playerId,
                        Position = new MatchReplayVector3V1 { X = index, Y = 0f, Z = 0f },
                        YawDegrees = 0f,
                        ScheduledAction = "Idle",
                        MovementTarget = new MatchReplayVector3V1 { X = index, Y = 0f, Z = 0f }
                    });
                }

                return samples;
            }

            private static float WeightValue(string name)
            {
                return name == "attack" ? 0.5f : 0.75f;
            }
        }
    }
}

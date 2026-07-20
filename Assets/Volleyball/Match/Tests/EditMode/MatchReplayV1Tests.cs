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
                        new MatchReplaySnapshotV1
                        {
                            SimulationTimeSeconds = 0f,
                            EventSequence = 0,
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
                        }
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
        }
    }
}

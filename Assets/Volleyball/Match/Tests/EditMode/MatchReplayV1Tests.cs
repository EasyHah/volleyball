using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Volleyball.Domain.Replay;
using Volleyball.Presentation;

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
        public void Validate_RejectsSameTimeSnapshotsWhoseEventSequenceIsReversed()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Snapshots.Add(ReplayFixture.CreateSnapshot(0f, eventSequence: 2));
            replay.Snapshots[0].EventSequence = 3;

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("EventSequence"));
        }

        [TestCase("2026-07-20T00:00:00")]
        [TestCase("2026-07-20T00:00:00+00:00")]
        [TestCase("07/20/2026 00:00:00Z")]
        public void Seal_RejectsCapturedAtUtcThatIsNotCanonicalUtcRoundTrip(string capturedAtUtc)
        {
            var replay = ReplayFixture.CreateValid();
            replay.CapturedAtUtc = capturedAtUtc;

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("UTC ISO-8601"));
        }

        [Test]
        public void Seal_UsesTheSameChecksumWhenDecisionWeightsHaveDifferentInsertionOrder()
        {
            var first = ReplayFixture.CreateValidWithDecisionWeights("attack", "reachability");
            var second = ReplayFixture.CreateValidWithDecisionWeights("reachability", "attack");

            Assert.That(second.ContentChecksum, Is.EqualTo(first.ContentChecksum));
        }

        [Test]
        public void Json_RoundTripsAValidatedSetChain()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events[0].SetChain = ReplayFixture.CreateSetChain();
            replay.Seal();

            var restored = MatchReplayJson.Deserialize(MatchReplayJson.Serialize(replay));

            Assert.That(restored.Events[0].SetChain.QualityGrade, Is.EqualTo("A"));
            Assert.That(restored.Events[0].SetChain.ActualAttackContactCenter.X,
                Is.EqualTo(1.1f));
            Assert.DoesNotThrow(() => restored.Validate());
        }

        [Test]
        public void Seal_AllowsActualAttackContactCenterToRemainUnavailable()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events[0].SetChain = ReplayFixture.CreateSetChain();
            replay.Events[0].SetChain.ActualAttackContactCenter = null;

            Assert.DoesNotThrow(() => replay.Seal());

            var restored = MatchReplayJson.Deserialize(MatchReplayJson.Serialize(replay));
            Assert.That(restored.Events[0].SetChain.PlannedAttackContactCenter, Is.Not.Null);
            Assert.That(restored.Events[0].SetChain.ActualAttackContactCenter, Is.Null);
            Assert.DoesNotThrow(() => restored.Validate());
        }

        [Test]
        public void Json_OmitsAbsentSetChainForHistoricalV1Events()
        {
            var json = MatchReplayJson.Serialize(ReplayFixture.CreateValid());

            Assert.That(json, Does.Not.Contain("\"setChain\""));
        }

        [Test]
        public void Json_RoundTripsRallyResolutionReasonAndErrorPlayer()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events[0].PlayerId = "player-1";
            replay.Events[0].ErrorPlayerId = "player-7";
            replay.Events[0].Reason = "attack landed out";
            replay.Seal();

            var restored = MatchReplayJson.Deserialize(MatchReplayJson.Serialize(replay));

            Assert.That(restored.Events[0].ErrorPlayerId, Is.EqualTo("player-7"));
            Assert.That(restored.Events[0].Reason, Is.EqualTo("attack landed out"));
        }

        [Test]
        public void Validate_RejectsSetChainWithoutAQualityGrade()
        {
            var replay = ReplayFixture.CreateValid();
            replay.Events[0].SetChain = ReplayFixture.CreateSetChain();
            replay.Events[0].SetChain.QualityGrade = null;

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("QualityGrade"));
        }

        [Test]
        public void Json_RoundTripsOptionalDecisionDiagnosticsAndPreservesTheirCanonicalPayload()
        {
            var replay = ReplayFixture.CreateValidWithDecisionDiagnostics();

            var restored = MatchReplayJson.Deserialize(MatchReplayJson.Serialize(replay));

            var diagnostics = restored.Events[0].Decision.Diagnostics;
            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics.ConsumedAbilities, Has.Count.EqualTo(2));
            Assert.That(diagnostics.ConsumedAbilities[0].PlayerId, Is.EqualTo("player-1"));
            Assert.That(diagnostics.ConsumedAbilities[0].MaxAttackReach, Is.EqualTo(3.42f));
            Assert.That(diagnostics.Organization.Target.X, Is.EqualTo(1.5f));
            Assert.That(diagnostics.Organization.ZoneGrade, Is.EqualTo("Best"));
            Assert.That(restored.ContentChecksum, Is.EqualTo(replay.ContentChecksum));
            Assert.DoesNotThrow(() => restored.Validate());
        }

        [Test]
        public void Validate_RejectsDecisionDiagnosticsThatReferenceAnUnknownPlayer()
        {
            var replay = ReplayFixture.CreateValidWithDecisionDiagnostics();
            replay.Events[0].Decision.Diagnostics.ConsumedAbilities[0].PlayerId = "unknown-player";

            Assert.That(
                () => replay.Seal(),
                Throws.TypeOf<MatchReplayValidationException>()
                    .With.Message.Contains("Consumed ability PlayerId"));
        }

        [Test]
        public void HtmlWriter_EmbedsAndLabelsProvidedDecisionDiagnostics()
        {
            var replay = ReplayFixture.CreateValidWithDecisionDiagnostics();
            var outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "volleyball-match-replay-tests",
                Guid.NewGuid().ToString("N"));

            MatchReplayArtifactWriter.Write(outputDirectory, replay);

            var html = File.ReadAllText(Path.Combine(outputDirectory, "index.html"));
            Assert.That(html, Does.Contain("Live V2 ability profile"));
            Assert.That(html, Does.Contain("Organization diagnostics"));
            Assert.That(html, Does.Contain("maxAttackReach"));
            Assert.That(html, Does.Contain("Predicted first-pass landing"));
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

            public static MatchReplayV1 CreateValidWithDecisionDiagnostics()
            {
                var replay = CreateValidWithDecisionWeights("attack", "reachability");
                replay.Events[0].Decision.Diagnostics = new MatchReplayDecisionDiagnosticsV1
                {
                    ConsumedAbilities = new List<MatchReplayConsumedAbilityV1>
                    {
                        new MatchReplayConsumedAbilityV1
                        {
                            PlayerId = "player-1",
                            Mobility = 0.71f,
                            Reaction = 0.72f,
                            Jump = 0.73f,
                            ReceiveTechnique = 0.74f,
                            SetTechnique = 0.75f,
                            AttackTechnique = 0.76f,
                            AttackPower = 0.77f,
                            MaxAttackReach = 3.42f
                        },
                        new MatchReplayConsumedAbilityV1
                        {
                            PlayerId = "player-2",
                            Mobility = 0.61f,
                            Reaction = 0.62f,
                            Jump = 0.63f,
                            ReceiveTechnique = 0.64f,
                            SetTechnique = 0.65f,
                            AttackTechnique = 0.66f,
                            AttackPower = 0.67f,
                            MaxAttackReach = 3.32f
                        }
                    },
                    Organization = new MatchReplayOrganizationDiagnosticsV1
                    {
                        Target = new MatchReplayVector3V1 { X = 1.5f, Y = 2.4f, Z = -1.1f },
                        FirstPassLanding = new MatchReplayVector3V1 { X = 1.45f, Y = 2.3f, Z = -1.12f },
                        ZoneGrade = "Best",
                        SetterPlayerId = "player-1",
                        SetterArrival = "AtTarget",
                        SetterMovementMeters = 0.12f,
                        OrganizerPlayerId = "player-1",
                        FallbackReason = string.Empty
                    }
                };
                replay.Seal();
                return replay;
            }

            public static MatchReplaySetChainV1 CreateSetChain()
            {
                return new MatchReplaySetChainV1
                {
                    PlannedAttackContactCenter = new MatchReplayVector3V1
                        { X = 1f, Y = 3.4f, Z = -2f },
                    ActualAttackContactCenter = new MatchReplayVector3V1
                        { X = 1.1f, Y = 3.35f, Z = -2.1f },
                    QualityGrade = "A",
                    ReplanOutcome = "FullAttack",
                    PrimaryResponsibility = "None",
                    Reason = "all errors within A thresholds"
                };
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Domain.Replay
{
    public sealed class MatchReplayValidationException : Exception
    {
        public MatchReplayValidationException(string message)
            : base(message)
        {
        }

        public MatchReplayValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    [DataContract]
    public sealed class MatchReplayV1
    {
        public const int FormatVersion = 1;
        public const float SampleIntervalSeconds = 0.1f;

        [DataMember(Name = "formatVersion", Order = 1)]
        private int _formatVersion = FormatVersion;

        [DataMember(Name = "sourceScene", Order = 2)]
        public string SourceScene { get; set; }

        [DataMember(Name = "capturedAtUtc", Order = 3)]
        public string CapturedAtUtc { get; set; }

        [DataMember(Name = "sampleIntervalSeconds", Order = 4)]
        public float SampleIntervalSecondsValue { get; set; } = SampleIntervalSeconds;

        [DataMember(Name = "court", Order = 5)]
        public MatchReplayCourtV1 Court { get; set; }

        [DataMember(Name = "targetScore", Order = 6)]
        public int TargetScore { get; set; }

        [DataMember(Name = "players", Order = 7)]
        public List<MatchReplayPlayerV1> Players { get; set; } = new List<MatchReplayPlayerV1>();

        [DataMember(Name = "initialState", Order = 8)]
        public MatchReplayInitialStateV1 InitialState { get; set; }

        [DataMember(Name = "snapshots", Order = 9)]
        public List<MatchReplaySnapshotV1> Snapshots { get; set; } = new List<MatchReplaySnapshotV1>();

        [DataMember(Name = "events", Order = 10)]
        public List<MatchReplayEventV1> Events { get; set; } = new List<MatchReplayEventV1>();

        [DataMember(Name = "isComplete", Order = 11)]
        public bool IsComplete { get; set; }

        [DataMember(Name = "contentChecksum", Order = 12)]
        public string ContentChecksum { get; set; }

        public int ReplayFormatVersion => _formatVersion;

        public void Seal()
        {
            ValidatePayload();
            ContentChecksum = ComputeChecksum();
        }

        public void Validate()
        {
            ValidatePayload();
            if (!IsSha256(ContentChecksum))
            {
                throw new MatchReplayValidationException("contentChecksum must be a SHA-256 hex digest.");
            }

            var expectedChecksum = ComputeChecksum();
            if (!string.Equals(expectedChecksum, ContentChecksum, StringComparison.Ordinal))
            {
                throw new MatchReplayValidationException("contentChecksum does not match the replay payload.");
            }
        }

        internal string CanonicalPayloadJson()
        {
            return MatchReplayJson.SerializeCanonicalPayload(new MatchReplayCanonicalPayloadV1
            {
                FormatVersion = _formatVersion,
                SourceScene = SourceScene,
                CapturedAtUtc = CapturedAtUtc,
                SampleIntervalSeconds = SampleIntervalSecondsValue,
                Court = Court,
                TargetScore = TargetScore,
                Players = Players,
                InitialState = InitialState,
                Snapshots = Snapshots,
                Events = Events,
                IsComplete = IsComplete
            });
        }

        private string ComputeChecksum()
        {
            var payload = Encoding.UTF8.GetBytes(CanonicalPayloadJson());
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(payload);
            var builder = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private void ValidatePayload()
        {
            if (_formatVersion != FormatVersion)
            {
                throw new MatchReplayValidationException(
                    "Unsupported replay format version: " + _formatVersion + ".");
            }

            Required(SourceScene, nameof(SourceScene));
            if (!DateTime.TryParse(CapturedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out _))
            {
                throw new MatchReplayValidationException("CapturedAtUtc must be an ISO-8601 timestamp.");
            }

            Finite(SampleIntervalSecondsValue, nameof(SampleIntervalSecondsValue));
            if (Math.Abs(SampleIntervalSecondsValue - SampleIntervalSeconds) > 0.0001f)
            {
                throw new MatchReplayValidationException("sampleIntervalSeconds must be 0.1.");
            }

            if (Court == null)
            {
                throw new MatchReplayValidationException("Court is required.");
            }

            Court.Validate();
            if (TargetScore <= 0)
            {
                throw new MatchReplayValidationException("TargetScore must be positive.");
            }

            if (!IsComplete)
            {
                throw new MatchReplayValidationException("Replay must be complete before it can be sealed.");
            }

            if (InitialState == null)
            {
                throw new MatchReplayValidationException("InitialState is required.");
            }

            InitialState.Validate();
            var playerIds = ValidatePlayers();
            ValidateSnapshots(playerIds);
            ValidateEvents(playerIds);
        }

        private HashSet<string> ValidatePlayers()
        {
            if (Players == null || Players.Count != 12)
            {
                throw new MatchReplayValidationException("Replay must contain exactly twelve players.");
            }

            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < Players.Count; index++)
            {
                var player = Players[index] ?? throw new MatchReplayValidationException("Players cannot contain null.");
                player.Validate();
                if (!playerIds.Add(player.PlayerId))
                {
                    throw new MatchReplayValidationException("Player IDs must be unique.");
                }
            }

            return playerIds;
        }

        private void ValidateSnapshots(ISet<string> playerIds)
        {
            if (Snapshots == null || Snapshots.Count == 0)
            {
                throw new MatchReplayValidationException("Replay must contain at least one snapshot.");
            }

            var previousTime = float.NegativeInfinity;
            for (var index = 0; index < Snapshots.Count; index++)
            {
                var snapshot = Snapshots[index] ?? throw new MatchReplayValidationException("Snapshots cannot contain null.");
                snapshot.Validate(playerIds);
                if (snapshot.SimulationTimeSeconds < previousTime)
                {
                    throw new MatchReplayValidationException("Snapshot simulation times must be monotonic.");
                }

                previousTime = snapshot.SimulationTimeSeconds;
            }
        }

        private void ValidateEvents(ISet<string> playerIds)
        {
            if (Events == null || Events.Count == 0)
            {
                throw new MatchReplayValidationException("Replay must contain at least one event.");
            }

            var previousTime = float.NegativeInfinity;
            for (var index = 0; index < Events.Count; index++)
            {
                var replayEvent = Events[index] ?? throw new MatchReplayValidationException("Events cannot contain null.");
                replayEvent.Validate(playerIds, Snapshots.Count);
                if (replayEvent.SimulationTimeSeconds < previousTime)
                {
                    throw new MatchReplayValidationException("Event simulation times must be monotonic.");
                }

                previousTime = replayEvent.SimulationTimeSeconds;
            }
        }

        internal static void Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MatchReplayValidationException(name + " is required.");
            }
        }

        internal static void OptionalPlayer(ISet<string> playerIds, string playerId, string name)
        {
            if (!string.IsNullOrEmpty(playerId) && !playerIds.Contains(playerId))
            {
                throw new MatchReplayValidationException(name + " references an unknown player: " + playerId + ".");
            }
        }

        internal static void Finite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new MatchReplayValidationException(name + " must be finite.");
            }
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f') ||
                      (character >= 'A' && character <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [DataContract]
    internal sealed class MatchReplayCanonicalPayloadV1
    {
        [DataMember(Name = "formatVersion", Order = 1)] public int FormatVersion { get; set; }
        [DataMember(Name = "sourceScene", Order = 2)] public string SourceScene { get; set; }
        [DataMember(Name = "capturedAtUtc", Order = 3)] public string CapturedAtUtc { get; set; }
        [DataMember(Name = "sampleIntervalSeconds", Order = 4)] public float SampleIntervalSeconds { get; set; }
        [DataMember(Name = "court", Order = 5)] public MatchReplayCourtV1 Court { get; set; }
        [DataMember(Name = "targetScore", Order = 6)] public int TargetScore { get; set; }
        [DataMember(Name = "players", Order = 7)] public List<MatchReplayPlayerV1> Players { get; set; }
        [DataMember(Name = "initialState", Order = 8)] public MatchReplayInitialStateV1 InitialState { get; set; }
        [DataMember(Name = "snapshots", Order = 9)] public List<MatchReplaySnapshotV1> Snapshots { get; set; }
        [DataMember(Name = "events", Order = 10)] public List<MatchReplayEventV1> Events { get; set; }
        [DataMember(Name = "isComplete", Order = 11)] public bool IsComplete { get; set; }
    }

    [DataContract]
    public sealed class MatchReplayCourtV1
    {
        [DataMember(Name = "widthMeters", Order = 1)] public float WidthMeters { get; set; }
        [DataMember(Name = "lengthMeters", Order = 2)] public float LengthMeters { get; set; }

        internal void Validate()
        {
            MatchReplayV1.Finite(WidthMeters, nameof(WidthMeters));
            MatchReplayV1.Finite(LengthMeters, nameof(LengthMeters));
            if (WidthMeters <= 0f || LengthMeters <= 0f)
            {
                throw new MatchReplayValidationException("Court dimensions must be positive.");
            }
        }
    }

    [DataContract]
    public sealed class MatchReplayAbilityV1
    {
        [DataMember(Name = "receive", Order = 1)] public float Receive { get; set; }
        [DataMember(Name = "set", Order = 2)] public float Set { get; set; }
        [DataMember(Name = "attack", Order = 3)] public float Attack { get; set; }
        [DataMember(Name = "block", Order = 4)] public float Block { get; set; }
        [DataMember(Name = "serve", Order = 5)] public float Serve { get; set; }
        [DataMember(Name = "speed", Order = 6)] public float Speed { get; set; }

        internal void Validate()
        {
            MatchReplayV1.Finite(Receive, nameof(Receive));
            MatchReplayV1.Finite(Set, nameof(Set));
            MatchReplayV1.Finite(Attack, nameof(Attack));
            MatchReplayV1.Finite(Block, nameof(Block));
            MatchReplayV1.Finite(Serve, nameof(Serve));
            MatchReplayV1.Finite(Speed, nameof(Speed));
        }
    }

    [DataContract]
    public sealed class MatchReplayPlayerV1
    {
        [DataMember(Name = "playerId", Order = 1)] public string PlayerId { get; set; }
        [DataMember(Name = "prototypeId", Order = 2)] public string PrototypeId { get; set; }
        [DataMember(Name = "displayName", Order = 3)] public string DisplayName { get; set; }
        [DataMember(Name = "team", Order = 4)] public string Team { get; set; }
        [DataMember(Name = "role", Order = 5)] public string Role { get; set; }
        [DataMember(Name = "rosterSlot", Order = 6)] public int RosterSlot { get; set; }
        [DataMember(Name = "ability", Order = 7)] public MatchReplayAbilityV1 Ability { get; set; }

        internal void Validate()
        {
            MatchReplayV1.Required(PlayerId, nameof(PlayerId));
            MatchReplayV1.Required(PrototypeId, nameof(PrototypeId));
            MatchReplayV1.Required(DisplayName, nameof(DisplayName));
            MatchReplayV1.Required(Team, nameof(Team));
            MatchReplayV1.Required(Role, nameof(Role));
            if (RosterSlot < 1 || RosterSlot > 6)
            {
                throw new MatchReplayValidationException("RosterSlot must be in the range [1, 6].");
            }

            if (Ability == null)
            {
                throw new MatchReplayValidationException("Ability is required.");
            }

            Ability.Validate();
        }
    }

    [DataContract]
    public sealed class MatchReplayInitialStateV1
    {
        [DataMember(Name = "homeScore", Order = 1)] public int HomeScore { get; set; }
        [DataMember(Name = "awayScore", Order = 2)] public int AwayScore { get; set; }
        [DataMember(Name = "servingTeam", Order = 3)] public string ServingTeam { get; set; }
        [DataMember(Name = "homeRotationOffset", Order = 4)] public int HomeRotationOffset { get; set; }
        [DataMember(Name = "awayRotationOffset", Order = 5)] public int AwayRotationOffset { get; set; }

        internal void Validate()
        {
            ValidateScoreAndRotation(HomeScore, AwayScore, HomeRotationOffset, AwayRotationOffset);
            MatchReplayV1.Required(ServingTeam, nameof(ServingTeam));
        }

        internal static void ValidateScoreAndRotation(int homeScore, int awayScore, int homeRotation, int awayRotation)
        {
            if (homeScore < 0 || awayScore < 0 || homeRotation < 0 || awayRotation < 0)
            {
                throw new MatchReplayValidationException("Scores and rotation offsets cannot be negative.");
            }
        }
    }

    [DataContract]
    public sealed class MatchReplayVector3V1
    {
        [DataMember(Name = "x", Order = 1)] public float X { get; set; }
        [DataMember(Name = "y", Order = 2)] public float Y { get; set; }
        [DataMember(Name = "z", Order = 3)] public float Z { get; set; }

        internal void Validate(string name)
        {
            if (this == null)
            {
                throw new MatchReplayValidationException(name + " is required.");
            }

            MatchReplayV1.Finite(X, name + ".X");
            MatchReplayV1.Finite(Y, name + ".Y");
            MatchReplayV1.Finite(Z, name + ".Z");
        }
    }

    [DataContract]
    public sealed class MatchReplayBallStateV1
    {
        [DataMember(Name = "position", Order = 1)] public MatchReplayVector3V1 Position { get; set; }
        [DataMember(Name = "velocity", Order = 2)] public MatchReplayVector3V1 Velocity { get; set; }

        internal void Validate()
        {
            if (Position == null || Velocity == null)
            {
                throw new MatchReplayValidationException("Ball position and velocity are required.");
            }

            Position.Validate(nameof(Position));
            Velocity.Validate(nameof(Velocity));
        }
    }

    [DataContract]
    public sealed class MatchReplayPlayerStateV1
    {
        [DataMember(Name = "playerId", Order = 1)] public string PlayerId { get; set; }
        [DataMember(Name = "position", Order = 2)] public MatchReplayVector3V1 Position { get; set; }
        [DataMember(Name = "yawDegrees", Order = 3)] public float YawDegrees { get; set; }
        [DataMember(Name = "scheduledAction", Order = 4)] public string ScheduledAction { get; set; }
        [DataMember(Name = "movementTarget", Order = 5)] public MatchReplayVector3V1 MovementTarget { get; set; }

        internal void Validate(ISet<string> playerIds)
        {
            MatchReplayV1.Required(PlayerId, nameof(PlayerId));
            MatchReplayV1.OptionalPlayer(playerIds, PlayerId, nameof(PlayerId));
            if (Position == null || MovementTarget == null)
            {
                throw new MatchReplayValidationException("Player position and movement target are required.");
            }

            Position.Validate(nameof(Position));
            MovementTarget.Validate(nameof(MovementTarget));
            MatchReplayV1.Finite(YawDegrees, nameof(YawDegrees));
            MatchReplayV1.Required(ScheduledAction, nameof(ScheduledAction));
        }
    }

    [DataContract]
    public sealed class MatchReplaySnapshotV1
    {
        [DataMember(Name = "simulationTimeSeconds", Order = 1)] public float SimulationTimeSeconds { get; set; }
        [DataMember(Name = "eventSequence", Order = 2)] public int EventSequence { get; set; }
        [DataMember(Name = "homeScore", Order = 3)] public int HomeScore { get; set; }
        [DataMember(Name = "awayScore", Order = 4)] public int AwayScore { get; set; }
        [DataMember(Name = "servingTeam", Order = 5)] public string ServingTeam { get; set; }
        [DataMember(Name = "homeRotationOffset", Order = 6)] public int HomeRotationOffset { get; set; }
        [DataMember(Name = "awayRotationOffset", Order = 7)] public int AwayRotationOffset { get; set; }
        [DataMember(Name = "rallyPhase", Order = 8)] public string RallyPhase { get; set; }
        [DataMember(Name = "possessionTeam", Order = 9)] public string PossessionTeam { get; set; }
        [DataMember(Name = "lastTouchPlayerId", Order = 10)] public string LastTouchPlayerId { get; set; }
        [DataMember(Name = "ball", Order = 11)] public MatchReplayBallStateV1 Ball { get; set; }
        [DataMember(Name = "players", Order = 12)] public List<MatchReplayPlayerStateV1> Players { get; set; } = new List<MatchReplayPlayerStateV1>();

        internal void Validate(ISet<string> playerIds)
        {
            MatchReplayV1.Finite(SimulationTimeSeconds, nameof(SimulationTimeSeconds));
            if (EventSequence < 0)
            {
                throw new MatchReplayValidationException("EventSequence cannot be negative.");
            }

            MatchReplayInitialStateV1.ValidateScoreAndRotation(HomeScore, AwayScore, HomeRotationOffset, AwayRotationOffset);
            MatchReplayV1.Required(ServingTeam, nameof(ServingTeam));
            MatchReplayV1.Required(RallyPhase, nameof(RallyPhase));
            MatchReplayV1.OptionalPlayer(playerIds, LastTouchPlayerId, nameof(LastTouchPlayerId));
            if (Ball == null)
            {
                throw new MatchReplayValidationException("Ball is required.");
            }

            Ball.Validate();
            if (Players == null || Players.Count != playerIds.Count)
            {
                throw new MatchReplayValidationException("Each snapshot must contain every replay player.");
            }

            var sampledPlayerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < Players.Count; index++)
            {
                var player = Players[index] ?? throw new MatchReplayValidationException("Snapshot players cannot contain null.");
                player.Validate(playerIds);
                if (!sampledPlayerIds.Add(player.PlayerId))
                {
                    throw new MatchReplayValidationException("Snapshot player IDs must be unique.");
                }
            }
        }
    }

    [DataContract]
    public sealed class MatchReplayCandidateScoreV1
    {
        [DataMember(Name = "playerId", Order = 1)] public string PlayerId { get; set; }
        [DataMember(Name = "isFeasible", Order = 2)] public bool IsFeasible { get; set; }
        [DataMember(Name = "exclusionReason", Order = 3)] public string ExclusionReason { get; set; }
        [DataMember(Name = "reachability", Order = 4)] public float Reachability { get; set; }
        [DataMember(Name = "nominalRole", Order = 5)] public float NominalRole { get; set; }
        [DataMember(Name = "approach", Order = 6)] public float Approach { get; set; }
        [DataMember(Name = "angle", Order = 7)] public float Angle { get; set; }
        [DataMember(Name = "technique", Order = 8)] public float Technique { get; set; }
        [DataMember(Name = "total", Order = 9)] public float Total { get; set; }

        internal void Validate(ISet<string> playerIds)
        {
            MatchReplayV1.Required(PlayerId, nameof(PlayerId));
            MatchReplayV1.OptionalPlayer(playerIds, PlayerId, nameof(PlayerId));
            MatchReplayV1.Finite(Reachability, nameof(Reachability));
            MatchReplayV1.Finite(NominalRole, nameof(NominalRole));
            MatchReplayV1.Finite(Approach, nameof(Approach));
            MatchReplayV1.Finite(Angle, nameof(Angle));
            MatchReplayV1.Finite(Technique, nameof(Technique));
            MatchReplayV1.Finite(Total, nameof(Total));
        }
    }

    [DataContract]
    public sealed class MatchReplayDecisionV1
    {
        [DataMember(Name = "stage", Order = 1)] public string Stage { get; set; }
        [DataMember(Name = "team", Order = 2)] public string Team { get; set; }
        [DataMember(Name = "action", Order = 3)] public string Action { get; set; }
        [DataMember(Name = "predictedBallTarget", Order = 4)] public MatchReplayVector3V1 PredictedBallTarget { get; set; }
        [DataMember(Name = "availableSeconds", Order = 5)] public float AvailableSeconds { get; set; }
        [DataMember(Name = "weights", Order = 6)] public Dictionary<string, float> Weights { get; set; } = new Dictionary<string, float>();
        [DataMember(Name = "selectedPlayerId", Order = 7)] public string SelectedPlayerId { get; set; }
        [DataMember(Name = "selectedAction", Order = 8)] public string SelectedAction { get; set; }
        [DataMember(Name = "candidates", Order = 9)] public List<MatchReplayCandidateScoreV1> Candidates { get; set; } = new List<MatchReplayCandidateScoreV1>();

        internal void Validate(ISet<string> playerIds)
        {
            MatchReplayV1.Required(Stage, nameof(Stage));
            MatchReplayV1.Required(Team, nameof(Team));
            MatchReplayV1.Required(Action, nameof(Action));
            MatchReplayV1.Required(SelectedPlayerId, nameof(SelectedPlayerId));
            MatchReplayV1.OptionalPlayer(playerIds, SelectedPlayerId, nameof(SelectedPlayerId));
            MatchReplayV1.Required(SelectedAction, nameof(SelectedAction));
            MatchReplayV1.Finite(AvailableSeconds, nameof(AvailableSeconds));
            if (PredictedBallTarget == null)
            {
                throw new MatchReplayValidationException("PredictedBallTarget is required.");
            }

            PredictedBallTarget.Validate(nameof(PredictedBallTarget));
            if (Weights == null || Candidates == null)
            {
                throw new MatchReplayValidationException("Decision weights and candidates are required.");
            }

            foreach (var weight in Weights)
            {
                MatchReplayV1.Required(weight.Key, "Decision weight name");
                MatchReplayV1.Finite(weight.Value, "Decision weight " + weight.Key);
            }

            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < Candidates.Count; index++)
            {
                var candidate = Candidates[index] ?? throw new MatchReplayValidationException("Decision candidates cannot contain null.");
                candidate.Validate(playerIds);
                if (!candidateIds.Add(candidate.PlayerId))
                {
                    throw new MatchReplayValidationException("Decision candidate IDs must be unique.");
                }
            }
        }
    }

    [DataContract]
    public sealed class MatchReplayEventV1
    {
        [DataMember(Name = "kind", Order = 1)] public string Kind { get; set; }
        [DataMember(Name = "simulationTimeSeconds", Order = 2)] public float SimulationTimeSeconds { get; set; }
        [DataMember(Name = "snapshotIndex", Order = 3)] public int SnapshotIndex { get; set; }
        [DataMember(Name = "team", Order = 4)] public string Team { get; set; }
        [DataMember(Name = "playerId", Order = 5)] public string PlayerId { get; set; }
        [DataMember(Name = "decision", Order = 6)] public MatchReplayDecisionV1 Decision { get; set; }

        internal void Validate(ISet<string> playerIds, int snapshotCount)
        {
            MatchReplayV1.Required(Kind, nameof(Kind));
            MatchReplayV1.Finite(SimulationTimeSeconds, nameof(SimulationTimeSeconds));
            if (SnapshotIndex < 0 || SnapshotIndex >= snapshotCount)
            {
                throw new MatchReplayValidationException("Event SnapshotIndex does not reference a snapshot.");
            }

            MatchReplayV1.Required(Team, nameof(Team));
            MatchReplayV1.OptionalPlayer(playerIds, PlayerId, nameof(PlayerId));
            if (Decision != null)
            {
                Decision.Validate(playerIds);
            }
        }
    }
}

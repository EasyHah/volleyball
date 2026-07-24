using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    /// <summary>
    /// Formal replay recorder. It records only native MatchReplayV4 evidence;
    /// prototype/legacy replay contracts are intentionally unsupported.
    /// </summary>
    public sealed class MatchReplayRecorder : MonoBehaviour
    {
        private FormalSixVsSixRallyDirector _director;
        private List<PrototypePlayerAgent> _players;
        private List<MatchReplayEventV4> _events;
        private bool _capturing;

        public bool IsComplete { get; private set; }

        public static MatchReplayRecorder Attach(
            FormalSixVsSixRallyDirector director,
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> players)
        {
            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            if (director.MatchContext == null)
            {
                throw new InvalidOperationException(
                    "The formal V4 director must be initialized before replay recording.");
            }

            if (director.V3RulesMode != V3RulesMode.Authority)
            {
                throw new InvalidOperationException(
                    "Formal V4 replay recording requires V3 rules authority.");
            }

            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            var recorder = director.gameObject.AddComponent<MatchReplayRecorder>();
            recorder.Initialize(director, players);
            return recorder;
        }

        public void StartCapture()
        {
            if (_capturing)
            {
                throw new InvalidOperationException(
                    "Replay capture is already running.");
            }

            _events = new List<MatchReplayEventV4>();
            _capturing = true;
            IsComplete = false;
        }

        public MatchReplayV4 Complete()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException(
                    "Replay capture has not completed.");
            }

            return MatchReplayV4.Create(
                ReplayId(_director.MatchContext),
                _director.MatchContext,
                _events);
        }

        private void Initialize(
            FormalSixVsSixRallyDirector director,
            IEnumerable<PrototypePlayerAgent> players)
        {
            _director = director;
            _players = new List<PrototypePlayerAgent>(
                players ?? throw new ArgumentNullException(nameof(players)));
            if (_players.Count != 12)
            {
                throw new ArgumentException(
                    "Formal V4 replay capture requires twelve players.",
                    nameof(players));
            }

            _players.Sort(
                (left, right) => string.CompareOrdinal(
                    left.StableId.Value,
                    right.StableId.Value));
            _director.ReplayContactAccepted += RecordContact;
            _director.ReplayRallyResolved += RecordResolution;
        }

        private void OnDestroy()
        {
            if (_director == null)
            {
                return;
            }

            _director.ReplayContactAccepted -= RecordContact;
            _director.ReplayRallyResolved -= RecordResolution;
        }

        private void RecordContact(ReplayContactEvent replayEvent)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            var actorId = replayEvent.PlayerId ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires an actor.");
            var actor = PlayerFor(actorId.Value);
            var envelope = actor.ScheduledExecutionEnvelopeV4 ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires the actor's consumed execution envelope.");
            var classification =
                actor.ScheduledExecutionClassificationV4 ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires the actor's actual sample classification.");
            var trajectory = _director.LastTrajectoryPredictionArtifactV4 ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires its trajectory artifact.");
            var ruleTransition = replayEvent.RuleTransition ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires its V3 rule decision.");

            _events.Add(
                new MatchReplayEventV4(
                    _events.Count,
                    replayEvent.Action.ToString(),
                    actorId.Value,
                    replayEvent.SimulationTimeSeconds,
                    _director.HomeScore,
                    _director.AwayScore,
                    ToReplayEnvelope(envelope),
                    ToReplayTrajectory(trajectory),
                    ToReplayConsumptions(actor, envelope.CandidateCategory),
                    ToReplayClassification(classification),
                    ToReplayGeometry(replayEvent.ObservedAttackGeometry),
                    new ReplayRuleDecisionRecordV4(
                        ContractVersions.MatchV3,
                        ruleTransition.Accepted,
                        ruleTransition.RejectionReason.ToString())));
        }

        private void RecordResolution(ReplayRallyResolvedEvent replayEvent)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            if (_events.Count == 0)
            {
                throw new InvalidOperationException(
                    "A formal V4 replay segment requires at least one contact event.");
            }

            _capturing = false;
            IsComplete = true;
        }

        private static ReplayExecutionEnvelopeRecordV4 ToReplayEnvelope(
            ExecutionEnvelopeV4 envelope)
        {
            return new ReplayExecutionEnvelopeRecordV4(
                envelope.Version,
                envelope.Identity,
                envelope.DerivedAttributesFingerprint,
                Sha256(ExecutionEnvelopePolicyV4.Default.ToCanonicalBytes()),
                envelope.SourceIntentIdentity,
                envelope.CandidateCategory.ToString(),
                Vector(envelope.BaselineTarget),
                Vector(envelope.BaselineVelocity),
                Vector(envelope.MaximumVelocity),
                Error(envelope.TargetError),
                Error(envelope.VelocityError),
                envelope.RequestedEffort,
                envelope.MaximumEffort,
                envelope.Sampling.SamplingKey,
                envelope.Sampling.PolicyVersion,
                envelope.Sampling.SampleCount,
                Names(envelope.Sampling.CandidateCategoryOrder),
                Names(envelope.Sampling.DegradationLadder),
                envelope.Expansion.MaximumExpansionCount,
                envelope.Expansion.AllowedExpansionCount,
                envelope.Expansion.CurrentExpansionCount,
                envelope.Expansion.PerStepExpansionFactor);
        }

        private static ReplayTrajectoryArtifactRecordV4 ToReplayTrajectory(
            BallTrajectoryPredictionArtifactV4 artifact)
        {
            var key = artifact.Key;
            return new ReplayTrajectoryArtifactRecordV4(
                artifact.ArtifactIdentity,
                artifact.PredictorSource,
                artifact.PredictorVersion,
                artifact.PredictorConfigurationHash,
                new ReplayTrajectoryCacheKeyRecordV4(
                    key.Identity,
                    key.BallStateVersion,
                    key.BallStateFingerprint,
                    key.PhysicsConfigurationHash,
                    key.SamplingKey,
                    key.PredictorVersion,
                    key.PredictorConfigurationHash,
                    key.EnvelopeIdentity,
                    ((ExecutionDegradationStepV4)key.DegradationStep)
                        .ToString()));
        }

        private static ReplaySampleClassificationRecordV4
            ToReplayClassification(
                ExecutionSampleClassificationV4 classification)
        {
            var sample = classification.Sample ??
                throw new InvalidOperationException(
                    "Formal V4 replay classification requires the actual sample.");
            return new ReplaySampleClassificationRecordV4(
                classification.Kind.ToString(),
                classification.TestedEnvelopeIdentity,
                classification.ExpandedEnvelopeIdentity ?? string.Empty,
                new ReplayActualSampleRecordV4(
                    sample.EnvelopeIdentity,
                    sample.SamplingKey,
                    sample.CandidateCategory.ToString(),
                    Vector(sample.Target),
                    Vector(sample.Velocity),
                    sample.Effort),
                Copy(classification.OffendingDimensions));
        }

        private static ReplayObservedP6GeometryRecordV4 ToReplayGeometry(
            AttackGeometryFactV3 geometry)
        {
            return geometry == null
                ? null
                : new ReplayObservedP6GeometryRecordV4(
                    geometry.Actor.Value,
                    geometry.Side.ToString(),
                    Vector(geometry.TakeoffPoint),
                    Vector(geometry.ContactPoint),
                    geometry.AttackLineDistanceFromCenter,
                    geometry.NetHeight);
        }

        private static ReplayBoundedErrorRecordV4 Error(
            BoundedErrorDistributionV4 value)
        {
            return new ReplayBoundedErrorRecordV4(
                value.Kind.ToString(),
                Vector(value.MinimumError),
                Vector(value.MaximumError));
        }

        private static ReplayVector3RecordV4 Vector(
            Volleyball.Domain.Simulation.SimVector3 value)
        {
            return new ReplayVector3RecordV4(value.X, value.Y, value.Z);
        }

        private static string[] Names<T>(IReadOnlyList<T> values)
        {
            var names = new string[values.Count];
            for (var index = 0; index < names.Length; index++)
            {
                names[index] = values[index].ToString();
            }

            return names;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            var copy = new string[values.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }

        private static IReadOnlyList<ReplayAbilityConsumptionRecordV4>
            ToReplayConsumptions(
                PrototypePlayerAgent player,
                ExecutionCandidateCategoryV4 category)
        {
            var attributes = player.Ability.Attributes;
            var fingerprint = player.Ability.Derived.ResultFingerprint;
            var records = new List<ReplayAbilityConsumptionRecordV4>(3);
            void Add(string name, float value)
            {
                records.Add(
                    new ReplayAbilityConsumptionRecordV4(
                        player.StableId.Value,
                        fingerprint,
                        name,
                        value,
                        "RuntimeRead"));
            }

            switch (category)
            {
                case ExecutionCandidateCategoryV4.Receive:
                    Add(
                        "Receive.FirstTouchControl",
                        attributes.Receive.FirstTouchControl);
                    Add("Receive.Movement", attributes.Receive.Movement);
                    break;
                case ExecutionCandidateCategoryV4.Set:
                    Add(
                        "Set.PlacementControl",
                        attributes.Set.PlacementControl);
                    Add("Set.TempoControl", attributes.Set.TempoControl);
                    Add("Set.Movement", attributes.Set.Movement);
                    break;
                case ExecutionCandidateCategoryV4.Attack:
                    Add(
                        "Attack.DirectionControl",
                        attributes.Attack.DirectionControl);
                    Add(
                        "Attack.SpeedControl",
                        attributes.Attack.SpeedControl);
                    Add(
                        "Attack.PowerCapacity",
                        attributes.Attack.PowerCapacity);
                    break;
                case ExecutionCandidateCategoryV4.Block:
                    Add("Block.Timing", attributes.Block.Timing);
                    Add("Block.HandControl", attributes.Block.HandControl);
                    Add(
                        "Block.LateralMobility",
                        attributes.Block.LateralMobility);
                    break;
                case ExecutionCandidateCategoryV4.Serve:
                    Add(
                        "Serve.DirectionControl",
                        attributes.Serve.DirectionControl);
                    Add(
                        "Serve.SpeedControl",
                        attributes.Serve.SpeedControl);
                    Add(
                        "Serve.PowerCapacity",
                        attributes.Serve.PowerCapacity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(category),
                        category,
                        null);
            }

            return records;
        }

        private PrototypePlayerAgent PlayerFor(string stablePlayerId)
        {
            for (var index = 0; index < _players.Count; index++)
            {
                if (string.Equals(
                        _players[index].StableId.Value,
                        stablePlayerId,
                        StringComparison.Ordinal))
                {
                    return _players[index];
                }
            }

            throw new InvalidOperationException(
                "Replay event references an unknown V4 player.");
        }

        private static string ReplayId(MatchContextV4 context)
        {
            return "formal-v4-" +
                context.SessionId.ToString("D", CultureInfo.InvariantCulture);
        }

        private static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var output = new StringBuilder(64);
            for (var index = 0; index < hash.Length; index++)
            {
                output.Append(
                    hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return output.ToString();
        }
    }
}

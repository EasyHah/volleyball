using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    /// <summary>V5 replay metadata binds a formal replay to the frozen attributes it consumed.</summary>
    public sealed class MatchReplayV5
    {
        private readonly string[] _derivedAttributeFingerprints;
        private readonly MatchReplayAttributeEvidenceV5[] _attributeEvidence;
        private readonly MatchReplayReportFactV1[] _reportFacts;

        private MatchReplayV5(string replayId, MatchContextV5 context,
            IReadOnlyList<string> derivedAttributeFingerprints,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence,
            IReadOnlyList<MatchReplayReportFactV1> reportFacts)
        {
            if (string.IsNullOrWhiteSpace(replayId))
            {
                throw new ContractValidationException("replayId is required.");
            }

            Context = context ?? throw new ContractValidationException("context is required.");
            Context.Validate();
            if (derivedAttributeFingerprints == null || derivedAttributeFingerprints.Count != 12)
            {
                throw new ContractValidationException("V5 replay requires twelve ordered derived fingerprints.");
            }

            _derivedAttributeFingerprints = new string[derivedAttributeFingerprints.Count];
            for (var index = 0; index < _derivedAttributeFingerprints.Length; index++)
            {
                var fingerprint = derivedAttributeFingerprints[index];
                ContractGuard.Hash(fingerprint, "derivedAttributeFingerprints");
                _derivedAttributeFingerprints[index] = fingerprint;
            }

            var expected = new List<string>(12);
            AddExpected(Context.Home, expected);
            AddExpected(Context.Away, expected);
            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(expected[index], _derivedAttributeFingerprints[index], StringComparison.Ordinal))
                {
                    throw new ContractValidationException("V5 replay attributes do not match its frozen context.");
                }
            }

            if (attributeEvidence == null)
            {
                throw new ContractValidationException("V5 replay attribute evidence is required.");
            }

            _attributeEvidence = new MatchReplayAttributeEvidenceV5[attributeEvidence.Count];
            for (var index = 0; index < _attributeEvidence.Length; index++)
            {
                var evidence = attributeEvidence[index] ??
                    throw new ContractValidationException("V5 replay evidence cannot contain null.");
                if (evidence.SequenceNumber != index)
                {
                    throw new ContractValidationException("V5 replay evidence must have contiguous sequence numbers.");
                }

                var expectedFingerprint = FindFingerprint(Context, evidence.PlayerId);
                if (!string.Equals(expectedFingerprint, evidence.DerivedAttributesFingerprint,
                        StringComparison.Ordinal))
                {
                    throw new ContractValidationException(
                        "V5 replay evidence must consume the actor's frozen derived attributes.");
                }

                _attributeEvidence[index] = evidence;
            }

            if (reportFacts == null) throw new ContractValidationException("V5 replay report facts are required.");
            _reportFacts = new MatchReplayReportFactV1[reportFacts.Count];
            var consumedAttributeEvidence = new HashSet<int>();
            for (var index = 0; index < _reportFacts.Length; index++)
            {
                var fact = reportFacts[index] ?? throw new ContractValidationException("V5 replay report facts cannot contain null.");
                if (fact.SequenceNumber != index || !ContainsPlayer(Context, fact.PlayerId))
                    throw new ContractValidationException("V5 replay report facts must be ordered context facts.");
                ValidateReportFact(fact, _reportFacts, _attributeEvidence, index);
                if (fact.Kind == "Contact" && !consumedAttributeEvidence.Add(fact.AttributeEvidenceSequenceNumber))
                    throw new ContractValidationException("V5 replay attribute evidence can support only one contact fact.");
                _reportFacts[index] = fact;
            }

            FormatVersion = ContractVersions.ReplayV5;
            ReplayId = replayId;
            ReplayHash = CanonicalMatchReplayHashV5.Compute(this);
        }

        public int FormatVersion { get; }
        public string ReplayId { get; }
        public MatchContextV5 Context { get; }
        public string ContextHash => Context.ContextHash;
        public IReadOnlyList<string> DerivedAttributeFingerprints =>
            new ReadOnlyCollection<string>(_derivedAttributeFingerprints);
        public IReadOnlyList<MatchReplayAttributeEvidenceV5> AttributeEvidence =>
            new ReadOnlyCollection<MatchReplayAttributeEvidenceV5>(_attributeEvidence);
        public IReadOnlyList<MatchReplayReportFactV1> ReportFacts =>
            new ReadOnlyCollection<MatchReplayReportFactV1>(_reportFacts);
        public string ReplayHash { get; }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints,
                Array.Empty<MatchReplayAttributeEvidenceV5>(), Array.Empty<MatchReplayReportFactV1>());
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints, attributeEvidence,
                Array.Empty<MatchReplayReportFactV1>());
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence,
            IReadOnlyList<MatchReplayReportFactV1> reportFacts)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints, attributeEvidence, reportFacts);
        }

        private static void AddExpected(TeamSnapshotV5 team, ICollection<string> output)
        {
            foreach (var player in team.RotationOrder) output.Add(player.Derived.ResultFingerprint);
        }

        private static string FindFingerprint(MatchContextV5 context, PlayerId playerId)
        {
            foreach (var player in context.Home.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            foreach (var player in context.Away.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            throw new ContractValidationException("V5 replay evidence actor is absent from its context.");
        }

        private static bool ContainsPlayer(MatchContextV5 context, PlayerId playerId)
        {
            try { FindFingerprint(context, playerId); return true; }
            catch (ContractValidationException) { return false; }
        }

        private static void ValidateReportFact(MatchReplayReportFactV1 fact,
            MatchReplayReportFactV1[] facts, MatchReplayAttributeEvidenceV5[] attributeEvidence,
            int factIndex)
        {
            if (fact.Kind != "Contact" && fact.Kind != "Decision" && fact.Kind != "RallyResult")
                throw new ContractValidationException("V5 replay report fact kind is unsupported.");
            if (fact.Action != "Serve" && fact.Action != "Receive" && fact.Action != "Set" &&
                fact.Action != "Attack" && fact.Action != "Block")
                throw new ContractValidationException("V5 replay report fact action is unsupported.");
            if (fact.Kind == "Contact")
            {
                if (!fact.Success || fact.ExecutableChoices != 0 ||
                    !HasMatchingAttributeEvidence(fact, attributeEvidence))
                    throw new ContractValidationException("V5 replay contact fact is inconsistent.");
                return;
            }
            if (fact.Kind == "Decision")
            {
                if (fact.ExecutableChoices < 2 || fact.WorkloadBasisPoints != 0 ||
                    fact.MovementMillimeters != 0)
                    throw new ContractValidationException("V5 replay decision fact is inconsistent.");
                return;
            }
            if (fact.ExecutableChoices != 0 || fact.WorkloadBasisPoints != 0 ||
                fact.MovementMillimeters != 0 || !HasLinkedContact(fact, facts, factIndex))
                throw new ContractValidationException("V5 replay rally-result fact lacks its proven action.");
        }

        private static bool HasLinkedContact(MatchReplayReportFactV1 fact,
            MatchReplayReportFactV1[] facts, int factIndex)
        {
            if (fact.RelatedContactSequenceNumber < 0 || fact.RelatedContactSequenceNumber >= factIndex)
                return false;
            var prior = facts[fact.RelatedContactSequenceNumber];
            return prior != null && prior.Kind == "Contact" && prior.PlayerId.Equals(fact.PlayerId) &&
                string.Equals(prior.Action, fact.Action, StringComparison.Ordinal);
        }

        private static bool HasMatchingAttributeEvidence(MatchReplayReportFactV1 fact,
            MatchReplayAttributeEvidenceV5[] attributeEvidence)
        {
            return fact.AttributeEvidenceSequenceNumber >= 0 &&
                fact.AttributeEvidenceSequenceNumber < attributeEvidence.Length &&
                attributeEvidence[fact.AttributeEvidenceSequenceNumber].PlayerId.Equals(fact.PlayerId) &&
                string.Equals(attributeEvidence[fact.AttributeEvidenceSequenceNumber].Action, fact.Action, StringComparison.Ordinal);
        }
    }

    /// <summary>Objective physical fact used by Match to aggregate a V5 Career report.</summary>
    public sealed class MatchReplayReportFactV1
    {
        public MatchReplayReportFactV1(int sequenceNumber, PlayerId playerId, string kind,
            string action, bool success, bool critical, int workloadBasisPoints,
            int movementMillimeters = 0, int executableChoices = 0,
            string selectedChoice = "", string decisionReason = "",
            int relatedContactSequenceNumber = -1, int attributeEvidenceSequenceNumber = -1)
        {
            if (sequenceNumber < 0 || workloadBasisPoints < 0 || workloadBasisPoints > 10000 || movementMillimeters < 0)
                throw new ContractValidationException("V5 replay report fact bounds are invalid.");
            if (executableChoices < 0 || (executableChoices < 2 &&
                (!string.IsNullOrEmpty(selectedChoice) || !string.IsNullOrEmpty(decisionReason))))
                throw new ContractValidationException("V5 replay decision fact evidence is invalid.");
            if (relatedContactSequenceNumber < -1)
                throw new ContractValidationException("V5 replay related contact sequence is invalid.");
            if (attributeEvidenceSequenceNumber < -1)
                throw new ContractValidationException("V5 replay attribute evidence sequence is invalid.");
            SequenceNumber = sequenceNumber;
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            Kind = ContractGuard.RequiredText(kind, nameof(kind), 32);
            Action = ContractGuard.RequiredText(action, nameof(action), 32);
            Success = success;
            Critical = critical;
            WorkloadBasisPoints = workloadBasisPoints;
            MovementMillimeters = movementMillimeters;
            ExecutableChoices = executableChoices;
            SelectedChoice = executableChoices >= 2
                ? ContractGuard.RequiredText(selectedChoice, nameof(selectedChoice), 32) : string.Empty;
            DecisionReason = executableChoices >= 2
                ? ContractGuard.RequiredText(decisionReason, nameof(decisionReason), 64) : string.Empty;
            RelatedContactSequenceNumber = relatedContactSequenceNumber;
            AttributeEvidenceSequenceNumber = attributeEvidenceSequenceNumber;
        }
        public int SequenceNumber { get; }
        public PlayerId PlayerId { get; }
        public string Kind { get; }
        public string Action { get; }
        public bool Success { get; }
        public bool Critical { get; }
        public int WorkloadBasisPoints { get; }
        public int MovementMillimeters { get; }
        public int ExecutableChoices { get; }
        public string SelectedChoice { get; }
        public string DecisionReason { get; }
        public int RelatedContactSequenceNumber { get; }
        public int AttributeEvidenceSequenceNumber { get; }
    }

    /// <summary>One accepted physical contact and the frozen derived inputs it consumed.</summary>
    public sealed class MatchReplayAttributeEvidenceV5
    {
        public MatchReplayAttributeEvidenceV5(int sequenceNumber, PlayerId playerId,
            string action, int simulationMilliseconds,
            string derivedAttributesFingerprint)
        {
            if (sequenceNumber < 0) throw new ContractValidationException("V5 replay sequence cannot be negative.");
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            Action = ContractGuard.RequiredText(action, nameof(action), 32);
            if (simulationMilliseconds < 0)
                throw new ContractValidationException("V5 replay simulation time cannot be negative.");
            ContractGuard.Hash(derivedAttributesFingerprint, nameof(derivedAttributesFingerprint));
            SequenceNumber = sequenceNumber;
            SimulationMilliseconds = simulationMilliseconds;
            DerivedAttributesFingerprint = derivedAttributesFingerprint;
        }

        public int SequenceNumber { get; }
        public PlayerId PlayerId { get; }
        public string Action { get; }
        public int SimulationMilliseconds { get; }
        public string DerivedAttributesFingerprint { get; }
    }

    public static class CanonicalMatchReplayHashV5
    {
        public static string Compute(MatchReplayV5 replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            var output = new StringBuilder("volleyball.match-replay.v5\n");
            output.Append(replay.ReplayId).Append('|').Append(replay.ContextHash);
            foreach (var fingerprint in replay.DerivedAttributeFingerprints) output.Append('|').Append(fingerprint);
            foreach (var evidence in replay.AttributeEvidence)
            {
                output.Append('|').Append(evidence.SequenceNumber).Append(':')
                    .Append(evidence.PlayerId.Value).Append(':').Append(evidence.Action)
                    .Append(':').Append(evidence.SimulationMilliseconds).Append(':')
                    .Append(evidence.DerivedAttributesFingerprint);
            }
            foreach (var fact in replay.ReportFacts)
            {
                output.Append('|').Append(fact.SequenceNumber).Append(':').Append(fact.PlayerId.Value)
                    .Append(':').Append(fact.Kind).Append(':').Append(fact.Action)
                    .Append(':').Append(fact.Success ? 1 : 0).Append(':').Append(fact.Critical ? 1 : 0)
                    .Append(':').Append(fact.WorkloadBasisPoints).Append(':').Append(fact.MovementMillimeters)
                    .Append(':').Append(fact.ExecutableChoices).Append(':').Append(fact.SelectedChoice)
                    .Append(':').Append(fact.DecisionReason).Append(':').Append(fact.RelatedContactSequenceNumber)
                    .Append(':').Append(fact.AttributeEvidenceSequenceNumber);
            }
            return CanonicalJsonHashV4.Sha256(output.ToString());
        }
    }
}

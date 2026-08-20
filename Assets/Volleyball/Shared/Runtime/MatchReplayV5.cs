using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    /// <summary>V5 replay metadata binds a formal replay to the frozen attributes it consumed.</summary>
    public sealed class MatchReplayV5
    {
        private readonly string[] _derivedAttributeFingerprints;
        private readonly MatchReplayAttributeEvidenceV5[] _attributeEvidence;
        private readonly MatchReplayReportFactV1[] _reportFacts;
        private readonly MatchPositionFaultV5[] _positionFaults;

        private MatchReplayV5(string replayId, MatchContextV5 context,
            IReadOnlyList<string> derivedAttributeFingerprints,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence,
            IReadOnlyList<MatchReplayReportFactV1> reportFacts,
            IReadOnlyList<MatchPositionFaultV5> positionFaults)
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

            _positionFaults = (positionFaults ?? throw new ContractValidationException(
                "V5 replay position-fault evidence is required.")).ToArray();
            for (var index = 0; index < _positionFaults.Length; index++)
            {
                var fault = _positionFaults[index] ?? throw new ContractValidationException(
                    "V5 replay position-fault evidence cannot contain null.");
                if (fault.RuleVersionValue != ContractVersions.PositionFaultEvidenceV5)
                    throw new ContractValidationException("V5 replay position-fault evidence is outside its context.");
                fault.ValidateAgainst(Context);
                if (index > 0 && CompareFaults(_positionFaults[index - 1], fault) >= 0)
                    throw new ContractValidationException("V5 replay position-fault evidence must be in canonical order.");
            }

            FormatVersion = ContractVersions.ReplayV5;
            PositionFaultEvidenceVersion = ContractVersions.PositionFaultEvidenceV5;
            ReplayId = replayId;
            ReplayHash = CanonicalMatchReplayHashV5.Compute(this);
        }

        public int FormatVersion { get; }
        public int PositionFaultEvidenceVersion { get; }
        public string ReplayId { get; }
        public MatchContextV5 Context { get; }
        public string ContextHash => Context.ContextHash;
        public IReadOnlyList<string> DerivedAttributeFingerprints =>
            new ReadOnlyCollection<string>(_derivedAttributeFingerprints);
        public IReadOnlyList<MatchReplayAttributeEvidenceV5> AttributeEvidence =>
            new ReadOnlyCollection<MatchReplayAttributeEvidenceV5>(_attributeEvidence);
        public IReadOnlyList<MatchReplayReportFactV1> ReportFacts =>
            new ReadOnlyCollection<MatchReplayReportFactV1>(_reportFacts);
        public IReadOnlyList<MatchPositionFaultV5> PositionFaults =>
            new ReadOnlyCollection<MatchPositionFaultV5>(_positionFaults);
        public string ReplayHash { get; }

        public void ValidateAgainst(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (FormatVersion != ContractVersions.ReplayV5 ||
                PositionFaultEvidenceVersion != ContractVersions.PositionFaultEvidenceV5 ||
                !string.Equals(ContextHash, context.ContextHash, StringComparison.Ordinal) ||
                !string.Equals(ReplayHash, CanonicalMatchReplayHashV5.Compute(this), StringComparison.Ordinal))
                throw new ContractValidationException("The V5 replay does not belong to its context.");
            foreach (var fault in _positionFaults) fault.ValidateAgainst(context);
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints,
                Array.Empty<MatchReplayAttributeEvidenceV5>(), Array.Empty<MatchReplayReportFactV1>(),
                Array.Empty<MatchPositionFaultV5>());
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints, attributeEvidence,
                Array.Empty<MatchReplayReportFactV1>(), Array.Empty<MatchPositionFaultV5>());
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence,
            IReadOnlyList<MatchReplayReportFactV1> reportFacts)
        {
            return Create(replayId, context, attributeEvidence, reportFacts,
                Array.Empty<MatchPositionFaultV5>());
        }

        public static MatchReplayV5 Create(string replayId, MatchContextV5 context,
            IReadOnlyList<MatchReplayAttributeEvidenceV5> attributeEvidence,
            IReadOnlyList<MatchReplayReportFactV1> reportFacts,
            IReadOnlyList<MatchPositionFaultV5> positionFaults)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var fingerprints = new List<string>(12);
            AddExpected(context.Home, fingerprints);
            AddExpected(context.Away, fingerprints);
            return new MatchReplayV5(replayId, context, fingerprints, attributeEvidence, reportFacts,
                positionFaults);
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

        private static int CompareFaults(MatchPositionFaultV5 left, MatchPositionFaultV5 right)
        {
            var rally = left.RallyNumber.CompareTo(right.RallyNumber);
            return rally != 0 ? rally : string.CompareOrdinal(left.Rule, right.Rule);
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
            output.Append(replay.ReplayId).Append('|').Append(replay.ContextHash)
                .Append('|').Append(replay.PositionFaultEvidenceVersion);
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
            foreach (var fault in replay.PositionFaults)
            {
                output.Append('|').Append(fault.RallyNumber).Append(':').Append(fault.RuleVersionValue)
                    .Append(':').Append((int)fault.ViolatingSide).Append(':').Append((int)fault.AwardedSide)
                    .Append(':').Append((int)fault.ServingSide).Append(':').Append(fault.Rule)
                    .Append(':').Append(fault.RequiredPlayerId.Value).Append(':').Append(fault.RequiredSlot)
                    .Append(':').Append(fault.RequiredXMillimeters).Append(':').Append(fault.RequiredZMillimeters)
                    .Append(':').Append(fault.ViolatingPlayerId.Value).Append(':').Append(fault.ViolatingSlot)
                    .Append(':').Append(fault.ViolatingXMillimeters).Append(':').Append(fault.ViolatingZMillimeters);
            }
            return CanonicalJsonHashV4.Sha256(output.ToString());
        }
    }

    internal static class CanonicalMatchReplayJsonV5
    {
        public static string Serialize(MatchReplayV5 replay)
        {
            var output = new StringBuilder("{\"formatVersion\":5,\"positionFaultEvidenceVersion\":1,\"replayId\":");
            output.Append(CanonicalMatchJsonV4.Quote(replay.ReplayId)).Append(",\"contextHash\":").Append(CanonicalMatchJsonV4.Quote(replay.ContextHash));
            AppendStrings(output, "derivedAttributeFingerprints", replay.DerivedAttributeFingerprints);
            output.Append(",\"attributeEvidence\":[");
            for (var i = 0; i < replay.AttributeEvidence.Count; i++) { if (i > 0) output.Append(','); var e = replay.AttributeEvidence[i]; output.Append("{\"sequenceNumber\":").Append(e.SequenceNumber).Append(",\"playerId\":").Append(CanonicalMatchJsonV4.Quote(e.PlayerId.Value)).Append(",\"action\":").Append(CanonicalMatchJsonV4.Quote(e.Action)).Append(",\"simulationMilliseconds\":").Append(e.SimulationMilliseconds).Append(",\"derivedAttributesFingerprint\":").Append(CanonicalMatchJsonV4.Quote(e.DerivedAttributesFingerprint)).Append('}'); }
            output.Append("],\"reportFacts\":[");
            for (var i = 0; i < replay.ReportFacts.Count; i++) { if (i > 0) output.Append(','); var f = replay.ReportFacts[i]; output.Append("{\"sequenceNumber\":").Append(f.SequenceNumber).Append(",\"playerId\":").Append(CanonicalMatchJsonV4.Quote(f.PlayerId.Value)).Append(",\"kind\":").Append(CanonicalMatchJsonV4.Quote(f.Kind)).Append(",\"action\":").Append(CanonicalMatchJsonV4.Quote(f.Action)).Append(",\"success\":").Append(f.Success ? "true" : "false").Append(",\"critical\":").Append(f.Critical ? "true" : "false").Append(",\"workloadBasisPoints\":").Append(f.WorkloadBasisPoints).Append(",\"movementMillimeters\":").Append(f.MovementMillimeters).Append(",\"executableChoices\":").Append(f.ExecutableChoices).Append(",\"selectedChoice\":").Append(CanonicalMatchJsonV4.Quote(f.SelectedChoice)).Append(",\"decisionReason\":").Append(CanonicalMatchJsonV4.Quote(f.DecisionReason)).Append(",\"relatedContactSequenceNumber\":").Append(f.RelatedContactSequenceNumber).Append(",\"attributeEvidenceSequenceNumber\":").Append(f.AttributeEvidenceSequenceNumber).Append('}'); }
            output.Append("],\"positionFaults\":");
            AppendFaults(output, replay.PositionFaults);
            output.Append(",\"replayHash\":").Append(CanonicalMatchJsonV4.Quote(replay.ReplayHash)).Append('}');
            return output.ToString();
        }
        private static void AppendStrings(StringBuilder output, string name, IReadOnlyList<string> values) { output.Append(",\"").Append(name).Append("\":["); for (var i = 0; i < values.Count; i++) { if (i > 0) output.Append(','); output.Append(CanonicalMatchJsonV4.Quote(values[i])); } output.Append(']'); }
        private static void AppendFaults(StringBuilder output, IReadOnlyList<MatchPositionFaultV5> values) { output.Append('['); for (var i = 0; i < values.Count; i++) { if (i > 0) output.Append(','); var f = values[i]; output.Append("{\"rallyNumber\":").Append(f.RallyNumber).Append(",\"ruleVersion\":").Append(f.RuleVersionValue).Append(",\"violatingSide\":").Append((int)f.ViolatingSide).Append(",\"awardedSide\":").Append((int)f.AwardedSide).Append(",\"servingSide\":").Append((int)f.ServingSide).Append(",\"rule\":").Append(CanonicalMatchJsonV4.Quote(f.Rule)).Append(",\"requiredPlayerId\":").Append(CanonicalMatchJsonV4.Quote(f.RequiredPlayerId.Value)).Append(",\"requiredSlot\":").Append(f.RequiredSlot).Append(",\"requiredXMillimeters\":").Append(f.RequiredXMillimeters).Append(",\"requiredZMillimeters\":").Append(f.RequiredZMillimeters).Append(",\"violatingPlayerId\":").Append(CanonicalMatchJsonV4.Quote(f.ViolatingPlayerId.Value)).Append(",\"violatingSlot\":").Append(f.ViolatingSlot).Append(",\"violatingXMillimeters\":").Append(f.ViolatingXMillimeters).Append(",\"violatingZMillimeters\":").Append(f.ViolatingZMillimeters).Append('}'); } output.Append(']'); }
    }
}

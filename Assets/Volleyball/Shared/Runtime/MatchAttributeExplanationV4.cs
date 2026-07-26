using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchAttributeExplanationV4
    {
        public MatchAttributeExplanationV4(
            string outputName,
            IEnumerable<string> inputNames,
            IEnumerable<float> coefficients,
            float result)
        {
            if (string.IsNullOrWhiteSpace(outputName))
            {
                throw new ContractValidationException("outputName is required.");
            }

            if (inputNames == null)
            {
                throw new ContractValidationException("inputNames are required.");
            }

            if (coefficients == null)
            {
                throw new ContractValidationException("coefficients are required.");
            }

            var inputCopy = new List<string>(inputNames);
            var coefficientCopy = new List<float>(coefficients);
            if (inputCopy.Count == 0 || inputCopy.Count != coefficientCopy.Count)
            {
                throw new ContractValidationException(
                    "inputNames and coefficients must contain the same non-zero number of values.");
            }

            for (var index = 0; index < inputCopy.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(inputCopy[index]))
                {
                    throw new ContractValidationException("inputNames cannot contain empty values.");
                }

                if (float.IsNaN(coefficientCopy[index]) || float.IsInfinity(coefficientCopy[index]))
                {
                    throw new ContractValidationException("coefficients must be finite.");
                }
            }

            if (float.IsNaN(result) || float.IsInfinity(result))
            {
                throw new ContractValidationException("result must be finite.");
            }

            OutputName = outputName;
            InputNames = new ReadOnlyCollection<string>(inputCopy);
            Coefficients = new ReadOnlyCollection<float>(coefficientCopy);
            Result = result;
        }

        public string OutputName { get; }
        public IReadOnlyList<string> InputNames { get; }
        public IReadOnlyList<float> Coefficients { get; }
        public float Result { get; }
    }

    public sealed class DerivedMatchAttributesV4
    {
        private readonly byte[] _canonicalBytes;

        internal DerivedMatchAttributesV4(
            MatchAttributesV4 attributes,
            int formulaVersion,
            int coefficientVersion,
            string inputFingerprint,
            string resultFingerprint,
            IEnumerable<MatchAttributeExplanationV4> explanations,
            byte[] canonicalBytes)
        {
            Attributes = attributes ?? throw new ContractValidationException("attributes are required.");
            if (formulaVersion <= 0)
            {
                throw new ContractValidationException("formulaVersion must be positive.");
            }

            if (coefficientVersion <= 0)
            {
                throw new ContractValidationException("coefficientVersion must be positive.");
            }

            ContractGuard.Hash(inputFingerprint, nameof(inputFingerprint));
            ContractGuard.Hash(resultFingerprint, nameof(resultFingerprint));
            if (explanations == null)
            {
                throw new ContractValidationException("explanations are required.");
            }

            if (canonicalBytes == null || canonicalBytes.Length == 0)
            {
                throw new ContractValidationException("canonicalBytes are required.");
            }

            FormulaVersion = formulaVersion;
            CoefficientVersion = coefficientVersion;
            InputFingerprint = inputFingerprint;
            ResultFingerprint = resultFingerprint;
            Explanations = new ReadOnlyCollection<MatchAttributeExplanationV4>(
                new List<MatchAttributeExplanationV4>(explanations));
            _canonicalBytes = (byte[])canonicalBytes.Clone();
        }

        public MatchAttributesV4 Attributes { get; }
        public int FormulaVersion { get; }
        public int CoefficientVersion { get; }
        public string InputFingerprint { get; }
        public string ResultFingerprint { get; }
        public IReadOnlyList<MatchAttributeExplanationV4> Explanations { get; }

        public byte[] ToCanonicalBytes()
        {
            return (byte[])_canonicalBytes.Clone();
        }
    }
}

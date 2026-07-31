using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchAttributeExplanationV5
    {
        private readonly string[] _sourceFields;

        internal MatchAttributeExplanationV5(string outputField, params string[] sourceFields)
        {
            OutputField = ContractGuard.RequiredText(outputField, nameof(outputField), 100);
            if (sourceFields == null || sourceFields.Length == 0)
            {
                throw new ContractValidationException("sourceFields are required.");
            }

            _sourceFields = new string[sourceFields.Length];
            for (var index = 0; index < sourceFields.Length; index++)
            {
                _sourceFields[index] = ContractGuard.RequiredText(
                    sourceFields[index], "sourceFields", 100);
            }
        }

        public string OutputField { get; }
        public IReadOnlyList<string> SourceFields => new ReadOnlyCollection<string>(_sourceFields);
    }
}

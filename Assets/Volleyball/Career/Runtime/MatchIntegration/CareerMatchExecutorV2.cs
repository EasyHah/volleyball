using System;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerMatchExecutorV2 : ICareerMatchExecutor
    {
        private readonly IMatchRunnerV2 _runner;
        private readonly CareerMatchV2Mapper _mapper;

        public CareerMatchExecutorV2(IMatchRunnerV2 runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _mapper = new CareerMatchV2Mapper();
        }

        public CareerCanonicalMatchContext Encode(CareerMatchLaunch launch)
        {
            if (launch == null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            var context = _mapper.ToContext(launch);
            var canonicalContextUtf8 = MatchContractV2Json.SerializeContext(context);
            return new CareerCanonicalMatchContext(
                context.SessionId,
                new Sha256Digest(context.ContextHash),
                canonicalContextUtf8);
        }

        public async Task<CareerMatchExecutionOutcome> ExecuteAsync(
            CareerCanonicalMatchContext context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var decodedContext = DecodeCanonicalContext(context.CanonicalContextUtf8);
            ValidateEnvelope(context, decodedContext);
            var result = await _runner.ExecuteAsync(decodedContext, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null)
            {
                throw new InvalidOperationException("The Match V2 runner returned no result.");
            }

            result.ValidateAgainst(decodedContext);
            var canonicalResultUtf8 = MatchContractV2Json.SerializeResult(result);
            var decodedResult = DecodeCanonicalResult(canonicalResultUtf8, decodedContext);
            var facts = _mapper.ToCareerFacts(decodedResult);
            return new CareerMatchExecutionOutcome(
                context,
                new Sha256Digest(decodedResult.ResultHash),
                canonicalResultUtf8,
                facts);
        }

        public CareerMatchExecutionOutcome DecodeAndValidate(
            byte[] canonicalContextUtf8,
            byte[] canonicalResultUtf8)
        {
            if (canonicalContextUtf8 == null)
            {
                throw new ArgumentNullException(nameof(canonicalContextUtf8));
            }

            if (canonicalResultUtf8 == null)
            {
                throw new ArgumentNullException(nameof(canonicalResultUtf8));
            }

            var context = DecodeCanonicalContext(canonicalContextUtf8);
            var result = DecodeCanonicalResult(canonicalResultUtf8, context);
            var canonicalContext = new CareerCanonicalMatchContext(
                context.SessionId,
                new Sha256Digest(context.ContextHash),
                canonicalContextUtf8);
            return new CareerMatchExecutionOutcome(
                canonicalContext,
                new Sha256Digest(result.ResultHash),
                canonicalResultUtf8,
                _mapper.ToCareerFacts(result));
        }

        private static MatchContextV2 DecodeCanonicalContext(byte[] canonicalContextUtf8)
        {
            var context = MatchContractV2Json.DeserializeContext(canonicalContextUtf8);
            RequireExactCanonicalBytes(
                canonicalContextUtf8,
                MatchContractV2Json.SerializeContext(context),
                "context");
            return context;
        }

        private static MatchResultV2 DecodeCanonicalResult(
            byte[] canonicalResultUtf8,
            MatchContextV2 context)
        {
            var result = MatchContractV2Json.DeserializeResult(canonicalResultUtf8, context);
            RequireExactCanonicalBytes(
                canonicalResultUtf8,
                MatchContractV2Json.SerializeResult(result),
                "result");
            result.ValidateAgainst(context);
            return result;
        }

        private static void ValidateEnvelope(
            CareerCanonicalMatchContext envelope,
            MatchContextV2 context)
        {
            if (envelope.SessionId != context.SessionId)
            {
                throw new MatchV2ContractException(
                    "The persisted context session does not match its envelope.");
            }

            if (!string.Equals(
                    envelope.ContextDigest.Value,
                    context.ContextHash,
                    StringComparison.Ordinal))
            {
                throw new MatchV2ContractException(
                    "The persisted context semantic hash does not match its envelope.");
            }
        }

        private static void RequireExactCanonicalBytes(
            byte[] actual,
            byte[] canonical,
            string documentName)
        {
            if (actual.Length != canonical.Length)
            {
                throw new MatchV2ContractException(
                    "The persisted " + documentName + " bytes are not canonical.");
            }

            var difference = 0;
            for (var index = 0; index < actual.Length; index++)
            {
                difference |= actual[index] ^ canonical[index];
            }

            if (difference != 0)
            {
                throw new MatchV2ContractException(
                    "The persisted " + documentName + " bytes are not canonical.");
            }
        }
    }
}

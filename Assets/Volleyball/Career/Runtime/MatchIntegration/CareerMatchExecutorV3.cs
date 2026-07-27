using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerMatchExecutorV3 : ICareerMatchExecutor
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly ICareerMatchRunnerV3 _runner;
        private readonly CareerMatchV3Mapper _mapper;

        public CareerMatchExecutorV3(ICareerMatchRunnerV3 runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _mapper = new CareerMatchV3Mapper();
        }

        public CareerCanonicalMatchContext Encode(CareerMatchLaunch launch)
        {
            if (launch == null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            var context = _mapper.ToContext(launch);
            var bytes = Serialize(context);
            return new CareerCanonicalMatchContext(
                context.SessionId,
                new Sha256Digest(context.ContextHash),
                bytes);
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
            var decodedContext = DecodeContext(context.CanonicalContextUtf8);
            ValidateEnvelope(context, decodedContext);
            var result = await _runner.ExecuteAsync(decodedContext, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null)
            {
                throw new InvalidOperationException("The Match V3 runner returned no result.");
            }

            result.ValidateAgainst(decodedContext);
            var resultBytes = Serialize(result);
            var decodedResult = DecodeResult(resultBytes, decodedContext);
            return new CareerMatchExecutionOutcome(
                context,
                new Sha256Digest(decodedResult.ResultHash),
                resultBytes,
                _mapper.ToCareerFacts(decodedContext, decodedResult));
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

            var context = DecodeContext(canonicalContextUtf8);
            var result = DecodeResult(canonicalResultUtf8, context);
            var envelope = new CareerCanonicalMatchContext(
                context.SessionId,
                new Sha256Digest(context.ContextHash),
                canonicalContextUtf8);
            return new CareerMatchExecutionOutcome(
                envelope,
                new Sha256Digest(result.ResultHash),
                canonicalResultUtf8,
                _mapper.ToCareerFacts(context, result));
        }

        private static MatchContextV3 DecodeContext(byte[] bytes)
        {
            var context = ContractJson.DeserializeContextV3(StrictUtf8.GetString(bytes));
            RequireExactBytes(bytes, Serialize(context), "context");
            return context;
        }

        private static MatchResultV3 DecodeResult(byte[] bytes, MatchContextV3 context)
        {
            var result = ContractJson.DeserializeResultV3(StrictUtf8.GetString(bytes));
            RequireExactBytes(bytes, Serialize(result), "result");
            result.ValidateAgainst(context);
            return result;
        }

        private static byte[] Serialize(MatchContextV3 context)
        {
            return StrictUtf8.GetBytes(ContractJson.SerializeV3(context));
        }

        private static byte[] Serialize(MatchResultV3 result)
        {
            return StrictUtf8.GetBytes(ContractJson.SerializeV3(result));
        }

        private static void ValidateEnvelope(
            CareerCanonicalMatchContext envelope,
            MatchContextV3 context)
        {
            if (envelope.SessionId != context.SessionId ||
                !string.Equals(
                    envelope.ContextDigest.Value,
                    context.ContextHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The persisted Match V3 context contradicts its Career envelope.");
            }
        }

        private static void RequireExactBytes(
            byte[] actual,
            byte[] canonical,
            string documentName)
        {
            if (actual.Length != canonical.Length)
            {
                throw new ContractValidationException(
                    "The persisted Match V3 " + documentName + " bytes are not canonical.");
            }

            var difference = 0;
            for (var index = 0; index < actual.Length; index++)
            {
                difference |= actual[index] ^ canonical[index];
            }

            if (difference != 0)
            {
                throw new ContractValidationException(
                    "The persisted Match V3 " + documentName + " bytes are not canonical.");
            }
        }
    }
}

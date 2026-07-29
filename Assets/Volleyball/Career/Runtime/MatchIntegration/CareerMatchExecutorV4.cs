using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerMatchExecutorV4 : ICareerMatchExecutor
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly ICareerMatchRunnerV4 _runner;
        private readonly CareerMatchV4Mapper _mapper;

        public CareerMatchExecutorV4(ICareerMatchRunnerV4 runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _mapper = new CareerMatchV4Mapper();
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
                throw new InvalidOperationException("The Match V4 runner returned no result.");
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

        private static MatchContextV4 DecodeContext(byte[] bytes)
        {
            var context = ContractJson.DeserializeMatchContextV4(StrictUtf8.GetString(bytes));
            RequireExactBytes(bytes, Serialize(context), "context");
            return context;
        }

        private static MatchResultV4 DecodeResult(byte[] bytes, MatchContextV4 context)
        {
            var result = ContractJson.DeserializeMatchResultV4(StrictUtf8.GetString(bytes));
            RequireExactBytes(bytes, Serialize(result), "result");
            result.ValidateAgainst(context);
            return result;
        }

        private static byte[] Serialize(MatchContextV4 context)
        {
            return StrictUtf8.GetBytes(ContractJson.SerializeV4(context));
        }

        private static byte[] Serialize(MatchResultV4 result)
        {
            return StrictUtf8.GetBytes(ContractJson.SerializeV4(result));
        }

        private static void ValidateEnvelope(
            CareerCanonicalMatchContext envelope,
            MatchContextV4 context)
        {
            if (envelope.SessionId != context.SessionId ||
                !string.Equals(
                    envelope.ContextDigest.Value,
                    context.ContextHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The persisted Match V4 context contradicts its Career envelope.");
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
                    "The persisted Match V4 " + documentName + " bytes are not canonical.");
            }

            var difference = 0;
            for (var index = 0; index < actual.Length; index++)
            {
                difference |= actual[index] ^ canonical[index];
            }

            if (difference != 0)
            {
                throw new ContractValidationException(
                    "The persisted Match V4 " + documentName + " bytes are not canonical.");
            }
        }
    }
}

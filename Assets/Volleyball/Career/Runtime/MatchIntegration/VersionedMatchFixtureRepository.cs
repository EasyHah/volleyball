using System;
using System.Security.Cryptography;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class VersionedMatchFixtureRepository
    {
        private const string AllowedFixtureId = "fixture.career.u1w1.6v6";
        private const int AllowedFixtureVersion = 1;
        private const string AllowedContextFileSha256 =
            "a33aefaef5860e68803fa0d3910638da661e777704d9981e3ffd910719126b93";
        private const string AllowedResultFileSha256 =
            "301df25404a1358f7a56fdc22008f9f7515b3954e75296c3cf5ffe92a959ad12";

        private readonly MatchContextV2 _templateContext;
        private readonly MatchResultV2 _templateResult;

        public VersionedMatchFixtureRepository(byte[] canonicalContextBytes, byte[] canonicalResultBytes)
        {
            if (canonicalContextBytes == null)
            {
                throw new ArgumentNullException(nameof(canonicalContextBytes));
            }

            if (canonicalResultBytes == null)
            {
                throw new ArgumentNullException(nameof(canonicalResultBytes));
            }

            var contextCopy = (byte[])canonicalContextBytes.Clone();
            var resultCopy = (byte[])canonicalResultBytes.Clone();
            if (!string.Equals(
                    ComputeSha256(contextCopy), AllowedContextFileSha256, StringComparison.Ordinal) ||
                !string.Equals(
                    ComputeSha256(resultCopy), AllowedResultFileSha256, StringComparison.Ordinal))
            {
                throw new MatchV2ContractException(
                    "The supplied payload bytes are not the committed fixture authority.");
            }

            _templateContext = MatchContractV2Json.DeserializeContext(contextCopy);
            if (_templateContext.ExecutionMode != MatchExecutionModeV2.Fixture ||
                !string.Equals(_templateContext.FixtureId, AllowedFixtureId, StringComparison.Ordinal) ||
                _templateContext.FixtureVersion != AllowedFixtureVersion)
            {
                throw new MatchV2ContractException(
                    "The supplied fixture ID/version is not the committed fixture authority.");
            }

            _templateResult = MatchContractV2Json.DeserializeResult(resultCopy, _templateContext);
        }

        internal MatchFixtureDefinitionV2 GetRequired(string fixtureId, int fixtureVersion)
        {
            if (!string.Equals(_templateContext.FixtureId, fixtureId, StringComparison.Ordinal) ||
                _templateContext.FixtureVersion.Value != fixtureVersion)
            {
                throw new MatchV2ContractException("The requested fixture ID/version is not registered.");
            }

            return new MatchFixtureDefinitionV2(_templateContext, _templateResult);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(bytes);
            var characters = new char[digest.Length * 2];
            const string hexadecimal = "0123456789abcdef";
            for (var index = 0; index < digest.Length; index++)
            {
                characters[index * 2] = hexadecimal[digest[index] >> 4];
                characters[(index * 2) + 1] = hexadecimal[digest[index] & 0x0f];
            }

            return new string(characters);
        }
    }

    internal sealed class MatchFixtureDefinitionV2
    {
        public MatchFixtureDefinitionV2(MatchContextV2 context, MatchResultV2 result)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public MatchContextV2 Context { get; }

        public MatchResultV2 Result { get; }
    }
}

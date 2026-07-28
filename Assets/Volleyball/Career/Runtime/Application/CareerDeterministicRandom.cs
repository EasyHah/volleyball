using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public interface ICareerRandomDigestSource
    {
        byte[] ComputeDigest(byte[] hashInput);
    }

    public sealed class CryptographicCareerSeedSource : ICareerSeedSource
    {
        public CareerSeed GenerateSeed()
        {
            var bytes = new byte[CareerSeed.ByteLength];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return new CareerSeed(bytes);
        }
    }

    public sealed class CareerDeterministicRandom : IDeterministicCareerRandom
    {
        private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("volleyball-career-rng");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly ICareerRandomDigestSource _digestSource;

        public CareerDeterministicRandom()
            : this(new Sha256CareerRandomDigestSource())
        {
        }

        public CareerDeterministicRandom(ICareerRandomDigestSource digestSource)
        {
            _digestSource = digestSource ?? throw new ArgumentNullException(nameof(digestSource));
        }

        public byte[] EncodeHashInput(CareerRandomRequest request, long attempt)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (attempt < 0 || attempt > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attempt),
                    attempt,
                    "Attempt must fit an unsigned 32-bit integer.");
            }

            var bytes = new List<byte>(160);
            bytes.AddRange(Prefix);
            bytes.Add(0);
            bytes.Add(1);
            AddTlv(bytes, 1, request.Seed.ToBytes());
            AddTlv(bytes, 2, StrictUtf8.GetBytes(request.StreamId));
            AddTlv(bytes, 3, UInt32Bytes((uint)request.Season));
            AddTlv(bytes, 4, UInt32Bytes((uint)request.Week));
            AddTlv(bytes, 5, StrictUtf8.GetBytes(request.EntityStableId));
            AddTlv(
                bytes,
                6,
                Encoding.ASCII.GetBytes(request.OccurrenceId.Value.ToString("D").ToLowerInvariant()));
            AddTlv(bytes, 7, UInt32Bytes((uint)request.DrawIndex));
            AddTlv(bytes, 8, UInt32Bytes((uint)attempt));
            return bytes.ToArray();
        }

        public byte[] ComputeDigest(CareerRandomRequest request, long attempt)
        {
            var digest = _digestSource.ComputeDigest(EncodeHashInput(request, attempt));
            if (digest == null || digest.Length != 32)
            {
                throw new InvalidOperationException("A career random digest must contain 32 bytes.");
            }

            return (byte[])digest.Clone();
        }

        public long NextInt64(
            CareerRandomRequest request,
            long minInclusive,
            long maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "The random range must be non-empty.");
            }

            var mathematicalWidth = (decimal)maxExclusive - minInclusive;
            if (mathematicalWidth < 1 || mathematicalWidth > 4294967296m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "The random range width must be in [1, 2^32].");
            }

            var width = (ulong)mathematicalWidth;
            var remainder = ((ulong.MaxValue % width) + 1UL) % width;
            var limit = remainder == 0 ? 0 : ulong.MaxValue - remainder + 1UL;
            for (uint attempt = 0; ; attempt++)
            {
                var value = FirstUInt64(ComputeDigest(request, attempt));
                if (remainder == 0 || value < limit)
                {
                    return checked(minInclusive + (long)(value % width));
                }

                if (attempt == uint.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Career random rejection sampling exhausted every unsigned attempt.");
                }
            }
        }

        private static void AddTlv(List<byte> destination, byte tag, byte[] value)
        {
            destination.Add(tag);
            destination.AddRange(UInt32Bytes((uint)value.Length));
            destination.AddRange(value);
        }

        private static byte[] UInt32Bytes(uint value)
        {
            return new[]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            };
        }

        private static ulong FirstUInt64(byte[] digest)
        {
            ulong value = 0;
            for (var index = 0; index < 8; index++)
            {
                value = (value << 8) | digest[index];
            }

            return value;
        }

        private sealed class Sha256CareerRandomDigestSource : ICareerRandomDigestSource
        {
            public byte[] ComputeDigest(byte[] hashInput)
            {
                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(hashInput);
                }
            }
        }
    }
}

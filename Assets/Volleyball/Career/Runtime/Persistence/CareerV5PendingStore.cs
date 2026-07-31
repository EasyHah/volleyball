using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    /// <summary>
    /// Durable boundary for native V5 profiles and frozen pending contexts.
    /// Historical V2 Career documents are deliberately never read here.
    /// </summary>
    public sealed class CareerV5PendingStore
    {
        private readonly string _root;
        private readonly IAtomicFileSystem _fileSystem;

        public CareerV5PendingStore(CareerStoragePaths paths, IAtomicFileSystem fileSystem)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            _root = Path.Combine(paths.PersistentDataPath, "CareerV5");
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public void SaveProfile(CareerPlayerProfileV5 profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Write(ProfilePath(profile.PlayerId), CareerPlayerProfileV5JsonCodec.Serialize(profile));
        }

        public CareerPlayerProfileV5 LoadProfile(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            var path = ProfilePath(playerId);
            return _fileSystem.FileExists(path)
                ? CareerPlayerProfileV5JsonCodec.Deserialize(_fileSystem.ReadAllBytes(path))
                : null;
        }

        public void SavePending(Volleyball.Shared.Contracts.PlayerId playerId,
            byte[] canonicalContextUtf8)
        {
            if (canonicalContextUtf8 == null)
                throw new ArgumentNullException(nameof(canonicalContextUtf8));
            Write(PendingPath(playerId), canonicalContextUtf8);
        }

        public byte[] LoadPending(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            var path = PendingPath(playerId);
            return _fileSystem.FileExists(path)
                ? _fileSystem.ReadAllBytes(path)
                : null;
        }

        public bool DiscardPending(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            var path = PendingPath(playerId);
            if (!_fileSystem.FileExists(path)) return false;
            _fileSystem.DeleteFile(path);
            return true;
        }

        private void Write(string path, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(path);
            _fileSystem.CreateDirectory(directory);
            if (_fileSystem.FileExists(path)) _fileSystem.OverwriteFileDurably(path, bytes);
            else _fileSystem.CreateFileDurably(path, bytes);
        }

        private string ProfilePath(Volleyball.Shared.Contracts.PlayerId playerId) =>
            Path.Combine(PlayerDirectory(playerId), "profile.json");

        private string PendingPath(Volleyball.Shared.Contracts.PlayerId playerId) =>
            Path.Combine(PlayerDirectory(playerId), "pending-context.json");

        private string PlayerDirectory(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value))
                throw new ArgumentException("A V5 player ID is required.", nameof(playerId));
            return Path.Combine(_root, Sha256(playerId.Value));
        }

        private static string Sha256(string value)
        {
            using var algorithm = SHA256.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
            var output = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes) output.Append(item.ToString("x2"));
            return output.ToString();
        }
    }
}

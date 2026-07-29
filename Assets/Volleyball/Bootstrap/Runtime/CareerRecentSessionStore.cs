using System;
using System.IO;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Bootstrap
{
    /// <summary>
    /// A non-authoritative navigation hint. Corruption or write failure is always
    /// resolved by falling back to the profile hub; Career snapshots remain authoritative.
    /// </summary>
    public sealed class CareerRecentSessionStore
    {
        private readonly string _directory;
        private readonly string _path;

        public CareerRecentSessionStore(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException(
                    "A persistent data root is required.",
                    nameof(persistentDataPath));
            }

            _directory = Path.Combine(Path.GetFullPath(persistentDataPath), "CareerUi");
            _path = Path.Combine(_directory, "recent-session.v1");
        }

        public bool TryRead(out ProfileId profileId, out SaveId saveId)
        {
            profileId = default;
            saveId = default;
            try
            {
                if (!File.Exists(_path))
                {
                    return false;
                }

                var lines = File.ReadAllLines(_path, Encoding.UTF8);
                if (lines.Length != 2 ||
                    !Guid.TryParseExact(lines[0], "D", out var profile) ||
                    !Guid.TryParseExact(lines[1], "D", out var save) ||
                    profile == Guid.Empty || save == Guid.Empty)
                {
                    Clear();
                    return false;
                }

                profileId = new ProfileId(profile);
                saveId = new SaveId(save);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public bool Remember(ProfileId profileId, SaveId saveId)
        {
            if (profileId.Value == Guid.Empty || saveId.Value == Guid.Empty)
            {
                return false;
            }

            var temporaryPath = _path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(
                    temporaryPath,
                    profileId.Value.ToString("D") + "\n" + saveId.Value.ToString("D"),
                    new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    File.Replace(temporaryPath, _path, null);
                }
                else
                {
                    File.Move(temporaryPath, _path);
                }

                return true;
            }
            catch (IOException)
            {
                TryDelete(temporaryPath);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(temporaryPath);
                return false;
            }
        }

        public void Clear()
        {
            TryDelete(_path);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

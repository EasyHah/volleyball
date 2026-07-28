using System;
using System.Collections.Generic;
using System.IO;

namespace Volleyball.Career.Persistence
{
    public interface IAtomicFileLock : IDisposable
    {
        string LockFilePath { get; }
    }

    public interface IAtomicFileSystem
    {
        void CreateDirectory(string directoryPath);

        bool DirectoryExists(string directoryPath);

        bool FileExists(string filePath);

        byte[] ReadAllBytes(string filePath);

        void CreateFileDurably(string filePath, byte[] contents);

        void OverwriteFileDurably(string filePath, byte[] contents);

        void MoveFileAtomicallyWhenDestinationDoesNotExist(
            string sourceFilePath,
            string destinationFilePath);

        void ReplaceFileWithOperationBackup(
            string replacementFilePath,
            string destinationFilePath,
            string operationBackupFilePath);

        void CopyFileWhenDestinationDoesNotExist(
            string sourceFilePath,
            string destinationFilePath);

        IReadOnlyList<string> EnumerateFiles(string directoryPath);

        IReadOnlyList<string> EnumerateDirectories(string directoryPath);

        void DeleteFile(string filePath);

        IAtomicFileLock AcquireExclusiveLock(string lockFilePath);
    }

    public sealed class SystemAtomicFileSystem : IAtomicFileSystem
    {
        public void CreateDirectory(string directoryPath)
        {
            Directory.CreateDirectory(AbsoluteDirectoryPath(directoryPath, nameof(directoryPath)));
        }

        public bool DirectoryExists(string directoryPath)
        {
            return Directory.Exists(AbsoluteDirectoryPath(directoryPath, nameof(directoryPath)));
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(AbsoluteFilePath(filePath, nameof(filePath)));
        }

        public byte[] ReadAllBytes(string filePath)
        {
            return File.ReadAllBytes(AbsoluteFilePath(filePath, nameof(filePath)));
        }

        public void CreateFileDurably(string filePath, byte[] contents)
        {
            WriteDurably(filePath, contents, FileMode.CreateNew);
        }

        public void OverwriteFileDurably(string filePath, byte[] contents)
        {
            WriteDurably(filePath, contents, FileMode.Create);
        }

        public void MoveFileAtomicallyWhenDestinationDoesNotExist(
            string sourceFilePath,
            string destinationFilePath)
        {
            var source = AbsoluteFilePath(sourceFilePath, nameof(sourceFilePath));
            var destination = AbsoluteFilePath(destinationFilePath, nameof(destinationFilePath));
            EnsureDistinct(source, destination, nameof(destinationFilePath));
            EnsureSameVolume(source, destination);
            File.Move(source, destination);
        }

        public void ReplaceFileWithOperationBackup(
            string replacementFilePath,
            string destinationFilePath,
            string operationBackupFilePath)
        {
            var replacement = AbsoluteFilePath(replacementFilePath, nameof(replacementFilePath));
            var destination = AbsoluteFilePath(destinationFilePath, nameof(destinationFilePath));
            var backup = AbsoluteFilePath(operationBackupFilePath, nameof(operationBackupFilePath));

            EnsureDistinct(replacement, destination, nameof(destinationFilePath));
            EnsureDistinct(replacement, backup, nameof(operationBackupFilePath));
            EnsureDistinct(destination, backup, nameof(operationBackupFilePath));
            EnsureSameVolume(replacement, destination);
            EnsureSameVolume(destination, backup);

            if (File.Exists(backup))
            {
                throw new IOException("The operation-specific replace backup already exists: " + backup);
            }

            File.Replace(replacement, destination, backup);
        }

        public void CopyFileWhenDestinationDoesNotExist(
            string sourceFilePath,
            string destinationFilePath)
        {
            var source = AbsoluteFilePath(sourceFilePath, nameof(sourceFilePath));
            var destination = AbsoluteFilePath(destinationFilePath, nameof(destinationFilePath));
            EnsureDistinct(source, destination, nameof(destinationFilePath));
            File.Copy(source, destination, false);
        }

        public IReadOnlyList<string> EnumerateFiles(string directoryPath)
        {
            var files = Directory.GetFiles(
                AbsoluteDirectoryPath(directoryPath, nameof(directoryPath)),
                "*",
                SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            return Array.AsReadOnly(files);
        }

        public IReadOnlyList<string> EnumerateDirectories(string directoryPath)
        {
            var directories = Directory.GetDirectories(
                AbsoluteDirectoryPath(directoryPath, nameof(directoryPath)),
                "*",
                SearchOption.TopDirectoryOnly);
            Array.Sort(directories, StringComparer.Ordinal);
            return Array.AsReadOnly(directories);
        }

        public void DeleteFile(string filePath)
        {
            File.Delete(AbsoluteFilePath(filePath, nameof(filePath)));
        }

        public IAtomicFileLock AcquireExclusiveLock(string lockFilePath)
        {
            var normalizedPath = AbsoluteFilePath(lockFilePath, nameof(lockFilePath));
            FileStream stream = null;
            try
            {
                stream = new FileStream(
                    normalizedPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.WriteThrough);
                stream.Lock(0, 1);
                return new SystemAtomicFileLock(normalizedPath, stream);
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }

        private static void WriteDurably(string filePath, byte[] contents, FileMode mode)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            var normalizedPath = AbsoluteFilePath(filePath, nameof(filePath));
            using (var stream = new FileStream(
                       normalizedPath,
                       mode,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(contents, 0, contents.Length);
                stream.Flush(true);
            }
        }

        private static string AbsoluteDirectoryPath(string path, string parameterName)
        {
            return AbsolutePath(path, parameterName, false);
        }

        private static string AbsoluteFilePath(string path, string parameterName)
        {
            return AbsolutePath(path, parameterName, true);
        }

        private static string AbsolutePath(string path, string parameterName, bool requireFileName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A non-empty path is required.", parameterName);
            }

            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException("An absolute path is required.", parameterName);
            }

            var normalized = Path.GetFullPath(path);
            if (requireFileName && string.IsNullOrEmpty(Path.GetFileName(normalized)))
            {
                throw new ArgumentException("An explicit file path is required.", parameterName);
            }

            return normalized;
        }

        private static void EnsureDistinct(string left, string right, string parameterName)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Source, destination, and backup paths must be distinct.", parameterName);
            }
        }

        private static void EnsureSameVolume(string left, string right)
        {
            var leftRoot = Path.GetPathRoot(left);
            var rightRoot = Path.GetPathRoot(right);
            if (!string.Equals(leftRoot, rightRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Atomic file publication requires source and destination on the same volume.");
            }
        }

        private sealed class SystemAtomicFileLock : IAtomicFileLock
        {
            private FileStream _stream;

            public SystemAtomicFileLock(string lockFilePath, FileStream stream)
            {
                LockFilePath = lockFilePath;
                _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            public string LockFilePath { get; }

            public void Dispose()
            {
                var stream = _stream;
                if (stream == null)
                {
                    return;
                }

                _stream = null;
                try
                {
                    stream.Unlock(0, 1);
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }
    }
}

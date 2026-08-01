using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

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
            var receiptPath = SettlementReceiptPath(playerId);
            RecoverLatestReceiptForPending(playerId, receiptPath);
            if (_fileSystem.FileExists(receiptPath))
            {
                var profile = CareerPlayerProfileV5JsonCodec.Deserialize(
                    CareerV5SettlementReceiptCodec.Deserialize(_fileSystem.ReadAllBytes(receiptPath)).ProfileUtf8);
                if (!profile.PlayerId.Equals(playerId))
                    throw new FormatException("V5 settlement receipt profile does not match its storage owner.");
                return profile;
            }
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
            if (!_fileSystem.FileExists(path)) return null;
            var pending = _fileSystem.ReadAllBytes(path);
            var pendingContext = ContractJson.DeserializeMatchContextV5(Encoding.UTF8.GetString(pending));
            if (_fileSystem.FileExists(SettlementReceiptPath(playerId, pendingContext.SessionId))) return null;
            if (!_fileSystem.FileExists(SettlementReceiptPath(playerId))) return pending;
            var receipt = CareerV5SettlementReceiptCodec.Deserialize(
                _fileSystem.ReadAllBytes(SettlementReceiptPath(playerId)));
            return pendingContext.SessionId == receipt.SessionId
                ? null : pending;
        }

        private void RecoverLatestReceiptForPending(Volleyball.Shared.Contracts.PlayerId playerId,
            string latestReceiptPath)
        {
            if (_fileSystem.FileExists(latestReceiptPath)) return;
            var pendingPath = PendingPath(playerId);
            if (!_fileSystem.FileExists(pendingPath)) return;
            var context = ContractJson.DeserializeMatchContextV5(
                Encoding.UTF8.GetString(_fileSystem.ReadAllBytes(pendingPath)));
            var sessionPath = SettlementReceiptPath(playerId, context.SessionId);
            if (!_fileSystem.FileExists(sessionPath)) return;
            // The session receipt was committed before a crash interrupted latest-pointer publication.
            var receipt = CareerV5SettlementReceiptCodec.Deserialize(_fileSystem.ReadAllBytes(sessionPath));
            if (receipt.SessionId != context.SessionId)
                throw new FormatException("V5 immutable settlement receipt does not match pending context.");
            Write(latestReceiptPath, _fileSystem.ReadAllBytes(sessionPath));
        }

        public bool DiscardPending(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            var path = PendingPath(playerId);
            if (!_fileSystem.FileExists(path)) return false;
            _fileSystem.DeleteFile(path);
            return true;
        }

        public void SaveSettlement(Volleyball.Shared.Contracts.PlayerId playerId,
            CareerMatchReportV1 report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Write(SettlementPath(playerId), Encoding.UTF8.GetBytes(ContractJson.SerializeV1(report)));
        }

        public byte[] LoadSettlement(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            var receiptPath = SettlementReceiptPath(playerId);
            if (_fileSystem.FileExists(receiptPath))
                return CareerV5SettlementReceiptCodec.Deserialize(
                    _fileSystem.ReadAllBytes(receiptPath)).ReportUtf8;
            var path = SettlementPath(playerId);
            return _fileSystem.FileExists(path) ? _fileSystem.ReadAllBytes(path) : null;
        }

        public void SaveQuickTrace(Volleyball.Shared.Contracts.PlayerId playerId,
            QuickSimulationTraceV1 trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            Write(QuickTracePath(playerId), Encoding.UTF8.GetBytes(ContractJson.SerializeV1(trace)));
        }

        public void CommitSettlement(CareerPlayerProfileV5 profile, MatchContextV5 context,
            MatchResultV5 result, CareerMatchReportV1 report, QuickSimulationTraceV1 trace)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (report == null) throw new ArgumentNullException(nameof(report));
            result.ValidateAgainst(context);
            report.ValidateAgainst(context, result);
            var sessionId = context.SessionId;
            if (sessionId == Guid.Empty || report.SessionId != sessionId)
                throw new ContractValidationException("The V5 settlement report does not belong to the supplied session.");
            var containsProfile = false;
            foreach (var playerReport in report.PlayerReports)
            {
                if (playerReport.PlayerId.Equals(profile.PlayerId))
                {
                    containsProfile = true;
                    break;
                }
            }
            if (!containsProfile)
                throw new ContractValidationException("The V5 settlement report does not contain the persisted profile.");
            if (report.EvidenceKind == CareerMatchEvidenceKindV1.QuickSimulationTrace)
            {
                if (trace == null || trace.SessionId != sessionId ||
                    !string.Equals(trace.TraceHash, report.EvidenceHash, StringComparison.Ordinal))
                    throw new ContractValidationException("The V5 quick settlement trace does not bind its report.");
            }
            else if (report.EvidenceKind == CareerMatchEvidenceKindV1.PhysicalReplay)
            {
                if (trace != null)
                    throw new ContractValidationException("Physical V5 settlement cannot persist a quick trace.");
            }
            else
            {
                throw new ContractValidationException("The V5 settlement evidence kind is unsupported.");
            }
            var receipt = new CareerV5SettlementReceipt(sessionId, report.ReportHash,
                CareerPlayerProfileV5JsonCodec.Serialize(profile),
                Encoding.UTF8.GetBytes(ContractJson.SerializeV5(context)),
                Encoding.UTF8.GetBytes(ContractJson.SerializeV5(result)),
                Encoding.UTF8.GetBytes(ContractJson.SerializeV1(report)),
                trace == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(ContractJson.SerializeV1(trace)));
            var sessionPath = SettlementReceiptPath(profile.PlayerId, sessionId);
            if (_fileSystem.FileExists(sessionPath))
            {
                var existing = CareerV5SettlementReceiptCodec.Deserialize(_fileSystem.ReadAllBytes(sessionPath));
                if (!string.Equals(existing.ReportHash, report.ReportHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("The V5 settlement session was already committed with different evidence.");
                // A crash may have persisted immutable evidence before the latest pointer.
                if (!_fileSystem.FileExists(SettlementReceiptPath(profile.PlayerId)))
                    Write(SettlementReceiptPath(profile.PlayerId), _fileSystem.ReadAllBytes(sessionPath));
                return;
            }
            var bytes = CareerV5SettlementReceiptCodec.Serialize(receipt);
            // Keep immutable session evidence before advancing the recoverable latest profile.
            Write(sessionPath, bytes);
            Write(SettlementReceiptPath(profile.PlayerId), bytes);
        }

        public byte[] LoadQuickTrace(Volleyball.Shared.Contracts.PlayerId playerId)
        {
            var receiptPath = SettlementReceiptPath(playerId);
            if (_fileSystem.FileExists(receiptPath))
                return CareerV5SettlementReceiptCodec.Deserialize(
                    _fileSystem.ReadAllBytes(receiptPath)).TraceUtf8;
            var path = QuickTracePath(playerId);
            return _fileSystem.FileExists(path) ? _fileSystem.ReadAllBytes(path) : null;
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

        private string SettlementPath(Volleyball.Shared.Contracts.PlayerId playerId) =>
            Path.Combine(PlayerDirectory(playerId), "last-settlement-report.json");

        private string QuickTracePath(Volleyball.Shared.Contracts.PlayerId playerId) =>
            Path.Combine(PlayerDirectory(playerId), "last-quick-trace.json");

        private string SettlementReceiptPath(Volleyball.Shared.Contracts.PlayerId playerId) =>
            Path.Combine(PlayerDirectory(playerId), "settlement-receipt.json");

        private string SettlementReceiptPath(Volleyball.Shared.Contracts.PlayerId playerId, Guid sessionId) =>
            Path.Combine(PlayerDirectory(playerId), "settlement-receipts", sessionId.ToString("D") + ".json");

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

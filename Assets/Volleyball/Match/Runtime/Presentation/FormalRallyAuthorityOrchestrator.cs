using System;
using System.Collections.Generic;
using Volleyball.AI;
using Volleyball.Match.Domain.FullRallyV3;
using TeamId = Volleyball.Domain.Prototype.TeamId;

namespace Volleyball.Presentation
{
    /// <summary>
    /// Owns event-scoped formal authority receipts independently from Unity
    /// lifecycle and physical-event dispatch.
    /// </summary>
    public sealed class FormalRallyAuthorityOrchestrator
    {
        private readonly Dictionary<string, ReceiveOrganizationAuthorityReceipt>
            _gateHReceipts =
                new Dictionary<string, ReceiveOrganizationAuthorityReceipt>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, GateISetIntentReceiptV3>
            _gateISetIntentReceipts =
                new Dictionary<string, GateISetIntentReceiptV3>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, AttackDefenseAuthorityReceipt>
            _gateIContactReceipts =
                new Dictionary<string, AttackDefenseAuthorityReceipt>(
                    StringComparer.Ordinal);
        private long _planRevision;
        private long _sourceSequence;

        public ReceiveOrganizationAuthorityCoordinator ReceiveCoordinator
            { get; set; }
        public AttackDefenseAuthorityCoordinator AttackCoordinator
            { get; set; }
        public IDictionary<TeamId, ReceiveOrganizationAuthorityController>
            ReceiveControllers { get; } =
                new Dictionary<TeamId,
                    ReceiveOrganizationAuthorityController>();
        public IDictionary<TeamId, AttackDefenseAuthorityController>
            AttackControllers { get; } =
                new Dictionary<TeamId,
                    AttackDefenseAuthorityController>();
        public GateISetIntentPlanningResultV3 ActiveSetIntent { get; set; }
        public long CurrentPlanRevision => _planRevision;
        public long CurrentSourceSequence => _sourceSequence;

        public long NextPlanRevision()
        {
            return ++_planRevision;
        }

        public long NextSourceSequence()
        {
            return ++_sourceSequence;
        }

        public long PeekNextSourceSequence()
        {
            return _sourceSequence + 1;
        }

        public void StoreGateH(string key,
            ReceiveOrganizationAuthorityReceipt receipt)
        {
            Replace(_gateHReceipts, key, receipt);
        }

        public ReceiveOrganizationAuthorityReceipt TakeGateH(string key)
        {
            return Take(_gateHReceipts, key);
        }

        public void StoreGateISetIntent(string key,
            GateISetIntentReceiptV3 receipt)
        {
            Replace(_gateISetIntentReceipts, key, receipt);
        }

        public GateISetIntentReceiptV3 TakeGateISetIntent(string key)
        {
            return Take(_gateISetIntentReceipts, key);
        }

        public void StoreGateIContact(string key,
            AttackDefenseAuthorityReceipt receipt)
        {
            Store(_gateIContactReceipts, key, receipt,
                "Gate I contact");
        }

        public AttackDefenseAuthorityReceipt TakeGateIContact(string key)
        {
            return Take(_gateIContactReceipts, key);
        }

        public void ClearGateH()
        {
            _gateHReceipts.Clear();
        }

        public void ClearGateI()
        {
            ActiveSetIntent = null;
            _gateISetIntentReceipts.Clear();
            _gateIContactReceipts.Clear();
        }

        public void ClearGateIContacts()
        {
            _gateIContactReceipts.Clear();
        }

        private static void Store<T>(
            IDictionary<string, T> receipts,
            string key,
            T receipt,
            string authority) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    "Receipt key is required.", nameof(key));
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (receipts.ContainsKey(key))
                throw new InvalidOperationException(
                    authority +
                    " event-owned receipt cannot be overwritten.");
            receipts.Add(key, receipt);
        }

        private static void Replace<T>(
            IDictionary<string, T> receipts,
            string key,
            T receipt) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    "Receipt key is required.", nameof(key));
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            receipts[key] = receipt;
        }

        private static T Take<T>(
            IDictionary<string, T> receipts,
            string key) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    "Receipt key is required.", nameof(key));
            if (!receipts.TryGetValue(key, out var receipt))
                return null;
            receipts.Remove(key);
            return receipt;
        }
    }
}

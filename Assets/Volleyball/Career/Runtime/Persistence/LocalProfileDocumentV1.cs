using System;

namespace Volleyball.Career.Persistence
{
    [Serializable]
    public sealed class LocalPlayerProfileDocumentV1
    {
        public int schemaVersion;
        public string profileId;
        public long profileRevision;
        public string profileSnapshotHash;
        public string displayName;
        public long createdAtUtcMs;
        public long updatedAtUtcMs;
        public CareerIndexEntryDocumentV1[] careerEntries;
    }

    [Serializable]
    public sealed class CareerIndexEntryDocumentV1
    {
        public string saveId;
        public string careerName;
        public string playerDisplayName;
        public string progressionKind;
        public string phase;
        public int season;
        public int week;
        public long updatedAtUtcMs;
        public string loadability;
    }

    [Serializable]
    public sealed class LocalProfileCatalogDocumentV1
    {
        public int schemaVersion;
        public long catalogRevision;
        public string catalogHash;
        public LocalProfileCatalogEntryDocumentV1[] profiles;
    }

    [Serializable]
    public sealed class LocalProfileCatalogEntryDocumentV1
    {
        public string profileId;
        public string displayName;
        public long updatedAtUtcMs;
        public string loadability;
    }
}

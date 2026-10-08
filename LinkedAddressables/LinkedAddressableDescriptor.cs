using System;

namespace Ksp2UnityTools.LinkedAddressables
{
    [Serializable]
    public sealed class LinkedAddressableDescriptor
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string StableId;
        public string DisplayName;
        public string SourceId;
        public string CatalogId;
        public string CatalogHash;
        public string CatalogFileName;
        public string SettingsFileName;
        public string Address;
        public string PrimaryKey;
        public string InternalId;
        public string ProviderId;
        public string AssetType;
        public LinkedAddressableDependency[] Dependencies = Array.Empty<LinkedAddressableDependency>();
    }

    [Serializable]
    public sealed class LinkedAddressableDependency
    {
        public string PrimaryKey;
        public string InternalId;
        public string ProviderId;
        public string ResourceType;
    }
}

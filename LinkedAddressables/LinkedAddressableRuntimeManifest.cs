using System;
using UnityEngine;

namespace Ksp2UnityTools.LinkedAddressables
{
    public sealed class LinkedAddressableRuntimeManifest : ScriptableObject
    {
        public const string ResourcePath =
            "KSP2UnityTools/LinkedAddressableRuntimeManifest";

        public string SourceId;
        public string SourceRoot;
        public string CatalogId;
        public string CatalogHash;
        public string CatalogFileName;
        public string SettingsFileName;
        public bool CopySourceToPlayer;
        public string CopiedSourceRelativePath;
        public string StagedExternalMetadataRelativePath;
        public string ContentDirectoryName;
        public string SceneBundleFileName;
        public string AssetBundleFileName;
        public string InitialScenePath;
        public bool UseTranslatedSceneBootstrap;
        public string[] SupportBundleFileNames = Array.Empty<string>();
        public LinkedAddressableRuntimeEntry[] Entries =
            Array.Empty<LinkedAddressableRuntimeEntry>();
    }

    [Serializable]
    public sealed class LinkedAddressableRuntimeEntry
    {
        public LinkedAddressableDescriptor Descriptor;
    }
}

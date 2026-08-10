using System;
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.SDKAssets
{
    [Serializable]
    internal sealed class SDKScriptIdentityManifest
    {
        public int Version = 1;
        public List<SDKScriptIdentity> ScriptIdentities = new();
    }

    [Serializable]
    internal sealed class SDKScriptIdentity
    {
        public string SourceGuid;
        public long SourceFileId;
        public string AssemblyName;
        public string TypeName;
    }

    [Serializable]
    internal sealed class SDKAssetGenerationInfo
    {
        public int CompilerVersion;
        public List<string> GeneratedFiles = new();
    }
}

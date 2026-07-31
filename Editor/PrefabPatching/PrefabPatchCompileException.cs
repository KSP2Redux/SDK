using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.PrefabPatching;

public sealed class PrefabPatchCompileException : Exception
{
    public IReadOnlyList<string> Diagnostics { get; }

    public PrefabPatchCompileException(IEnumerable<string> diagnostics)
        : base(
            "Prefab patch compilation failed:\n- "
                + string.Join(
                    "\n- ",
                    diagnostics ?? Enumerable.Empty<string>()
                )
        )
    {
        Diagnostics = (diagnostics ?? Enumerable.Empty<string>()).ToArray();
    }
}

public sealed class PrefabPatchCompileResult
{
    public PatchManager.PrefabPatching.PrefabPatchManifest Manifest;
    public string OutputPath;
    public string VariantPath;
    public string BaseDescriptorPath;
}

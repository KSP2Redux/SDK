using System.Collections.Generic;
using System.Linq;
using ThunderKit.Core.Config;

namespace Ksp2UnityTools.Editor
{
    internal static class SdkOwnedAssemblyImportFilter
    {
        public static readonly string[] AssemblyNames =
        {
            "Redux.SDK.dll",
            "UitkForKsp2.dll",
            "uitkforksp2.controls.Runtime.dll"
        };
    }

    public sealed class SdkOwnedAssemblyBlacklistProcessor : BlacklistProcessor
    {
        public override int Priority => int.MaxValue;

        public override IEnumerable<string> Process(IEnumerable<string> blacklist)
        {
            return blacklist.Concat(SdkOwnedAssemblyImportFilter.AssemblyNames);
        }
    }

    public sealed class SdkOwnedAssemblyWhitelistProcessor : WhitelistProcessor
    {
        public override int Priority => int.MaxValue;

        public override IEnumerable<string> Process(IEnumerable<string> whitelist)
        {
            return whitelist.Except(SdkOwnedAssemblyImportFilter.AssemblyNames, System.StringComparer.OrdinalIgnoreCase);
        }
    }
}

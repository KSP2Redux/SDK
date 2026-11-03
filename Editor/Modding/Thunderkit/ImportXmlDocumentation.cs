using System.IO;
using ThunderKit.Core.Config;
using ThunderKit.Core.Data;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Imports the XML documentation the game ships beside its managed assemblies, so IDEs show it for the imported game API.
    /// </summary>
    public class ImportXmlDocumentation : OptionalExecutor
    {
        // Runs after ThunderKit's assembly import at 3_000_000, which fills the folder the docs go into.
        private const int IMPORT_PRIORITY = 2_950_000;

        /// <inheritdoc />
        public override int Priority => IMPORT_PRIORITY;

        /// <inheritdoc />
        public override string Description =>
            "Imports the game's XML documentation next to the imported game assemblies, where IDEs read it.";

        /// <inheritdoc />
        public override bool Execute()
        {
            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();

            // The assembly import names its folder after the executable as is, which is PackageFilePath rather than the lowercased PackagePath.
            string packagePath = settings.PackageFilePath;
            foreach (string xmlPath in Directory.EnumerateFiles(settings.ManagedAssembliesPath, "*.xml"))
            {
                string assemblyName = Path.GetFileNameWithoutExtension(xmlPath);
                if (!File.Exists(Path.Combine(packagePath, $"{assemblyName}.dll")))
                    continue;

                File.Copy(xmlPath, Path.Combine(packagePath, Path.GetFileName(xmlPath)), true);
            }

            return true;
        }
    }
}

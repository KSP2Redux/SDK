using System;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;
using Redux.QA;
using ThunderKit.Core.Manifests;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Manifest-scoped validation shared by mod authors and the build pipeline.
    /// </summary>
    public static class ModValidation
    {
        [MenuItem("Redux/QA/Validate Mod")]
        private static void ValidateSelected()
        {
            Manifest manifest = Selection.activeObject as Manifest;
            if (Selection.activeObject is Mod mod)
                manifest = AssetDatabase.LoadAssetAtPath<Manifest>($"{mod.Folder}/Pipelines/Build for Player Manifest.asset");
            if (manifest == null)
            {
                Debug.LogWarning("Select a Mod asset with generated pipelines, or its ThunderKit Manifest asset.");
                return;
            }
            Validate(manifest);
        }

        /// <summary>
        /// Writes a QA report for the manifest's authored content without blocking its build.
        /// </summary>
        /// <param name="manifest">The mod manifest.</param>
        public static void Validate(Manifest manifest)
        {
            try
            {
                if (manifest == null)
                    throw new ArgumentNullException(nameof(manifest));
                string[] folders = manifest.Data.OfType<TextAssets>()
                    .Where(datum => datum.possibleFolders != null)
                    .SelectMany(datum => datum.possibleFolders)
                    .Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct().ToArray();
                if (folders.Length == 0 || folders.Any(folder => !AssetDatabase.IsValidFolder(folder)))
                    throw new InvalidOperationException("The manifest needs existing TextAssets content folders. Refusing a project-wide scan.");
                int result = SharedValidatorRunner.Run("none", folders);
                if (result != 0)
                    Debug.LogWarning("Mod validation found errors or was incomplete. See the QA validator report link above. Build continues.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Mod validation could not finish: {exception}. Build continues.");
                try
                {
                    var report = new QaReport("Mod validation setup", "validator-run");
                    report.Add(new QaReportResult("qa:scope", "Mod content", "Setup", "failure", QaVerdict.Failed, exception.ToString()));
                    Debug.Log($"QA validator report: {report.Write()}");
                }
                catch (Exception outputError)
                {
                    Debug.LogWarning($"Could not write mod validation report: {outputError.Message}");
                }
            }
        }
    }
}

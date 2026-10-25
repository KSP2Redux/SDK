using System.Collections.Generic;
using System.IO;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Flags a part whose JSON sidecar is missing or names the part differently from the prefab.
    /// </summary>
    /// <remarks>
    /// The game builds the part from the JSON and finds the prefab through the JSON's
    /// <c>partName</c>, so a JSON left behind by a rename looks for a prefab address that no
    /// longer exists. Addressable keys are case-sensitive, so a name differing only in case breaks
    /// the lookup too. Renaming a part never rewrites its JSON. Only an export does, which is the
    /// fix. Expensive because it reads and parses the file.
    /// </remarks>
    public class PartJsonOutOfDateValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the JSON is missing or names another part.</summary>
        public const string Code = "PART_JSON_OUT_OF_DATE";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData part = context?.Part;
            string partName = context?.Data?.partName;
            if (part == null || string.IsNullOrWhiteSpace(partName))
                yield break;

            string prefabPath = PartPathResolver.ResolvePrefabPath(part);
            if (string.IsNullOrEmpty(prefabPath))
                yield break;

            string jsonPath = $"{Path.GetDirectoryName(prefabPath)?.Replace('\\', '/')}/{partName}.json";
            var fix = new ValidationFix("Export JSON", () => PartJsonSaver.Save(part, false));
            if (!File.Exists(jsonPath))
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"No part JSON at '{jsonPath}'. The game builds parts from it, so the part does not load until it is exported.",
                    new[] { fix });
                yield break;
            }

            string jsonName = null;
            string readError = null;
            try
            {
                jsonName = (string)JObject.Parse(File.ReadAllText(jsonPath))["data"]?["partName"];
            }
            catch (JsonException exception)
            {
                readError = exception.Message;
            }
            catch (IOException exception)
            {
                readError = exception.Message;
            }

            if (readError == null && jsonName == partName)
                yield break;

            string message = readError != null ? $"Part JSON at '{jsonPath}' could not be read: {readError}"
                : jsonName == null ? $"Part JSON at '{jsonPath}' has no partName."
                : $"Part JSON names the part '{jsonName}' but the prefab names it '{partName}'. The game uses the JSON's name, so it looks for the wrong prefab.";
            yield return new ValidationIssue(Code, ValidationSeverity.Error, message, new[] { fix });
        }
    }
}

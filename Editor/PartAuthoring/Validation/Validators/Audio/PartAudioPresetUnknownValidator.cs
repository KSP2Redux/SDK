using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Redux.Audio;
using Redux.Modules;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Audio
{
    /// <summary>
    /// Warns when a <c>Data_PartAudioPreset.Presets[].PresetId</c> references a preset that is
    /// not in <see cref="PartAudioPresetRegistry" />.
    /// </summary>
    /// <remarks>
    /// The registry is populated from addressable PartAudioPresetDefinition assets at editor
    /// load. An unknown preset ID silently fails to bind audio at runtime - the binding row
    /// exists but emits no sound. Warning rather than Error because mod-loaded presets may
    /// register after the editor's cached list was built.
    /// </remarks>
    public sealed class PartAudioPresetUnknownValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unknown preset ID.</summary>
        public const string Code = "PART_AUDIO_PRESET_UNKNOWN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
            if (modules == null)
            {
                yield break;
            }
            HashSet<string> known = null;
            foreach (var module in modules)
            {
                if (module is not Data_PartAudioPreset audio || audio.Presets == null)
                {
                    continue;
                }
                known ??= new HashSet<string>(PartAudioPresetRegistry.GetAuthoringPresetIds(), StringComparer.Ordinal);
                for (int i = 0; i < audio.Presets.Count; i++)
                {
                    var binding = audio.Presets[i];
                    if (binding == null || string.IsNullOrEmpty(binding.PresetId))
                    {
                        continue;
                    }
                    if (known.Contains(binding.PresetId))
                    {
                        continue;
                    }
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Presets[{i}].PresetId = '{binding.PresetId}' is not in PartAudioPresetRegistry. The binding row exists but emits no sound.");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    public partial class ResourceMapsWindow
    {
        private const float RESULT_IMAGE_SIZE = 320f;
        private const float CHANNEL_IMAGE_SIZE = 160f;

        // Tab backgrounds are set from code rather than USS because the shared stylesheet has no
        // class for a two-state tab, and both skins need their own pair. Read on demand rather than
        // cached in a field: a field initializer runs in the type constructor, and Unity refuses to
        // report the skin from there on an EditorWindow.
        private static Color EnabledTabColor => EditorGUIUtility.isProSkin
            ? new Color(0.36f, 0.36f, 0.36f)
            : new Color(0.87f, 0.87f, 0.87f);

        private static Color DisabledTabColor => EditorGUIUtility.isProSkin
            ? new Color(0.20f, 0.20f, 0.20f)
            : new Color(0.66f, 0.66f, 0.66f);

        private void RebuildResources()
        {
            if (_resourcesSlot == null)
                return;

            DisposeCards();
            _resourcesSlot.Clear();

            if (_serialized == null)
            {
                _resourcesSlot.Add(new HelpBox(
                    "Pick an authoring asset above, or create one with Create New Resource Map Authoring.",
                    HelpBoxMessageType.Info
                ));
                return;
            }

            _serialized.Update();
            SerializedProperty resources = _serialized.FindProperty(nameof(ResourceMapAuthoring.Resources));

            _resourcesSlot.Add(CardListSection.Build(resources, new CardListSection.Config
            {
                Title = "Resource Maps",
                AddButtonText = "+ Add",
                IdentityFieldName = nameof(ResourceMapEntry.ResourceName),
                BuildIdentityField = BuildCardTitle,
                AllowReorder = true,
                ApplyDefaultsToNew = ApplyDefaultsToNewResource,
                BuildBody = BuildResourceBody,
            }));
        }

        private void ApplyDefaultsToNewResource(SerializedProperty entry, int index)
        {
            // A brand new array element inherits whatever the previous element left in memory, so
            // every field is written rather than only the ones that differ from a fresh instance.
            entry.FindPropertyRelative(nameof(ResourceMapEntry.ResourceName)).stringValue =
                _resourceNames.Count > 0 ? _resourceNames[0] : "";
            entry.FindPropertyRelative(nameof(ResourceMapEntry.MapBrightness)).intValue = ResourceMapComposer.MIN_MAP_BRIGHTNESS;
            entry.FindPropertyRelative(nameof(ResourceMapEntry.AutoMapBrightness)).boolValue = true;
            entry.FindPropertyRelative(nameof(ResourceMapEntry.OverwriteExisting)).boolValue = false;

            SerializedProperty channels = entry.FindPropertyRelative(nameof(ResourceMapEntry.Channels));
            channels.arraySize = ResourceMapAuthoring.CHANNEL_COUNT;
            FractalNoiseChannel[] defaults = ResourceMapEntry.CreateDefaultChannels();
            for (var channel = 0; channel < ResourceMapAuthoring.CHANNEL_COUNT; channel++)
            {
                WriteChannel(channels.GetArrayElementAtIndex(channel), defaults[channel]);
            }
        }

        private void BuildResourceBody(SerializedProperty entry, VisualElement body)
        {
            var card = new ResourceCardView(this, entry, body);
            _cards.Add(card);
            // CardListSection rebuilds a card's body when the list is reordered or an element is
            // removed, which strands the old view's textures. Detaching is the one signal that
            // covers every one of those paths.
            body.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _cards.Remove(card);
                card.Dispose();
            });
        }

        private static Label BuildSectionHeader(string text)
        {
            var header = new Label(text);
            header.AddToClassList("sdk-section-header");
            return header;
        }

        // The card's title is the resource it generates, shown rather than edited: the name is
        // chosen by the Resource control inside the card, which validates it against the project's
        // resource definitions, and a second editable copy in the header could disagree with it.
        private static VisualElement BuildCardTitle(SerializedProperty nameProperty)
        {
            var title = new Label
            {
                tooltip = "The resource this map is for. Set it with the Resource field inside the card.",
            };
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.paddingLeft = 2f;

            void Refresh(SerializedProperty property)
            {
                string value = property.stringValue;
                title.text = string.IsNullOrEmpty(value) ? "(no resource)" : value;
            }

            Refresh(nameProperty);
            title.TrackPropertyValue(nameProperty, Refresh);
            return title;
        }

        /// <summary>
        /// Builds a slider bound to a float property, optionally shown in different units to the ones stored.
        /// </summary>
        /// <remarks>
        /// Only used where the range is a real bound rather than a convenient one, because a slider's
        /// input field clamps to its range and would silently cap a value the maths accepts.
        /// </remarks>
        /// <param name="owner">The property whose child is bound.</param>
        /// <param name="relativeName">Name of the child property.</param>
        /// <param name="label">Label shown beside the slider.</param>
        /// <param name="minimum">Lowest stored value the slider offers.</param>
        /// <param name="maximum">Highest stored value the slider offers.</param>
        /// <param name="displayScale">Factor between the stored value and the displayed one. 1 shows the stored value.</param>
        /// <param name="tooltip">Tooltip for the slider.</param>
        /// <param name="onChanged">Invoked after a change is written. Null when the value does not affect the previews.</param>
        /// <returns>The slider.</returns>
        private static Slider BuildFloatSlider(
            SerializedProperty owner,
            string relativeName,
            string label,
            float minimum,
            float maximum,
            float displayScale,
            string tooltip,
            Action onChanged)
        {
            SerializedProperty property = owner.FindPropertyRelative(relativeName);
            var slider = new Slider(label, minimum * displayScale, maximum * displayScale)
            {
                name = $"field-{relativeName}",
                showInputField = true,
                tooltip = tooltip,
            };
            slider.AddToClassList("sdk-field");
            slider.AddToClassList("unity-base-field__aligned");
            slider.SetValueWithoutNotify(property.floatValue * displayScale);

            slider.RegisterValueChangedCallback(evt =>
            {
                property.serializedObject.Update();
                property.floatValue = evt.newValue / displayScale;
                property.serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
            // A slider driven by hand does not raise the binding's own change event, so an undo or
            // an edit from elsewhere has to be picked up explicitly.
            slider.TrackPropertyValue(property, p => slider.SetValueWithoutNotify(p.floatValue * displayScale));
            return slider;
        }

        /// <summary>
        /// Builds a slider bound to an integer property.
        /// </summary>
        /// <param name="owner">The property whose child is bound.</param>
        /// <param name="relativeName">Name of the child property.</param>
        /// <param name="label">Label shown beside the slider.</param>
        /// <param name="minimum">Lowest value the slider offers.</param>
        /// <param name="maximum">Highest value the slider offers.</param>
        /// <param name="tooltip">Tooltip for the slider.</param>
        /// <param name="onChanged">Invoked after a change is written.</param>
        /// <returns>The slider.</returns>
        private static SliderInt BuildIntSlider(
            SerializedProperty owner,
            string relativeName,
            string label,
            int minimum,
            int maximum,
            string tooltip,
            Action onChanged)
        {
            SerializedProperty property = owner.FindPropertyRelative(relativeName);
            var slider = new SliderInt(label, minimum, maximum)
            {
                name = $"field-{relativeName}",
                showInputField = true,
                tooltip = tooltip,
            };
            slider.AddToClassList("sdk-field");
            slider.AddToClassList("unity-base-field__aligned");
            slider.SetValueWithoutNotify(property.intValue);

            slider.RegisterValueChangedCallback(evt =>
            {
                property.serializedObject.Update();
                property.intValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
            slider.TrackPropertyValue(property, p => slider.SetValueWithoutNotify(p.intValue));
            return slider;
        }

        private static void WriteChannel(SerializedProperty target, FractalNoiseChannel source)
        {
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Enabled)).boolValue = source.Enabled;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Label)).stringValue = source.Label;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Opacity)).floatValue = source.Opacity;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Basis)).enumValueIndex = (int)source.Basis;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Frequency)).floatValue = source.Frequency;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Octaves)).intValue = source.Octaves;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Lacunarity)).floatValue = source.Lacunarity;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Persistence)).floatValue = source.Persistence;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Seed)).intValue = source.Seed;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Evolution)).floatValue = source.Evolution;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.RotationLongitude)).floatValue = source.RotationLongitude;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.RotationLatitude)).floatValue = source.RotationLatitude;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.WarpEnabled)).boolValue = source.WarpEnabled;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.WarpPower)).floatValue = source.WarpPower;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.WarpFrequency)).floatValue = source.WarpFrequency;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.WarpRoughness)).intValue = source.WarpRoughness;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Contrast)).floatValue = source.Contrast;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Brightness)).floatValue = source.Brightness;
            target.FindPropertyRelative(nameof(FractalNoiseChannel.Invert)).boolValue = source.Invert;
        }

        private static FractalNoiseChannel ReadChannel(SerializedProperty source) => new()
        {
            Enabled = source.FindPropertyRelative(nameof(FractalNoiseChannel.Enabled)).boolValue,
            Label = source.FindPropertyRelative(nameof(FractalNoiseChannel.Label)).stringValue,
            Opacity = source.FindPropertyRelative(nameof(FractalNoiseChannel.Opacity)).floatValue,
            Basis = (NoiseBasis)source.FindPropertyRelative(nameof(FractalNoiseChannel.Basis)).enumValueIndex,
            Frequency = source.FindPropertyRelative(nameof(FractalNoiseChannel.Frequency)).floatValue,
            Octaves = source.FindPropertyRelative(nameof(FractalNoiseChannel.Octaves)).intValue,
            Lacunarity = source.FindPropertyRelative(nameof(FractalNoiseChannel.Lacunarity)).floatValue,
            Persistence = source.FindPropertyRelative(nameof(FractalNoiseChannel.Persistence)).floatValue,
            Seed = source.FindPropertyRelative(nameof(FractalNoiseChannel.Seed)).intValue,
            Evolution = source.FindPropertyRelative(nameof(FractalNoiseChannel.Evolution)).floatValue,
            RotationLongitude = source.FindPropertyRelative(nameof(FractalNoiseChannel.RotationLongitude)).floatValue,
            RotationLatitude = source.FindPropertyRelative(nameof(FractalNoiseChannel.RotationLatitude)).floatValue,
            WarpEnabled = source.FindPropertyRelative(nameof(FractalNoiseChannel.WarpEnabled)).boolValue,
            WarpPower = source.FindPropertyRelative(nameof(FractalNoiseChannel.WarpPower)).floatValue,
            WarpFrequency = source.FindPropertyRelative(nameof(FractalNoiseChannel.WarpFrequency)).floatValue,
            WarpRoughness = source.FindPropertyRelative(nameof(FractalNoiseChannel.WarpRoughness)).intValue,
            Contrast = source.FindPropertyRelative(nameof(FractalNoiseChannel.Contrast)).floatValue,
            Brightness = source.FindPropertyRelative(nameof(FractalNoiseChannel.Brightness)).floatValue,
            Invert = source.FindPropertyRelative(nameof(FractalNoiseChannel.Invert)).boolValue,
        };

        // A snapshot rather than the live object, so a background preview render cannot observe the
        // artist changing a slider halfway through it.
        private static ResourceMapEntry ReadEntry(SerializedProperty entry)
        {
            var snapshot = new ResourceMapEntry
            {
                ResourceName = entry.FindPropertyRelative(nameof(ResourceMapEntry.ResourceName)).stringValue,
                // Clamped on the way in because an asset authored before this field became an
                // integer deserializes as 0, which the runtime would read as its own fallback of 10.
                MapBrightness = Mathf.Clamp(
                    entry.FindPropertyRelative(nameof(ResourceMapEntry.MapBrightness)).intValue,
                    ResourceMapComposer.MIN_MAP_BRIGHTNESS,
                    ResourceMapComposer.MAX_MAP_BRIGHTNESS),
                AutoMapBrightness = entry.FindPropertyRelative(nameof(ResourceMapEntry.AutoMapBrightness)).boolValue,
                OverwriteExisting = entry.FindPropertyRelative(nameof(ResourceMapEntry.OverwriteExisting)).boolValue,
                Channels = new FractalNoiseChannel[ResourceMapAuthoring.CHANNEL_COUNT],
            };

            SerializedProperty channels = entry.FindPropertyRelative(nameof(ResourceMapEntry.Channels));
            FractalNoiseChannel[] defaults = ResourceMapEntry.CreateDefaultChannels();
            for (var channel = 0; channel < ResourceMapAuthoring.CHANNEL_COUNT; channel++)
            {
                snapshot.Channels[channel] = channel < channels.arraySize
                    ? ReadChannel(channels.GetArrayElementAtIndex(channel))
                    : defaults[channel];
            }

            return snapshot;
        }

        /// <summary>
        /// One resource's card, in three sections: what the generated definition says, the fractal
        /// noise that shapes the map, and the generate action.
        /// </summary>
        /// <remarks>
        /// The split is not only cosmetic. Only edits inside the Fractal Noise section recompute the
        /// viewers, because the definition fields and the generate options change nothing about the
        /// field being previewed and re-rendering on them would be wasted work.
        /// </remarks>
        private sealed class ResourceCardView : IDisposable
        {
            private readonly ResourceMapsWindow _window;
            private readonly SerializedProperty _entry;
            private readonly ResourceMapPreview _resultPreview;
            private readonly VisualElement _channelBody;
            private readonly List<Button> _tabs = new();

            private ResourceMapPreview _channelPreview;
            private Texture2D _biomeThumbnail;
            private int _activeChannel;
            private bool _customResource;

            public ResourceCardView(ResourceMapsWindow window, SerializedProperty entry, VisualElement body)
            {
                _window = window;
                _entry = entry;

                body.Add(BuildSectionHeader("Resource Map Definition"));
                body.Add(BuildResourceNameField());
                body.Add(BuildDefinitionField(nameof(ResourceMapEntry.AutoMapBrightness), "Auto Brightness",
                    "Recompute Map Brightness from the generated map on every generate."));
                // Bounded by the auto computation's own clamp at one end and by the runtime's
                // fallback of 10 at the other, so the slider's range is the real range.
                body.Add(BuildIntSlider(_entry, nameof(ResourceMapEntry.MapBrightness), "Map Brightness",
                    ResourceMapComposer.MIN_MAP_BRIGHTNESS, ResourceMapComposer.MAX_MAP_BRIGHTNESS,
                    "Overlay brightness written into the definition. Affects map view only, never mining yield.",
                    null));

                body.Add(BuildSectionHeader("Fractal Noise"));
                var noiseSection = new VisualElement();

                Image resultImage = BuildViewer(noiseSection, "Result", RESULT_IMAGE_SIZE,
                    "The composed density map: every enabled channel's noise, weighted by that biome's coverage in the mask.");
                _resultPreview = new ResourceMapPreview(resultImage);

                noiseSection.Add(BuildTabRow());
                _channelBody = new VisualElement();
                noiseSection.Add(_channelBody);
                body.Add(noiseSection);

                body.Add(BuildSectionHeader("Generate"));
                body.Add(BuildGenerateButton());
                PropertyField overwrite = Field(_entry, nameof(ResourceMapEntry.OverwriteExisting), "Overwrite Existing");
                overwrite.tooltip = "Skip the confirmation prompt when the output files already exist.";
                body.Add(overwrite);

                // Each noise control asks for a re-render itself rather than a single handler on the
                // section catching everything. A section-wide handler also fired for the binding
                // events raised while the channel panel is being rebuilt, which re-rendered the
                // composed map every time the artist merely switched tabs.
                BuildChannelBody();
                RequestPreviews();
            }

            // Definition fields live outside the noise section, so they need their own binding
            // rather than riding on the section's change callback.
            private PropertyField BuildDefinitionField(string relativeName, string label, string tooltip)
            {
                PropertyField field = Field(_entry, relativeName, label);
                field.tooltip = tooltip;
                return field;
            }

            private VisualElement BuildResourceNameField()
            {
                SerializedProperty nameProperty = _entry.FindPropertyRelative(nameof(ResourceMapEntry.ResourceName));

                var container = new VisualElement();
                var choices = new List<string>(_window._resourceNames) { ResourceMapCatalog.OTHER_ENTRY };
                var dropdown = new DropdownField("Resource", choices, 0)
                {
                    tooltip = "Resource this map is for. The name sets the output file names and is written into the definition, so it has to match a real ResourceDefinition.",
                };
                dropdown.AddToClassList("sdk-field");
                dropdown.AddToClassList("unity-base-field__aligned");

                var freeText = new TextField("Resource Name") { isDelayed = true };
                freeText.AddToClassList("sdk-field");
                freeText.AddToClassList("unity-base-field__aligned");

                var warning = new VisualElement();

                void Refresh()
                {
                    string current = nameProperty.stringValue ?? "";
                    bool known = _window._resourceNames.Contains(current);
                    // Sticky rather than derived from the stored name. Deriving it meant choosing
                    // Other while a known resource was stored snapped the selection straight back,
                    // which made Other impossible to select at all.
                    _customResource = _customResource || (!known && !string.IsNullOrEmpty(current));

                    dropdown.SetValueWithoutNotify(_customResource ? ResourceMapCatalog.OTHER_ENTRY : current);
                    freeText.SetValueWithoutNotify(current);
                    freeText.style.display = _customResource ? DisplayStyle.Flex : DisplayStyle.None;

                    warning.Clear();
                    if (!known && !string.IsNullOrEmpty(current))
                    {
                        warning.Add(new HelpBox(
                            $"No ResourceDefinition named '{current}'. The map will generate, but the game will not load it until ResourceDefinitions/{current}.json exists.",
                            HelpBoxMessageType.Warning
                        ));
                    }
                }

                dropdown.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == ResourceMapCatalog.OTHER_ENTRY)
                    {
                        _customResource = true;
                        Refresh();
                        freeText.Focus();
                        return;
                    }

                    _customResource = false;
                    Write(nameProperty, evt.newValue);
                    Refresh();
                });
                freeText.RegisterValueChangedCallback(evt =>
                {
                    Write(nameProperty, evt.newValue);
                    Refresh();
                });

                container.Add(dropdown);
                container.Add(freeText);
                container.Add(warning);
                Refresh();
                return container;
            }

            private static Image BuildViewer(VisualElement parent, string caption, float size, string tooltip)
            {
                var column = new VisualElement();
                column.style.marginRight = 8f;
                column.Add(new Label(caption) { tooltip = tooltip });

                var image = new Image
                {
                    scaleMode = ScaleMode.ScaleToFit,
                    tooltip = tooltip,
                };
                image.style.width = size;
                image.style.height = size;
                column.Add(image);

                parent.Add(column);
                return image;
            }

            private VisualElement BuildTabRow()
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                for (var channel = 0; channel < ResourceMapAuthoring.CHANNEL_COUNT; channel++)
                {
                    int index = channel;
                    var tab = new Button(() =>
                    {
                        _activeChannel = index;
                        BuildChannelBody();
                        RefreshTabs();
                        // Only the channel viewer. Which tab is open changes nothing about the
                        // composed map, so re-rendering it here would be wasted work.
                        RequestChannelPreview();
                    });
                    tab.style.flexGrow = 1f;
                    _tabs.Add(tab);
                    row.Add(tab);
                }

                RefreshTabs();
                return row;
            }

            private void RefreshTabs()
            {
                SerializedProperty channels = _entry.FindPropertyRelative(nameof(ResourceMapEntry.Channels));
                for (var index = 0; index < _tabs.Count; index++)
                {
                    Button tab = _tabs[index];
                    SerializedProperty channel = index < channels.arraySize
                        ? channels.GetArrayElementAtIndex(index)
                        : null;

                    string custom = channel?.FindPropertyRelative(nameof(FractalNoiseChannel.Label)).stringValue ?? "";
                    tab.text = string.IsNullOrEmpty(custom) ? ResourceMapAuthoring.CHANNEL_NAMES[index] : custom;

                    // A channel carrying none of the resource is dimmed, so which biomes this map
                    // actually reaches is readable from the tab strip without opening each one.
                    bool enabled = channel != null
                        && channel.FindPropertyRelative(nameof(FractalNoiseChannel.Enabled)).boolValue;
                    tab.style.backgroundColor = enabled ? EnabledTabColor : DisabledTabColor;

                    tab.SetEnabled(index != _activeChannel);
                    // A channel the mask leaves empty is still editable, but flagged, because
                    // nothing tuned there can reach the output.
                    bool covered = _window._fullMask == null || _window.PeakForChannel(index) > 0f;
                    tab.style.opacity = covered ? 1f : 0.5f;
                }
            }

            private void BuildChannelBody()
            {
                _channelPreview?.Dispose();
                DestroyBiomeThumbnail();
                _channelBody.Clear();

                SerializedProperty channels = _entry.FindPropertyRelative(nameof(ResourceMapEntry.Channels));
                if (_activeChannel >= channels.arraySize)
                {
                    channels.arraySize = ResourceMapAuthoring.CHANNEL_COUNT;
                    channels.serializedObject.ApplyModifiedProperties();
                }

                SerializedProperty channel = channels.GetArrayElementAtIndex(_activeChannel);
                string channelName = ResourceMapAuthoring.CHANNEL_NAMES[_activeChannel];

                var viewers = new VisualElement();
                viewers.style.flexDirection = FlexDirection.Row;

                Image biomeImage = BuildViewer(viewers, "Biome", CHANNEL_IMAGE_SIZE,
                    $"The {channelName.ToLowerInvariant()} channel of the biome mask on its own, which is where this channel's noise can reach.");
                _biomeThumbnail = _window._previewMask?.CreateChannelPreview(_activeChannel, (int)CHANNEL_IMAGE_SIZE);
                biomeImage.image = _biomeThumbnail;

                Image noiseImage = BuildViewer(viewers, "Fractal Noise", CHANNEL_IMAGE_SIZE,
                    "This channel's noise on its own, before the biome mask restricts it to that biome.");
                _channelPreview = new ResourceMapPreview(noiseImage);
                _channelBody.Add(viewers);

                PropertyField enabled = Field(channel, nameof(FractalNoiseChannel.Enabled), "Enabled");
                enabled.tooltip = "Whether this biome carries any of this resource. Off by default, so a resource starts out present nowhere.";
                _channelBody.Add(enabled);

                // Every setting below only means something once the channel is on, so the whole
                // block greys out with it rather than inviting edits that change nothing.
                var fields = new VisualElement();
                fields.Add(Field(channel, nameof(FractalNoiseChannel.Label), "Label"));
                fields.Add(BuildFloatSlider(channel, nameof(FractalNoiseChannel.Opacity), "Opacity (%)",
                    0f, 1f, 100f,
                    "How much of this resource the biome carries, from none to full.", RequestPreviews));

                fields.Add(Field(channel, nameof(FractalNoiseChannel.Basis), "Basis"));
                fields.Add(NoiseField(channel, nameof(FractalNoiseChannel.Frequency), "Frequency"));
                fields.Add(BuildIntSlider(channel, nameof(FractalNoiseChannel.Octaves), "Octaves",
                    1, 30,
                    "How many octaves are summed. LibNoise runs at most 30.", RequestPreviews));
                fields.Add(NoiseField(channel, nameof(FractalNoiseChannel.Lacunarity), "Lacunarity"));

                var basis = (NoiseBasis)channel.FindPropertyRelative(nameof(FractalNoiseChannel.Basis)).enumValueIndex;
                Slider persistence = BuildFloatSlider(channel, nameof(FractalNoiseChannel.Persistence), "Persistence",
                    0f, 1f, 1f, "", RequestPreviews);
                ApplyBasisToPersistence(persistence, basis);
                fields.Add(persistence);

                fields.Add(NoiseField(channel, nameof(FractalNoiseChannel.Seed), "Seed"));
                fields.Add(NoiseField(channel, nameof(FractalNoiseChannel.Evolution), "Evolution"));
                fields.Add(BuildFloatSlider(channel, nameof(FractalNoiseChannel.RotationLongitude), "Rotation Longitude",
                    -180f, 180f, 1f,
                    "Moves the pattern east or west, in degrees. A full turn returns to where it started.", RequestPreviews));
                fields.Add(BuildFloatSlider(channel, nameof(FractalNoiseChannel.RotationLatitude), "Rotation Latitude",
                    -180f, 180f, 1f,
                    "Tilts the pattern relative to the poles, in degrees.", RequestPreviews));

                var warp = new Foldout { text = "Domain Warp", value = false };
                warp.AddToClassList("sdk-subsection-foldout");
                warp.Add(NoiseField(channel, nameof(FractalNoiseChannel.WarpEnabled), "Enabled"));
                warp.Add(BuildFloatSlider(channel, nameof(FractalNoiseChannel.WarpPower), "Power",
                    0f, 2f, 1f,
                    "How far the turbulence pass displaces the sampling coordinate. 0 leaves it alone.", RequestPreviews));
                warp.Add(NoiseField(channel, nameof(FractalNoiseChannel.WarpFrequency), "Frequency"));
                warp.Add(BuildIntSlider(channel, nameof(FractalNoiseChannel.WarpRoughness), "Roughness",
                    1, 30,
                    "Octave count of the turbulence pass's own noise. LibNoise runs at most 30.", RequestPreviews));
                fields.Add(warp);

                fields.Add(BuildFloatSlider(channel, nameof(FractalNoiseChannel.Contrast), "Contrast",
                    0f, 4f, 1f,
                    "Gain about mid grey. 0 flattens the field, 1 leaves it unchanged, higher drives it toward a hard edge.", RequestPreviews));
                fields.Add(BuildFloatSlider(channel, nameof(FractalNoiseChannel.Brightness), "Brightness",
                    -1f, 1f, 1f,
                    "Offset added after contrast. Past plus or minus 1 the whole field clamps, so that is the useful range.", RequestPreviews));
                fields.Add(NoiseField(channel, nameof(FractalNoiseChannel.Invert), "Invert"));

                _channelBody.Add(fields);
                _channelBody.Bind(_entry.serializedObject);

                fields.SetEnabled(channel.FindPropertyRelative(nameof(FractalNoiseChannel.Enabled)).boolValue);
                OnPropertyEdited(enabled, channel.FindPropertyRelative(nameof(FractalNoiseChannel.Enabled)), property =>
                {
                    fields.SetEnabled(property.boolValue);
                    // The tab strip shades by this, so it has to follow the toggle.
                    RefreshTabs();
                    RequestPreviews();
                });

                // Basis is updated in place. Rebuilding the whole panel for it replaced the channel
                // viewer mid-edit and re-registered every handler, which is what made a basis change
                // refresh the viewer only sometimes.
                PropertyField basisField = _channelBody.Q<PropertyField>($"field-{nameof(FractalNoiseChannel.Basis)}");
                if (basisField != null)
                {
                    OnPropertyEdited(basisField, channel.FindPropertyRelative(nameof(FractalNoiseChannel.Basis)), property =>
                    {
                        ApplyBasisToPersistence(persistence, (NoiseBasis)property.enumValueIndex);
                        RequestPreviews();
                    });
                }

                // The label only names the tab, so it costs nothing to change.
                PropertyField labelField = _channelBody.Q<PropertyField>($"field-{nameof(FractalNoiseChannel.Label)}");
                if (labelField != null)
                {
                    OnPropertyEdited(labelField, channel.FindPropertyRelative(nameof(FractalNoiseChannel.Label)), _ => RefreshTabs());
                }

                // Building the panel replaces the channel viewer, so it always needs filling. The
                // composed map is unaffected by which channel is on screen and is left alone.
                RequestChannelPreview();
            }

            // RidgedMultifractal derives its octave amplitudes from spectral weights and never reads
            // Persistence, so leaving the slider live would be a lie.
            private static void ApplyBasisToPersistence(Slider persistence, NoiseBasis basis)
            {
                bool used = basis != NoiseBasis.RidgedMultifractal;
                persistence.SetEnabled(used);
                persistence.tooltip = used
                    ? "Amplitude multiplier between successive octaves."
                    : "Not used by RidgedMultifractal, which derives octave amplitudes from its own spectral weights.";
            }

            // A noise parameter that has no dedicated handler of its own. Every one of them changes
            // what both viewers show.
            private PropertyField NoiseField(SerializedProperty owner, string relativeName, string label)
            {
                PropertyField field = Field(owner, relativeName, label);
                OnPropertyEdited(field, owner.FindPropertyRelative(relativeName), _ => RequestPreviews());
                return field;
            }

            /// <summary>
            /// Registers a handler that runs only when a property's value actually changes.
            /// </summary>
            /// <remarks>
            /// A PropertyField raises its change callback when the binding first assigns a value,
            /// not only when the artist edits one. Rebuilding the channel panel therefore looked
            /// exactly like an edit, which re-rendered the composed map on every tab switch and, on
            /// the basis field, would have scheduled a rebuild that triggered itself again.
            /// Remembering the last value seen tells a bind apart from an edit.
            /// </remarks>
            /// <param name="field">The field to watch.</param>
            /// <param name="property">The property the field is bound to, read once to seed the comparison.</param>
            /// <param name="handler">Invoked with the property on a real change.</param>
            private static void OnPropertyEdited(PropertyField field, SerializedProperty property, Action<SerializedProperty> handler)
            {
                // Seeded from the property here rather than from whichever callback arrives first.
                // Waiting for the first callback lost an edit made before the binding had got round
                // to raising it, which is why a change could appear to do nothing.
                string last = DescribeValue(property);
                field.RegisterValueChangeCallback(evt =>
                {
                    SerializedProperty changed = evt.changedProperty;
                    if (changed == null)
                        return;

                    string current = DescribeValue(changed);
                    if (current == last)
                        return;

                    last = current;
                    handler(changed);
                });
            }

            // A comparable snapshot of a property's value, used only to tell one value from another.
            private static string DescribeValue(SerializedProperty property) => property.propertyType switch
            {
                SerializedPropertyType.Boolean => property.boolValue ? "true" : "false",
                SerializedPropertyType.Integer => property.intValue.ToString(CultureInfo.InvariantCulture),
                SerializedPropertyType.Float => property.floatValue.ToString("R", CultureInfo.InvariantCulture),
                SerializedPropertyType.String => property.stringValue ?? "",
                SerializedPropertyType.Enum => property.enumValueIndex.ToString(CultureInfo.InvariantCulture),
                _ => property.propertyPath,
            };

            private static PropertyField Field(SerializedProperty owner, string relativeName, string label)
            {
                var field = new PropertyField(owner.FindPropertyRelative(relativeName), label)
                {
                    name = $"field-{relativeName}",
                };
                field.AddToClassList("sdk-field");
                return field;
            }

            private Button BuildGenerateButton()
            {
                var generate = new Button(Generate);
                generate.AddToClassList("sdk-action-button");
                // Names the file stem the button is about to write, so what lands on disk is
                // readable before committing to it.
                generate.schedule.Execute(() =>
                {
                    string resource = _entry.FindPropertyRelative(nameof(ResourceMapEntry.ResourceName)).stringValue;
                    string body = _window._asset == null ? "" : _window._asset.CelestialBodyName;
                    generate.text = string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(body)
                        ? "Generate Resource Map"
                        : $"Generate {body}_{resource}";
                }).Every(250);
                return generate;
            }

            // Previews point-sample rather than supersample. They exist to judge the shape of the
            // field, and quadrupling their cost would put the refresh past the point where it still
            // reads as a response.
            private void RequestPreviews()
            {
                RequestResultPreview();
                RequestChannelPreview();
            }

            private void RequestResultPreview()
            {
                BiomeMask mask = _window._previewMask;
                if (mask == null)
                    return;

                _entry.serializedObject.Update();
                ResourceMapEntry snapshot = ReadEntry(_entry);
                int size = mask.Width;
                _resultPreview.Request(() => ResourceMapRenderer.Render(mask, snapshot, size, 1), size, Color.white);
            }

            // Kept separate from the result so switching tabs redraws only the channel being looked
            // at. The composed map does not depend on which tab is open.
            private void RequestChannelPreview()
            {
                BiomeMask mask = _window._previewMask;
                if (mask == null || _channelPreview == null)
                    return;

                _entry.serializedObject.Update();
                ResourceMapEntry snapshot = ReadEntry(_entry);
                int size = mask.Width;
                int channelIndex = _activeChannel;
                _channelPreview.Request(
                    () => ResourceMapRenderer.RenderChannel(snapshot.Channels[channelIndex], size),
                    size,
                    Color.white
                );
            }

            public void Tick()
            {
                _resultPreview.Tick();
                _channelPreview?.Tick();
            }

            private void Write(SerializedProperty property, string value)
            {
                property.serializedObject.Update();
                property.stringValue = value ?? "";
                property.serializedObject.ApplyModifiedProperties();
            }

            private void Generate()
            {
                ResourceMapAuthoring asset = _window._asset;
                BiomeMask mask = _window._fullMask;

                _entry.serializedObject.Update();
                ResourceMapEntry snapshot = ReadEntry(_entry);

                string problem = Validate(asset, mask, snapshot);
                if (problem != null)
                {
                    EditorUtility.DisplayDialog(TITLE, problem, "OK");
                    return;
                }

                ResourceMapBakeOperation.OutputPaths paths = ResourceMapBakeOperation.GetOutputPaths(
                    ResourceMapSettings.MapFolder,
                    ResourceMapSettings.DefinitionFolder,
                    asset.CelestialBodyName,
                    snapshot.ResourceName
                );

                if (!snapshot.OverwriteExisting && paths.AnyExists && !ConfirmOverwrite(paths))
                    return;

                int size = ResourceMapSettings.OutputSize;
                float[] densities;
                try
                {
                    densities = ResourceMapRenderer.Render(
                        mask,
                        snapshot,
                        size,
                        ResourceMapSettings.SUPERSAMPLE,
                        progress => !EditorUtility.DisplayCancelableProgressBar(
                            TITLE,
                            $"Rendering {asset.CelestialBodyName}_{snapshot.ResourceName}...",
                            progress
                        )
                    );
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (densities == null)
                    return;

                int brightness = snapshot.AutoMapBrightness
                    ? ResourceMapComposer.ComputeAutoMapBrightness(densities)
                    : snapshot.MapBrightness;

                try
                {
                    EditorUtility.DisplayProgressBar(TITLE, "Writing map, definition and Addressables entries...", 0.9f);
                    ResourceMapBakeOperation.Write(
                        densities,
                        size,
                        asset.CelestialBodyName,
                        snapshot.ResourceName,
                        brightness,
                        paths,
                        ResourceMapSettings.GroupName
                    );
                }
                catch (Exception ex)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog(TITLE, $"Generating failed.\n\n{ex.GetType().Name}: {ex.Message}", "OK");
                    return;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                _entry.serializedObject.Update();
                _entry.FindPropertyRelative(nameof(ResourceMapEntry.MapBrightness)).intValue = brightness;
                _entry.serializedObject.ApplyModifiedProperties();

                _window.RefreshWarnings();
                Debug.Log($"[ResourceMaps] Wrote {paths.MapPath} and {paths.DefinitionPath} at map brightness {brightness}.");
            }

            private string Validate(ResourceMapAuthoring asset, BiomeMask mask, ResourceMapEntry snapshot)
            {
                if (asset == null)
                    return "Pick or create an authoring asset first.";
                if (string.IsNullOrEmpty(asset.CelestialBodyName))
                    return "Choose a celestial body first. Its name sets the output file names and goes into the definition.";
                if (string.IsNullOrEmpty(snapshot.ResourceName))
                    return "Choose a resource for this map first.";
                if (mask == null)
                    return "Import a biome mask first. Without one there is nothing to place deposits against.";
                return null;
            }

            private static bool ConfirmOverwrite(ResourceMapBakeOperation.OutputPaths paths)
            {
                var lines = new List<string>();
                foreach (string path in new[] { paths.MapPath, paths.DefinitionPath })
                {
                    lines.Add(File.Exists(path) ? $"REPLACE  {path}" : $"Create   {path}");
                }

                return EditorUtility.DisplayDialog(
                    TITLE,
                    string.Join("\n", lines),
                    "Generate",
                    "Cancel"
                );
            }

            private void DestroyBiomeThumbnail()
            {
                if (_biomeThumbnail == null)
                    return;
                DestroyImmediate(_biomeThumbnail);
                _biomeThumbnail = null;
            }

            public void Dispose()
            {
                _resultPreview.Dispose();
                _channelPreview?.Dispose();
                _channelPreview = null;
                DestroyBiomeThumbnail();
            }
        }
    }
}

using System;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the surface authoring section tree consumed by <see cref="PQSEditor" />.
    /// </summary>
    /// <remarks>
    /// Resolves the bound <see cref="PQSData" /> and the surface material from
    /// <c>PQSData.materialSettings.surfaceMaterial</c>, then emits one foldout per logical
    /// section (mirrors PARAMS.md grouping).
    /// Section methods take only the data they need (Material, PQSData SerializedObject, or
    /// both) so a future PQSData direct-edit inspector can call the same builders. Section
    /// methods are split across SurfaceAuthoringBuilder.X.cs partial files by domain (Quality,
    /// HeightmapStack, PerBiomeLayers, SmallBiome, MaterialSections).
    /// Keyword toggles invoke a refresh callback that rebuilds the section tree so gated
    /// fields (for example the subzone mask under SUB_ZONES_ENABLED) update immediately.
    /// </remarks>
    public static partial class SurfaceAuthoringBuilder
    {
        private const string ShowReservedPrefKey = "Ksp2UnityTools.PlanetAuthoring.ShowReserved";

        /// <summary>
        /// Gets or sets whether the inspector shows shader fields declared but not yet consumed by V3.
        /// </summary>
        /// <remarks>
        /// Authoring-time preference covering Peak/cavity windows, curvature maps, and similar.
        /// Persisted in EditorPrefs across sessions. Off by default to keep the inspector lean.
        /// </remarks>
        public static bool ShowReservedPref
        {
            get => EditorPrefs.GetBool(ShowReservedPrefKey, false);
            set => EditorPrefs.SetBool(ShowReservedPrefKey, value);
        }

        /// <summary>
        /// Populates the supplied slot with the full surface authoring section tree for the given PQS.
        /// </summary>
        /// <remarks>
        /// Clears the slot first. Emits a help box and returns early if the PQS is null, has no
        /// bound <see cref="PQSData" />, or the PQSData has no surface material assigned.
        /// </remarks>
        /// <param name="slot">The container element that receives the generated section tree.</param>
        /// <param name="pqs">The PQS whose data and surface material drive the inspector.</param>
        public static void Populate(VisualElement slot, PQS pqs)
        {
            slot.Clear();

            if (pqs == null)
            {
                slot.Add(new HelpBox("PQS reference missing.", HelpBoxMessageType.Warning));
                return;
            }

            var data = pqs.data;
            if (data == null)
            {
                slot.Add(new HelpBox(
                    "Bind a PQSData asset to the Data field above to begin authoring the surface.",
                    HelpBoxMessageType.Info
                ));
                return;
            }

            var material = data.materialSettings?.surfaceMaterial;
            if (material == null)
            {
                slot.Add(new HelpBox(
                    "PQSData has no surface material assigned. Set " +
                    "PQSData.materialSettings.surfaceMaterial to the body's local-space material " +
                    "before authoring shader properties.",
                    HelpBoxMessageType.Warning
                ));
                return;
            }

            Texture2DArrayPacker.MigrateFromPackedState(data, material);

            var pqsDataSO = new SerializedObject(data);
            // The PQSData's authoring sidecar holds the small-biome and subzone-normal source textures - the runtime PQSData no longer carries them.
            PQSDataAuthoring authoring = PlanetAuthoringRegistry.Instance.GetOrCreatePQSData(data);
            var pqsDataAuthoringSO = authoring != null ? new SerializedObject(authoring) : null;

            // Self-referencing closure: refresh recurses through BuildSections, which re-emits
            // the Quality section with this same refresh hooked to its keyword toggles. The
            // null-then-assign two-step is needed because a lambda can't see itself otherwise.
            Action refresh = null;
            refresh = () =>
            {
                slot.Clear();
                BuildSections(slot, material, pqsDataSO, pqsDataAuthoringSO, data, refresh);
            };

            BuildSections(slot, material, pqsDataSO, pqsDataAuthoringSO, data, refresh);
        }

        private static void BuildSections(
            VisualElement slot,
            Material material,
            SerializedObject pqsDataSO,
            SerializedObject pqsDataAuthoringSO,
            PQSData pqsData,
            Action refresh
        )
        {
            slot.Add(BuildQualitySection(material, pqsDataSO, refresh));
            slot.Add(BuildHeightmapStackSection(pqsDataSO));
            slot.Add(BuildPoleSettingsSection(pqsDataSO));
            slot.Add(BuildScaledSpaceSection(material));
            slot.Add(BuildBiomeControlSection(material, pqsDataSO));
            slot.Add(BuildBiomeLookupBakeSection(material, pqsDataAuthoringSO, pqsData));
            slot.Add(BuildTriplanarSection(material));

            var subzonesOn = material.IsKeywordEnabled("SUB_ZONES_ENABLED");
            for (var i = 0; i < PqsAuthoringNaming.BiomeChannels.Length; i++)
                slot.Add(BuildLargeBiomeSection(material, pqsDataSO, PqsAuthoringNaming.BiomeChannels[i], i, subzonesOn));
            for (var i = 0; i < PqsAuthoringNaming.BiomeChannels.Length; i++)
                slot.Add(BuildMidBiomeSection(material, pqsDataSO, PqsAuthoringNaming.BiomeChannels[i], i, subzonesOn));

            if (subzonesOn)
            {
                for (var i = 0; i < PqsAuthoringNaming.BiomeChannels.Length; i++)
                    slot.Add(BuildSubzoneTierBiomeSection(material, pqsDataSO, pqsDataAuthoringSO, pqsData, 3, PqsAuthoringNaming.BiomeChannels[i], i));
                for (var i = 0; i < PqsAuthoringNaming.BiomeChannels.Length; i++)
                    slot.Add(BuildSubzoneTierBiomeSection(material, pqsDataSO, pqsDataAuthoringSO, pqsData, 4, PqsAuthoringNaming.BiomeChannels[i], i));
            }

            slot.Add(BuildSmallBiomeDetailSection(material, pqsDataSO, pqsDataAuthoringSO, pqsData));
            slot.Add(BuildDecalsSection(material));
            slot.Add(BuildDistanceCascadeSection(material));
            slot.Add(BuildCrossBiomeBlendSection(material));
            slot.Add(BuildMiscSection(material));
        }

        private static PropertyField BindPropertyField(SerializedObject so, string path, string label, string tooltip)
        {
            var prop = so?.FindProperty(path);
            var field = new PropertyField(prop, label) { tooltip = tooltip };
            if (prop != null)
                field.BindProperty(prop);
            return field;
        }

        private static Label GroupLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("pqs-inspector-group-label");
            return label;
        }
    }
}

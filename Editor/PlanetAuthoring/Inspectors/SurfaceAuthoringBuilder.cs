using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Builds the surface authoring sections the planet inspector's Terrain and Surface tabs show.
    /// </summary>
    /// <remarks>
    /// <see cref="TryResolve" /> finds the bound <see cref="PQSData" /> and its surface material, then each tab calls
    /// the section builders it needs. Section methods take only the data they need (Material, PQSData
    /// SerializedObject, or both). They are split across SurfaceAuthoringBuilder.X.cs partial files by domain
    /// (Quality, HeightmapStack, PerBiomeLayers, SmallBiome, MaterialSections). Keyword toggles invoke a refresh
    /// callback that rebuilds the tab, so gated fields such as the subzone mask update at once.
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
        /// The objects the surface authoring sections edit, resolved from a PQS.
        /// </summary>
        public class Inputs
        {
            /// <summary>
            /// Initializes the inputs.
            /// </summary>
            /// <param name="data">The PQS's data.</param>
            /// <param name="material">The data's surface material.</param>
            /// <param name="authoring">The data's authoring sidecar, or null.</param>
            public Inputs(PQSData data, Material material, PQSDataAuthoring authoring)
            {
                Data = data;
                Material = material;
                DataObject = new SerializedObject(data);
                AuthoringObject = authoring != null ? new SerializedObject(authoring) : null;
            }

            /// <summary>
            /// Gets the PQS's data.
            /// </summary>
            public PQSData Data { get; }

            /// <summary>
            /// Gets the surface material.
            /// </summary>
            public Material Material { get; }

            /// <summary>
            /// Gets the serialized object for <see cref="Data" />.
            /// </summary>
            public SerializedObject DataObject { get; }

            /// <summary>
            /// Gets the serialized object for the data's authoring sidecar, which holds the small-biome and
            /// subzone-normal source textures, or null when there is none.
            /// </summary>
            public SerializedObject AuthoringObject { get; }

            /// <summary>
            /// Gets a value indicating whether the surface material has sub-zones on.
            /// </summary>
            public bool SubzonesOn => Material.IsKeywordEnabled("SUB_ZONES_ENABLED");
        }

        /// <summary>
        /// Resolves the objects the surface sections edit for a PQS.
        /// </summary>
        /// <param name="pqs">The PQS.</param>
        /// <param name="inputs">Receives the inputs, or null when they cannot be resolved.</param>
        /// <returns>A help box saying what is missing, or null when <paramref name="inputs" /> was resolved.</returns>
        public static HelpBox TryResolve(PQS pqs, out Inputs inputs)
        {
            inputs = null;
            if (pqs == null)
                return new HelpBox("This body has no PQS.", HelpBoxMessageType.Warning);

            PQSData data = pqs.data;
            if (data == null)
                return new HelpBox("Bind a PQSData asset to the PQS's Data field to begin authoring the surface.", HelpBoxMessageType.Info);

            Material material = data.materialSettings?.surfaceMaterial;
            if (material == null)
            {
                return new HelpBox(
                    "PQSData has no surface material assigned. Set PQSData.materialSettings.surfaceMaterial to the body's local-space material before authoring shader properties.",
                    HelpBoxMessageType.Warning);
            }

            inputs = new Inputs(data, material, AuthoringSidecars.GetOrCreate(data));
            return null;
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

using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// The objects one planet inspector edits: the body on the Scaled prefab and the PQS on the Local prefab.
    /// </summary>
    /// <remarks>
    /// The inspector is the same whichever half of the body is selected. The selected half's serialized object is the
    /// hosting editor's own, and the other half gets a serialized object of its own here.
    /// </remarks>
    public class PlanetInspectorContext
    {
        private PlanetInspectorContext(CoreCelestialBodyData body, SerializedObject bodyObject, PQS pqs, SerializedObject pqsObject, bool isLocalSelected)
        {
            Body = body;
            BodyObject = bodyObject;
            Pqs = pqs;
            PqsObject = pqsObject;
            IsLocalSelected = isLocalSelected;
        }

        /// <summary>
        /// Gets the body's data component, on the Scaled prefab.
        /// </summary>
        public CoreCelestialBodyData Body { get; }

        /// <summary>
        /// Gets the serialized object for <see cref="Body" />.
        /// </summary>
        public SerializedObject BodyObject { get; }

        /// <summary>
        /// Gets the PQS on the Local prefab, or null for a body without one.
        /// </summary>
        public PQS Pqs { get; }

        /// <summary>
        /// Gets the serialized object for <see cref="Pqs" />, or null for a body without one.
        /// </summary>
        public SerializedObject PqsObject { get; }

        /// <summary>
        /// Gets a value indicating whether the inspector is showing on the Local prefab rather than the Scaled one.
        /// </summary>
        public bool IsLocalSelected { get; }

        /// <summary>
        /// Gets a value indicating whether the inspector shows a body the running game loaded, rather than one being
        /// authored.
        /// </summary>
        /// <remarks>
        /// A live body is for cross-checking: its fields are edited in place and lost when play stops, and the tools
        /// that write files or drive the authoring preview do not apply to it.
        /// </remarks>
        public bool IsLive => Application.isPlaying && Body != null && !EditorUtility.IsPersistent(Body);

        /// <summary>
        /// Gets the body's class, read from its data on every call so that edits to it are seen at once.
        /// </summary>
        public BodyClassFlags BodyClass => BodyClassClassifier.Classify(Body);

        /// <summary>
        /// Gets the body's name, falling back to its GameObject's.
        /// </summary>
        public string BodyName
        {
            get
            {
                string bodyName = Body != null ? Body.Data?.bodyName : null;
                return string.IsNullOrEmpty(bodyName) && Body != null ? Body.gameObject.name : bodyName;
            }
        }

        /// <summary>
        /// Gets the folder of the body's Scaled prefab, where its other assets live.
        /// </summary>
        /// <returns>The folder, or null for a body that is not a prefab or a prefab instance.</returns>
        public string BodyFolder()
        {
            if (Body == null)
                return null;

            string path = EditorUtility.IsPersistent(Body)
                ? AssetDatabase.GetAssetPath(Body)
                : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(Body.gameObject);
            return string.IsNullOrEmpty(path) ? null : System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        /// <summary>
        /// Gets the Scaled prefab asset, whether the body is the asset itself or an instance of it.
        /// </summary>
        /// <returns>The prefab asset's root, or null for a body that is neither.</returns>
        public GameObject ScaledPrefabAsset()
        {
            if (Body == null)
                return null;
            if (EditorUtility.IsPersistent(Body))
                return Body.transform.root.gameObject;

            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(Body.gameObject);
            return source != null ? source.transform.root.gameObject : null;
        }

        /// <summary>
        /// Gets the PQS's surface data, or null.
        /// </summary>
        public PQSData PqsData => Pqs != null ? Pqs.data : null;

        /// <summary>
        /// Creates the context for an inspector on a body's Scaled prefab.
        /// </summary>
        /// <param name="body">The selected body.</param>
        /// <param name="bodyObject">The hosting editor's serialized object for <paramref name="body" />.</param>
        /// <returns>The context.</returns>
        public static PlanetInspectorContext FromBody(CoreCelestialBodyData body, SerializedObject bodyObject)
        {
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            return new PlanetInspectorContext(body, bodyObject, pqs, pqs != null ? new SerializedObject(pqs) : null, false);
        }

        /// <summary>
        /// Creates the context for an inspector on a body's Local prefab.
        /// </summary>
        /// <param name="pqs">The selected PQS.</param>
        /// <param name="pqsObject">The hosting editor's serialized object for <paramref name="pqs" />.</param>
        /// <returns>The context, or null if no body could be found for <paramref name="pqs" />.</returns>
        public static PlanetInspectorContext FromPqs(PQS pqs, SerializedObject pqsObject)
        {
            CoreCelestialBodyData body = BodyResolver.FindBodyIncludingAsset(pqs);
            return body == null ? null : new PlanetInspectorContext(body, new SerializedObject(body), pqs, pqsObject, true);
        }

        /// <summary>
        /// Gets the GameObject of the half that is not selected: the Local prefab's root when the Scaled one is
        /// selected, and the other way round.
        /// </summary>
        /// <returns>The other half's GameObject, or null when the body has no Local half.</returns>
        public GameObject OtherHalf()
        {
            if (IsLocalSelected)
                return Body != null ? Body.gameObject : null;

            return Pqs != null ? Pqs.gameObject : null;
        }
    }
}

using System.Collections.Generic;
using KSP.Rendering.Planets;
using KSP.Sim;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Draws a body's sim objects in the scene view while its preview runs, with handles for the ones placed on it.
    /// </summary>
    /// <remarks>
    /// Sim objects are entries in the body's data rather than GameObjects, so nothing else draws them. Every object
    /// gets a marker and its name. One placed against the body in its Body frame also gets a move handle, which drops it
    /// on the terrain under the pointer, and a heading disc.
    /// </remarks>
    public class SimObjectGizmos
    {
        private const string LIST_PATH = "core.data.LocalSimObjectsData";
        private const int MAX_PARENT_DEPTH = 16;

        private static readonly Color MARKER_COLOR = new(1f, 0.75f, 0.25f, 1f);

        private readonly PlanetInspectorContext _context;
        private readonly List<Entry> _entries = new();

        /// <summary>
        /// Initializes the gizmos for a body.
        /// </summary>
        /// <param name="context">The inspector's context.</param>
        public SimObjectGizmos(PlanetInspectorContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Starts drawing in the scene view.
        /// </summary>
        public void Attach()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Stops drawing in the scene view.
        /// </summary>
        public void Detach()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.RepaintAll();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            SerializedObject body = _context.BodyObject;
            if (body == null || body.targetObject == null || !PlanetPreviewLauncher.IsPreviewing(_context.Body))
                return;

            PQS pqs = PlanetAuthoringSession.Active.Pqs;
            if (pqs == null)
                return;

            body.Update();
            SerializedProperty list = body.FindProperty(LIST_PATH);
            ReadEntries(list);
            for (var index = 0; index < _entries.Count; index++)
            {
                if (!TryGetWorld(index, pqs, 0, out Vector3 worldPosition, out Quaternion worldRotation))
                    continue;

                Entry entry = _entries[index];
                float size = HandleUtility.GetHandleSize(worldPosition);
                Handles.color = MARKER_COLOR;
                Handles.SphereHandleCap(0, worldPosition, Quaternion.identity, size * 0.08f, EventType.Repaint);
                Handles.Label(worldPosition + worldRotation * Vector3.up * size * 0.15f, entry.Name);

                if (entry.IsOnBody)
                {
                    DrawHandles(list.GetArrayElementAtIndex(index), pqs, worldPosition, worldRotation, size);
                }
            }
        }

        private void DrawHandles(SerializedProperty element, PQS pqs, Vector3 worldPosition, Quaternion worldRotation, float size)
        {
            SerializedProperty positionProperty = element.FindPropertyRelative("LocalPosition");
            SerializedProperty rotationProperty = element.FindPropertyRelative("LocalRotation");
            Vector3d position = ReadPosition(positionProperty);
            double heading = SimObjectPlacement.ToHeading(position, rotationProperty.quaternionValue);

            EditorGUI.BeginChangeCheck();
            Handles.FreeMoveHandle(worldPosition, size * 0.06f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                if (PlanetSurfaceHit.TryHit(pqs, ray, out Vector3 hitWorld, out _, out _))
                {
                    Vector3d onSurface = (Vector3d)(Quaternion.Inverse(pqs.transform.rotation) * (hitWorld - pqs.transform.position));
                    WritePosition(positionProperty, onSurface);
                    rotationProperty.quaternionValue = SimObjectPlacement.ToRotation(onSurface, heading);
                    element.serializedObject.ApplyModifiedProperties();
                }

                return;
            }

            Vector3 worldUp = worldRotation * Vector3.up;
            EditorGUI.BeginChangeCheck();
            Quaternion turned = Handles.Disc(worldRotation, worldPosition, worldUp, size * 0.4f, false, 5f);
            if (!EditorGUI.EndChangeCheck())
                return;

            Quaternion localTurned = Quaternion.Inverse(pqs.transform.rotation) * turned;
            rotationProperty.quaternionValue = SimObjectPlacement.ToRotation(position, SimObjectPlacement.ToHeading(position, localTurned));
            element.serializedObject.ApplyModifiedProperties();
        }

        private void ReadEntries(SerializedProperty list)
        {
            _entries.Clear();
            if (list == null)
                return;

            for (var index = 0; index < list.arraySize; index++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(index);
                _entries.Add(new Entry
                {
                    Name = element.FindPropertyRelative("Name").stringValue,
                    RelativeTo = element.FindPropertyRelative("RelativeTo").stringValue,
                    Frame = (TransformFrameType)element.FindPropertyRelative("ReferenceFrame").enumValueIndex,
                    Position = ReadPosition(element.FindPropertyRelative("LocalPosition")),
                    Rotation = element.FindPropertyRelative("LocalRotation").quaternionValue,
                });
            }
        }

        // An object against another is placed in that object's frame, so its world placement chains up the list. The
        // depth limit stops a loop of objects placed against each other.
        private bool TryGetWorld(int index, PQS pqs, int depth, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (depth > MAX_PARENT_DEPTH)
                return false;

            Entry entry = _entries[index];
            Vector3 parentPosition = pqs.transform.position;
            Quaternion parentRotation = pqs.transform.rotation;
            if (!string.IsNullOrEmpty(entry.RelativeTo))
            {
                int parent = _entries.FindIndex(candidate => candidate.Name == entry.RelativeTo);
                if (parent < 0 || !TryGetWorld(parent, pqs, depth + 1, out parentPosition, out Quaternion parentWorldRotation))
                    return false;

                // The parent's Body frame turns with it. Its other frames do not, which in the still preview leaves the
                // body's own orientation.
                parentRotation = entry.Frame == TransformFrameType.Body ? parentWorldRotation : pqs.transform.rotation;
            }

            position = parentPosition + parentRotation * (Vector3)entry.Position;
            rotation = parentRotation * entry.Rotation;
            return true;
        }

        /// <summary>
        /// Reads a serialized <see cref="Vector3d" />.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>The vector.</returns>
        public static Vector3d ReadPosition(SerializedProperty property) => new(
            property.FindPropertyRelative("x").doubleValue,
            property.FindPropertyRelative("y").doubleValue,
            property.FindPropertyRelative("z").doubleValue);

        /// <summary>
        /// Writes a serialized <see cref="Vector3d" />.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">The vector.</param>
        public static void WritePosition(SerializedProperty property, Vector3d value)
        {
            property.FindPropertyRelative("x").doubleValue = value.x;
            property.FindPropertyRelative("y").doubleValue = value.y;
            property.FindPropertyRelative("z").doubleValue = value.z;
        }

        private struct Entry
        {
            public string Name;
            public string RelativeTo;
            public TransformFrameType Frame;
            public Vector3d Position;
            public Quaternion Rotation;

            // Placed against the body in its turning frame, the case latitude, longitude and altitude describe.
            public bool IsOnBody => string.IsNullOrEmpty(RelativeTo) && Frame == TransformFrameType.Body;
        }
    }
}

using System.Reflection;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Gizmos;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_WheelBogey" />. Renders the standard generic field
    /// surface, then injects "Show Axis Gizmo" / "Show Up Axis Gizmo" toggles directly below
    /// the <c>bogeyAxis</c> / <c>bogeyUpAxis</c> rows so the visibility control for each
    /// SceneView arrow lives with the axis it controls.
    /// </summary>
    [DataEditor(typeof(Data_WheelBogey))]
    public sealed class WheelBogeyDataEditor : IDataEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            var partRoot = module == null ? null : module.gameObject.transform;
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var iterator = dataProp.Copy();
            var end = iterator.GetEndProperty();
            bool first = true;
            while (iterator.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(iterator, end))
                {
                    break;
                }
                var field = typeof(Data_WheelBogey).GetField(iterator.name, FIELD_FLAGS);
                if (!ShouldRender(field))
                {
                    continue;
                }
                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(iterator.Copy(), field, partRoot);
                if (row != null)
                {
                    root.Add(row);
                }

                if (field.Name == nameof(Data_WheelBogey.bogeyAxis))
                {
                    root.Add(BuildGizmoToggleRow(
                        "Show Axis Gizmo",
                        () => PartAuthoringGizmoSettings.ShowWheelBogeyAxis,
                        v => PartAuthoringGizmoSettings.ShowWheelBogeyAxis = v));
                }
                else if (field.Name == nameof(Data_WheelBogey.bogeyUpAxis))
                {
                    root.Add(BuildGizmoToggleRow(
                        "Show Up Axis Gizmo",
                        () => PartAuthoringGizmoSettings.ShowWheelBogeyUpAxis,
                        v => PartAuthoringGizmoSettings.ShowWheelBogeyUpAxis = v));
                }
            }

            return root;
        }

        private static bool ShouldRender(FieldInfo field)
        {
            if (field == null)
            {
                return false;
            }
            if (field.IsDefined(typeof(KSPStateAttribute), inherit: true))
            {
                return false;
            }
            if (field.IsDefined(typeof(HideInInspector), inherit: true))
            {
                return false;
            }
            if (!field.IsDefined(typeof(KSPDefinitionAttribute), inherit: true))
            {
                return false;
            }
            return true;
        }

        private static VisualElement BuildGizmoToggleRow(string label, System.Func<bool> getter, System.Action<bool> setter)
        {
            var toggle = new Toggle(label)
            {
                value = getter(),
            };
            toggle.AddToClassList("unity-base-field__aligned");
            toggle.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                SceneView.RepaintAll();
            });
            return toggle;
        }
    }
}

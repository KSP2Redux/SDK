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
    /// Custom editor for <see cref="Data_Heatshield" />. Renders the standard generic field
    /// surface, then injects a "Show Direction Gizmo" toggle directly below the
    /// <see cref="Data_Heatshield.ShieldingDirection" /> row so the visibility control for the
    /// SceneView gizmo lives with the field it controls.
    /// </summary>
    [DataEditor(typeof(Data_Heatshield))]
    public sealed class HeatshieldDataEditor : IDataEditor
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
                var field = typeof(Data_Heatshield).GetField(iterator.name, FIELD_FLAGS);
                if (!ShouldRender(field))
                {
                    continue;
                }
                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(iterator.Copy(), field, partRoot);
                if (row != null)
                {
                    root.Add(row);
                }

                if (field.Name == nameof(Data_Heatshield.ShieldingDirection))
                {
                    root.Add(BuildGizmoToggleRow());
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

        private static VisualElement BuildGizmoToggleRow()
        {
            var toggle = new Toggle("Show Direction Gizmo")
            {
                value = PartAuthoringGizmoSettings.ShowHeatshieldShieldingDirection,
            };
            toggle.AddToClassList("unity-base-field__aligned");
            toggle.RegisterValueChangedCallback(evt =>
            {
                PartAuthoringGizmoSettings.ShowHeatshieldShieldingDirection = evt.newValue;
                SceneView.RepaintAll();
            });
            return toggle;
        }
    }
}

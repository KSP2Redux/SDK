using System;
using System.Reflection;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using Redux.Modules.Attributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Shared base for <see cref="IDataEditor" /> implementations whose surface is "render every
    /// visible field via the generic dispatch, with a small set of injection points where extra
    /// UI sits below a specific field".
    /// </summary>
    /// <remarks>
    /// Subclasses define what gets injected (typically a gizmo-visibility toggle) by overriding
    /// <see cref="InjectAfter" />. Field-rendering, the <c>NextVisible</c> loop, and the
    /// KSP-attribute filtering are all handled here so each subclass collapses to the injection
    /// rules that make it distinct.
    /// </remarks>
    /// <typeparam name="TData">The concrete Data_* type whose declared fields drive the editor.</typeparam>
    public abstract class DataFieldIteratorEditor<TData> : IDataEditor
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
            var first = true;
            while (iterator.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(iterator, end)) break;
                var field = typeof(TData).GetField(iterator.name, FIELD_FLAGS);
                if (!ShouldRender(field)) continue;

                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(iterator.Copy(), field, partRoot);
                if (row != null) root.Add(row);

                var injection = InjectAfter(field);
                if (injection != null) root.Add(injection);
            }

            return root;
        }

        /// <summary>
        /// Override to return a VisualElement to append immediately after the row for
        /// <paramref name="field" />, or null to inject nothing.
        /// </summary>
        protected abstract VisualElement InjectAfter(FieldInfo field);

        /// <summary>
        /// Builds a standard "show gizmo" Toggle wired to an editor pref getter/setter and a
        /// SceneView repaint on change. Handy for subclasses' injection points.
        /// </summary>
        protected static VisualElement BuildGizmoToggle(string label, Func<bool> getter, Action<bool> setter)
        {
            var toggle = new Toggle(label) { value = getter() };
            toggle.AddToClassList("unity-base-field__aligned");
            toggle.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                SceneView.RepaintAll();
            });
            return toggle;
        }

        private static bool ShouldRender(FieldInfo field)
        {
            if (field == null) return false;
            if (field.IsDefined(typeof(KSPStateAttribute), inherit: true)) return false;
            if (field.IsDefined(typeof(HideInInspector), inherit: true)) return false;
            if (!field.IsDefined(typeof(KSPDefinitionAttribute), inherit: true)) return false;
            return true;
        }
    }
}

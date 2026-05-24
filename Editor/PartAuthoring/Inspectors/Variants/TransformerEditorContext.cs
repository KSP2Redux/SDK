using System;
using KSP;
using VSwift.Modules.Behaviours;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants
{
    /// <summary>
    /// Editing context passed to every <see cref="ITransformerEditor" /> build call.
    /// </summary>
    /// <remarks>
    /// The transformer's data lives directly on the <see cref="Module" /> Component (a Unity Object), not on <see cref="Part" />. Editors should call <see cref="MarkDirty" /> after any mutation so the prefab picks up the change. <see cref="Part" /> is supplied for editor lookups that span the whole part (e.g. a MaterialSwapper's source-material dropdown needs every Renderer in the prefab).
    /// </remarks>
    public sealed class TransformerEditorContext
    {
        /// <summary>The owning part. Used for prefab-wide queries.</summary>
        public CorePartData Part;

        /// <summary>The <see cref="Module_PartSwitch" /> that carries the transformer's variant data. <c>SetDirty</c> and <c>RecordObject</c> target this.</summary>
        public Module_PartSwitch Module;

        /// <summary>Invoked after any author-visible mutation. Wraps <c>Undo.RecordObject</c> + <c>EditorUtility.SetDirty</c> on <see cref="Module" />.</summary>
        public Action MarkDirty;
    }
}

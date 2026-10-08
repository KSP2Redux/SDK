using System;
using System.Collections.Generic;
using KSP;
using KSP.OAB;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// One archetype the New Part wizard can scaffold from.
    /// </summary>
    /// <remarks>
    /// Implementations are pure data declarations. The wizard reads them to populate
    /// its picker and pre-fill steps. The scaffold reads them to lay down a prefab,
    /// JSON, and addressables entry. Discovered via <see cref="API.ReduxTypeCache" />
    /// so a new archetype is one new file under <c>ArchetypeTemplates/</c>.
    /// </remarks>
    public interface IPartArchetype
    {
        /// <summary>Picker grouping header, e.g. "Propulsion".</summary>
        string Category { get; }

        /// <summary>Tiered family key applied to the new part, e.g. "0100-Methalox".</summary>
        string Family { get; }

        /// <summary>Display name shown in the picker, e.g. "Methalox engine".</summary>
        string DisplayName { get; }

        /// <summary>One-line description shown beneath the display name in the picker.</summary>
        string Description { get; }

        /// <summary>
        /// Pre-selected size key when the wizard opens Step 2 for this archetype. The picker suggests
        /// known keys from <see cref="PartSizeRegistry" /> but still allows custom string keys.
        /// </summary>
        string DefaultSizeKey { get; }

        /// <summary>
        /// Module Component <see cref="Type" />s (subclasses of <c>PartBehaviourModule</c>) the scaffold
        /// adds via <c>gameObject.AddComponent(type)</c> in declaration order. The wizard surfaces each
        /// as a checkbox so the author can drop individual modules per part - e.g. unchecking
        /// <c>Module_Gimbal</c> on a Methalox archetype to produce a fixed-thrust engine.
        /// </summary>
        IReadOnlyList<Type> DefaultModules { get; }

        /// <summary>Attach-node templates the scaffold writes into the new part data.</summary>
        IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes { get; }

        /// <summary>
        /// Fills scalar values on the freshly-scaffolded part from the resolved stock-stats bucket.
        /// </summary>
        /// <param name="part">The CorePartData the scaffold just attached.</param>
        /// <param name="bucket">Stock stats for the chosen (family, size) plus fallback context.</param>
        void SeedDefaults(CorePartData part, BucketResolution bucket);
    }
}

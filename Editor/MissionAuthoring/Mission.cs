using KSP.Game.Missions.Definitions;
using UnityEngine;

namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// Editor authoring source for a KSP2 mission.
    /// </summary>
    /// <remarks>
    /// Holds <see cref="MissionData" /> natively via <see cref="SerializeField" />. Polymorphic
    /// fields walk via <c>[SerializeReference]</c> on the runtime types. The sibling JSON,
    /// emitted by an explicit Bake to JSON inspector action, is what the runtime addressable
    /// load path consumes.
    /// </remarks>
    public class Mission : ScriptableObject
    {
        /// <summary>
        /// The authored mission data serialised on this asset.
        /// </summary>
        public MissionData missionData = new();
    }
}

using UnityEngine;

namespace ksp2community.ksp2unitytools.editor.Missions
{
    public enum ConditionOperator
    {
        [InspectorName("EQUAL")]
        Equal,
        [InspectorName("LESSER")]
        Lesser,
        [InspectorName("GREATER")]
        Greater,
    }
}
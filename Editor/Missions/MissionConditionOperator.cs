using UnityEngine;

namespace Ksp2UnityTools.Editor.Missions
{
    public enum ConditionOperator
    {
        [InspectorName("EQUAL")] Equal,
        [InspectorName("LESSER")] Lesser,
        [InspectorName("GREATER")] Greater
    }
}
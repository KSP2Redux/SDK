using System;
using System.Linq;
using KSP.Game.Missions;

namespace ksp2community.ksp2unitytools.editor.Missions.ConditionTree
{
    [Serializable]
    public abstract class RecursiveMissionCondition<T> : BaseMissionCondition where T : BaseMissionCondition
    {
        #region Condition Set
        public LogicalOperator conditionMode = LogicalOperator.AND;
        public T[] children;
        #endregion
        
        public override Condition ToCondition()
        {
            switch (conditionType)
            {
                case MissionConditionType.ConditionSet:
                    return new ConditionSet
                    {
                        ConditionMode = conditionMode,
                        Children = children.Select(x => x.ToCondition()).ToList(),
                    };
                default:
                    return base.ToCondition();
            }
        }
    }
    
    // stages (1) . condition (2)
    [Serializable]
    public class MissionCondition0 : RecursiveMissionCondition<MissionCondition1> { }
    // stages (1) . condition (2) . conditions (3) . condition (4)
    [Serializable]
    public class MissionCondition1 : RecursiveMissionCondition<MissionCondition2> { }
    
    // stages (1) . condition (2) . conditions (3) . condition (4) . conditions (5) . condition (6)
    [Serializable]
    public class MissionCondition2 : RecursiveMissionCondition<MissionCondition3> { }
    
    // stages (1) . condition (2) . conditions (3) . condition (4) . conditions (5) . condition (6) . conditions (7) . condition (8)
    [Serializable]
    public class MissionCondition3 : RecursiveMissionCondition<MissionCondition4> { }
    
    [Serializable]
    public class MissionCondition4 : BaseMissionCondition { }
}
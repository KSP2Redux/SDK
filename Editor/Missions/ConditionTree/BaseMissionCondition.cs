using System;
using KSP.Game.Missions;

namespace Ksp2UnityTools.Editor.Missions.ConditionTree
{
    // Do this in a very weird way to
    [Serializable]
    public abstract class BaseMissionCondition
    {
        public MissionConditionType conditionType;

        #region Property Condition

        public string propertyType;
        public bool requireCurrentValue;
        public string watchedStringValue;
        public double watchedFloatValue;
        public int watchedIntValue;
        public bool watchedBooleanValue;
        public PropertyOperator propertyOperator;
        public bool useStringInput;
        public string stringInput;

        #endregion

        #region Event Condition

        public string eventType;

        #endregion

        public virtual Condition ToCondition()
        {
            switch (conditionType)
            {
                case MissionConditionType.None:
                    return null;
                case MissionConditionType.EventCondition:
                    return new EventCondition
                    {
                        EventTypeAQN = eventType
                    };
                case MissionConditionType.PropertyCondition:
                    return new PropertyCondition
                    {
                        PropertyTypeAQN = propertyType,
                        RequireCurrentValue = requireCurrentValue,
                        TestWatchedstring = watchedStringValue,
                        TestWatchedInt = watchedIntValue,
                        TestWatchedValue = watchedFloatValue,
                        TestWatchedBool = watchedBooleanValue,
                        propOperator = propertyOperator,
                        isInput = useStringInput,
                        Inputstring = stringInput
                    };
                default:
                    return null;
            }
        }
    }
}
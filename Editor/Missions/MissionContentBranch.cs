using System;
using System.Linq;
using UnityEngine;

namespace ksp2community.ksp2unitytools.editor.Missions
{
    [Serializable]
    public class MissionContentBranch
    {
        public enum AcceptableIds
        {
            Brief,
            Debrief,
            OnSubmit
        }
        
        public AcceptableIds id = AcceptableIds.Brief;
        public MissionAction[] actions;

        public KSP.Game.Missions.MissionContentBranch ToMissionContentBranch()
        {
            return new KSP.Game.Missions.MissionContentBranch
            {
                ID = id.ToString(),
                actions = actions.Select(x => x.ToMissionAction()).ToList()
            };
        }
    }
}
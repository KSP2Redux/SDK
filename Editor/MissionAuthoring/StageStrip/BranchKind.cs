namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// The three runtime branch containers a chip can represent. Drives chip
    /// styling and the runtime list mutations on add/remove.
    /// </summary>
    public enum BranchKind
    {
        StageLocal,
        Exception,
        Prerequisite,
    }
}

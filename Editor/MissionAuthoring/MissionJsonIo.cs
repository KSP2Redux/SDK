using KSP.Game.Missions.Definitions;
using KSP.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// Mission JSON load/save helpers that handle the IOProvider init gate and the
    /// <see cref="MissionStage" /> Pack/Unpack contract transparently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MissionStage.condition" /> is <c>[JsonIgnore]</c>. The on-disk form lives in
    /// <see cref="MissionStage.scriptableCondition" /> as a <c>JObject</c>. The runtime calls
    /// <c>Unpack()</c> from <c>MissionStage.Initialize()</c> to lift the typed tree out, and
    /// <c>Pack()</c> before saving to push it back. The editor never goes through
    /// <c>Initialize()</c>, so the pack/unpack steps have to be explicit here.
    /// </para>
    /// <para>
    /// The runtime <see cref="MissionStage.Pack" /> uses <c>JObject.FromObject(condition)</c>
    /// without a serializer arg, which drops the <c>$type</c> discriminator on the packed
    /// JObject. The shipped JSONs include it, so we re-pack here with an explicit serializer
    /// carrying <see cref="TypeNameHandling.Auto" />.
    /// </para>
    /// </remarks>
    internal static class MissionJsonIo
    {
        static MissionJsonIo() => IOProvider.Init();

        /// <summary>
        /// Deserialises mission JSON and unpacks every stage's condition tree so the typed
        /// <see cref="MissionStage.condition" /> field is populated.
        /// </summary>
        /// <param name="text">The mission JSON text to parse.</param>
        /// <returns>The deserialised mission data with every stage's condition tree unpacked.</returns>
        public static MissionData FromJson(string text)
        {
            var data = IOProvider.FromJson<MissionData>(text);
            if (data?.missionStages != null)
            {
                foreach (var stage in data.missionStages)
                {
                    stage.Unpack();
                }
            }
            return data;
        }

        /// <summary>
        /// Packs every stage's condition tree into <see cref="MissionStage.scriptableCondition" />
        /// (with $type discriminators preserved) then serialises through the default
        /// Unity serializer settings.
        /// </summary>
        /// <remarks>
        /// Does not route through <c>KSP2UnityTools.ToJson</c> because that uses
        /// <see cref="IOProvider.GetDontDeserializeKspStateSerializerSettings" />, whose
        /// contract resolver sets <c>Ignored = true</c> on every member tagged
        /// <c>[KSPState]</c>. That strips fields like <c>MissionStage.completed</c> and
        /// <c>MissionStage.active</c> from the output despite their <c>[JsonProperty]</c>,
        /// so mission JSON round-trips would lose them. Using the default settings keeps
        /// authored state intact while leaving the shared Part / Planet exports alone.
        /// </remarks>
        /// <param name="missionData">The mission data to serialise.</param>
        /// <returns>The serialised mission JSON.</returns>
        public static string ToJson(MissionData missionData)
        {
            PackWithTypeInfo(missionData);
            return IOProvider.ToJson(missionData, IOProvider.GetDefaultSerializerSettings());
        }

        // The runtime's Pack uses JObject.FromObject which drops the $type discriminator at the
        // root of the packed condition. Force Auto to emit root $type by serializing through a
        // JTokenWriter with declared type = object.
        private static void PackWithTypeInfo(MissionData missionData)
        {
            if (missionData?.missionStages == null) return;

            var serializer = JsonSerializer.Create(IOProvider.GetDontDeserializeKspStateSerializerSettings());

            foreach (var stage in missionData.missionStages)
            {
                if (stage.condition == null)
                {
                    stage.scriptableCondition = null;
                    continue;
                }
                stage.condition.Pack();
                var writer = new JTokenWriter();
                serializer.Serialize(writer, stage.condition, typeof(object));
                stage.scriptableCondition = (JObject)writer.Token;
            }
        }
    }
}

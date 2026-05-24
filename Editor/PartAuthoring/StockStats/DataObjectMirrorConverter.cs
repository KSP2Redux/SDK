#if REDUX
using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>Routes <see cref="DataObjectMirror" /> deserialisation to the right concrete subclass.</summary>
    /// <remarks>
    /// Reads the <c>$type</c> discriminator KSP2's own serializer emits on every module
    /// DataObject (e.g. <c>"KSP.Modules.Data_Engine, Assembly-CSharp"</c>) and populates the
    /// matching mirror. Subclasses self-register via <see cref="DataObjectMirrorAttribute" />,
    /// so adding a new module mirror requires zero converter edits. Unknown discriminators
    /// resolve to <see cref="UnknownDataObjectMirror" /> so deserialisation is total.
    /// </remarks>
    internal sealed class DataObjectMirrorConverter : JsonConverter
    {
        private static readonly Lazy<Dictionary<string, Type>> _registry =
            new(BuildRegistry, isThreadSafe: true);

        public override bool CanConvert(Type objectType) =>
            typeof(DataObjectMirror).IsAssignableFrom(objectType);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            JObject jo = JObject.Load(reader);
            string rawDiscriminator = jo["$type"]?.ToString() ?? string.Empty;
            string typeName = ExtractTypeName(rawDiscriminator);

            if (!string.IsNullOrEmpty(typeName) && _registry.Value.TryGetValue(typeName, out Type mirrorType))
            {
                var instance = (DataObjectMirror)Activator.CreateInstance(mirrorType);
                using (JsonReader jReader = jo.CreateReader())
                {
                    serializer.Populate(jReader, instance);
                }
                return instance;
            }
            return new UnknownDataObjectMirror { TypeDiscriminator = rawDiscriminator };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("DataObjectMirror is read-only.");
        }

        private static Dictionary<string, Type> BuildRegistry()
        {
            var dict = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type type in ReduxTypeCache.GetTypesWithAttribute<DataObjectMirrorAttribute>())
            {
                if (!typeof(DataObjectMirror).IsAssignableFrom(type))
                {
                    continue;
                }
                var attr = (DataObjectMirrorAttribute)Attribute.GetCustomAttribute(type, typeof(DataObjectMirrorAttribute));
                if (attr == null || string.IsNullOrEmpty(attr.TypeDiscriminator))
                {
                    continue;
                }
                dict[attr.TypeDiscriminator] = type;
            }
            return dict;
        }

        private static string ExtractTypeName(string discriminator)
        {
            if (string.IsNullOrEmpty(discriminator))
            {
                return string.Empty;
            }
            int comma = discriminator.IndexOf(',');
            return comma < 0 ? discriminator.Trim() : discriminator.Substring(0, comma).Trim();
        }
    }
}
#endif

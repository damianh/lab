using System;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    /// <summary>
    /// Fields with this attribute will have their contents redacted when serialized.
    /// </summary>
    public class SensitiveConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue("**Redacted**");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotSupportedException("Should not be attempting to deserialize as sensitive data has been discarded.");

        public override bool CanConvert(Type objectType)
            => false;
    }
}

using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace Shared
{
    /// <summary>
    /// Ref to https://github.com/GoogleCloudPlatform/dotnet-docs-samples/blob/8a942cae26/monitoring/api/AlertSample/Program.cs
    /// </summary>
    public class ProtoMessageConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IMessage).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader,
            System.Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            // Read an entire object from the reader.
            var converter = new ExpandoObjectConverter();
            var o = converter.ReadJson(reader, objectType, existingValue, serializer);

            // Convert it back to json text.
            var text = JsonConvert.SerializeObject(o);

            // And let protobuf's parser parse the text.
            var message = (IMessage)Activator.CreateInstance(objectType);
            return JsonParser.Default.Parse(text, message.Descriptor);
        }

        public override void WriteJson(JsonWriter writer, object value,
            JsonSerializer serializer)
        {
            writer.WriteRawValue(JsonFormatter.Default.Format((IMessage)value));
        }
    }
}

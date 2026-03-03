using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using chara2img.Models;

namespace chara2img.Services
{
    public class WorkflowInputConverter : JsonConverter<WorkflowInput>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(WorkflowInput).IsAssignableFrom(typeToConvert);
        }

        public override WorkflowInput? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("InputType", out var inputTypeElement))
            {
                return null;
            }

            var inputType = inputTypeElement.GetString();

            WorkflowInput? input = inputType switch
            {
                "text" => JsonSerializer.Deserialize<TextInput>(root.GetRawText(), options),
                "number" => JsonSerializer.Deserialize<NumberInput>(root.GetRawText(), options),
                "numberpair" => JsonSerializer.Deserialize<NumberPairInput>(root.GetRawText(), options),
                _ => null
            };

            return input;
        }

        public override void Write(Utf8JsonWriter writer, WorkflowInput value, JsonSerializerOptions options)
        {
            var optionsWithoutConverter = new JsonSerializerOptions(options);
            optionsWithoutConverter.Converters.Clear();
            
            JsonSerializer.Serialize(writer, value, optionsWithoutConverter);
        }
    }
}
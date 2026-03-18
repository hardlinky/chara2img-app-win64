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

            // Create options without this converter to avoid infinite recursion
            var optionsWithoutConverter = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            WorkflowInput? input = inputType switch
            {
                "text" or "multiline" => JsonSerializer.Deserialize<TextInput>(root.GetRawText(), optionsWithoutConverter),
                "number" => JsonSerializer.Deserialize<NumberInput>(root.GetRawText(), optionsWithoutConverter),
                "number_pair" or "numberpair" => JsonSerializer.Deserialize<NumberPairInput>(root.GetRawText(), optionsWithoutConverter),
                "lora_list" => JsonSerializer.Deserialize<LoraListInput>(root.GetRawText(), optionsWithoutConverter),
                "boolean" => JsonSerializer.Deserialize<BooleanInput>(root.GetRawText(), optionsWithoutConverter),
                "image" => JsonSerializer.Deserialize<ImageInput>(root.GetRawText(), optionsWithoutConverter),
                _ => null
            };

            return input;
        }

        public override void Write(Utf8JsonWriter writer, WorkflowInput value, JsonSerializerOptions options)
        {
            // Create options without this converter to avoid infinite recursion
            var optionsWithoutConverter = new JsonSerializerOptions
            {
                WriteIndented = options.WriteIndented,
                PropertyNameCaseInsensitive = true
            };
            
            // Determine the actual type and serialize accordingly
            switch (value)
            {
                case TextInput textInput:
                    JsonSerializer.Serialize(writer, textInput, optionsWithoutConverter);
                    break;
                case NumberInput numberInput:
                    JsonSerializer.Serialize(writer, numberInput, optionsWithoutConverter);
                    break;
                case NumberPairInput numberPairInput:
                    JsonSerializer.Serialize(writer, numberPairInput, optionsWithoutConverter);
                    break;
                case LoraListInput loraListInput:
                    JsonSerializer.Serialize(writer, loraListInput, optionsWithoutConverter);
                    break;
                case BooleanInput booleanInput:
                    JsonSerializer.Serialize(writer, booleanInput, optionsWithoutConverter);
                    break;
                case ImageInput imageInput:
                    JsonSerializer.Serialize(writer, imageInput, optionsWithoutConverter);
                    break;
                default:
                    // Fallback to base type
                    JsonSerializer.Serialize(writer, value, optionsWithoutConverter);
                    break;
            }
        }
    }
}
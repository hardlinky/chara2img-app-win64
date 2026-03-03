using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using chara2img.Models;

namespace chara2img.Services
{
    public class WorkflowInputParser
    {
        private static readonly Dictionary<string, string> CategoryMap = new()
        {
            ["Model"] = "Model",
            ["Prompt"] = "Prompt",
            ["Image"] = "Image",
            ["Config"] = "Config",
            ["Character"] = "Character",
            ["Costume"] = "Costume",
            ["Pose"] = "Pose",
            ["Detailer"] = "Detailer"
        };

        public static Dictionary<string, ObservableCollection<WorkflowInput>> ParseWorkflow(string workflowJson)
        {
            var result = new Dictionary<string, ObservableCollection<WorkflowInput>>();
            
            // Initialize all categories
            foreach (var category in CategoryMap.Values.Distinct())
            {
                result[category] = new ObservableCollection<WorkflowInput>();
            }

            try
            {
                var workflow = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(workflowJson);
                if (workflow == null) return result;

                foreach (var (nodeId, node) in workflow)
                {
                    if (!node.TryGetProperty("_meta", out var meta)) continue;
                    if (!meta.TryGetProperty("title", out var titleElement)) continue;

                    var title = titleElement.GetString();
                    if (string.IsNullOrEmpty(title) || !title.StartsWith("[I]")) continue;

                    var input = ParseNode(nodeId, title, node);
                    if (input != null && result.ContainsKey(input.Category))
                    {
                        result[input.Category].Add(input);
                    }
                }
            }
            catch
            {
                // Return empty collections on error
            }

            return result;
        }

        private static WorkflowInput? ParseNode(string nodeId, string title, JsonElement node)
        {
            if (!node.TryGetProperty("inputs", out var inputs)) return null;

            var category = ExtractCategory(title);
            var displayName = title.Replace("[I]", "").Trim();

            // Model - CHECKPOINT
            if (title.Contains("Model - CHECKPOINT"))
            {
                var value = inputs.TryGetProperty("ckpt_name", out var ckpt) ? ckpt.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "text",
                    InputKey = "ckpt_name",
                    Value = value
                };
            }

            // Model - Power Lora Loader
            if (title.Contains("Model - Power Lora Loader"))
            {
                var loras = new ObservableCollection<LoraItem>();
                
                foreach (var prop in inputs.EnumerateObject())
                {
                    if (prop.Name.StartsWith("lora_") && prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        var loraObj = prop.Value;
                        var enabled = loraObj.TryGetProperty("on", out var onProp) && onProp.GetBoolean();
                        var loraName = loraObj.TryGetProperty("lora", out var loraProp) ? loraProp.GetString() ?? "" : "";
                        var strength = loraObj.TryGetProperty("strength", out var strengthProp) ? strengthProp.GetDouble() : 1.0;

                        loras.Add(new LoraItem
                        {
                            Enabled = enabled,
                            LoraName = loraName,
                            Strength = strength
                        });
                    }
                }

                return new LoraListInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "lora_list",
                    Loras = loras
                };
            }

            // Prompt - POSITIVE / NEGATIVE
            if (title.Contains("Prompt - POSITIVE") || title.Contains("Prompt - NEGATIVE"))
            {
                var value = inputs.TryGetProperty("template", out var template) ? template.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "multiline",
                    InputKey = "template",
                    Value = value
                };
            }

            // Image - Width x Height
            if (title.Contains("Image - Width x Height"))
            {
                var width = inputs.TryGetProperty("Xi", out var xi) ? xi.GetInt32() : 1024;
                var height = inputs.TryGetProperty("Yi", out var yi) ? yi.GetInt32() : 1024;

                return new NumberPairInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "number_pair",
                    InputKey1 = "Xi",
                    InputKey2 = "Yi",
                    Label1 = "Width",
                    Label2 = "Height",
                    Value1 = width,
                    Value2 = height
                };
            }

            // Placeholder for future nodes
            return new TextInput
            {
                NodeId = nodeId,
                NodeTitle = title,
                Category = category,
                DisplayName = displayName,
                InputType = "text",
                InputKey = "value",
                Value = ""
            };
        }

        private static string ExtractCategory(string title)
        {
            var parts = title.Replace("[I]", "").Trim().Split('-');
            if (parts.Length > 0)
            {
                var categoryKey = parts[0].Trim();
                if (CategoryMap.ContainsKey(categoryKey))
                    return CategoryMap[categoryKey];
            }
            return "Other";
        }
    }
}
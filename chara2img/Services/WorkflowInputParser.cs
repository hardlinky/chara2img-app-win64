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

            // Config - STEPS (integer)
            if (title.Contains("Config - STEPS"))
            {
                var value = inputs.TryGetProperty("int", out var intVal) ? intVal.GetInt32().ToString() : "30";
                return new NumberInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "number",
                    InputKey = "int",
                    Value = value,
                    IsInteger = true
                };
            }

            // Config - CFG (double)
            if (title.Contains("Config - CFG"))
            {
                var value = inputs.TryGetProperty("cfg", out var cfg) ? cfg.GetDouble().ToString("F1") : "6.0";
                return new NumberInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "number",
                    InputKey = "cfg",
                    Value = value,
                    IsInteger = false
                };
            }

            // Config - Scheduler (single line text)
            if (title.Contains("Config - Scheduler"))
            {
                var value = inputs.TryGetProperty("scheduler", out var sched) ? sched.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "text",
                    InputKey = "scheduler",
                    Value = value
                };
            }

            // Config - Sampler (single line text)
            if (title.Contains("Config - Sampler"))
            {
                var value = inputs.TryGetProperty("sampler_name", out var sampler) ? sampler.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "text",
                    InputKey = "sampler_name",
                    Value = value
                };
            }

            // Config - Denoise (double)
            if (title.Contains("Config - Denoise"))
            {
                var value = inputs.TryGetProperty("float", out var floatVal) ? floatVal.GetDouble().ToString("F2") : "0.50";
                return new NumberInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "number",
                    InputKey = "float",
                    Value = value,
                    IsInteger = false
                };
            }

            // Config - Empty Latent Images (batch_size integer)
            if (title.Contains("Config - Empty Latent Images"))
            {
                var value = inputs.TryGetProperty("batch_size", out var batch) ? batch.GetInt32().ToString() : "1";
                return new NumberInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = "Batch Size",
                    InputType = "number",
                    InputKey = "batch_size",
                    Value = value,
                    IsInteger = true
                };
            }

            // Character - Sex (single line)
            if (title.Contains("Character - Sex"))
            {
                var value = inputs.TryGetProperty("value", out var val) ? val.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "text",
                    InputKey = "value",
                    Value = value
                };
            }

            // Character - Name (single line)
            if (title.Contains("Character - Name"))
            {
                var value = inputs.TryGetProperty("value", out var val) ? val.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "text",
                    InputKey = "value",
                    Value = value
                };
            }

            // Character - Eyes, Hair, Face, Body, Body Details, Negative (multiline)
            if (title.Contains("Character - Eyes") || 
                title.Contains("Character - Hair") || 
                title.Contains("Character - Face") ||
                title.Contains("Character - Body Details") ||
                title.Contains("Character - Body") ||
                title.Contains("Character - Negative"))
            {
                var value = inputs.TryGetProperty("value", out var val) ? val.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "multiline",
                    InputKey = "value",
                    Value = value
                };
            }

            // Pose - Arms, Legs, Face Expression (multiline with 'text' key)
            if (title.Contains("Pose - Arms") || 
                title.Contains("Pose - Legs") || 
                title.Contains("Pose - Face Expression"))
            {
                var value = inputs.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "multiline",
                    InputKey = "text",
                    Value = value
                };
            }

            // Costume - Name (single line with 'value' key)
            if (title.Contains("Costume - Name"))
            {
                var value = inputs.TryGetProperty("value", out var val) ? val.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "text",
                    InputKey = "value",
                    Value = value
                };
            }

            // Costume - Face, Head, Top, Bottom, Shoes, Negative (multiline with 'text' key)
            if (title.Contains("Costume - Face") || 
                title.Contains("Costume - Head") || 
                title.Contains("Costume - Top") ||
                title.Contains("Costume - Bottom") ||
                title.Contains("Costume - Shoes") ||
                title.Contains("Costume - Negative"))
            {
                var value = inputs.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
                return new TextInput
                {
                    NodeId = nodeId,
                    NodeTitle = title,
                    Category = category,
                    DisplayName = displayName,
                    InputType = "multiline",
                    InputKey = "text",
                    Value = value
                };
            }

            // Placeholder for future nodes - return null so they don't show up
            return null;
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
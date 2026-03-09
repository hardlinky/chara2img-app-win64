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
        public static Dictionary<string, ObservableCollection<WorkflowInput>> ParseWorkflow(string workflowJson)
        {
            var result = new Dictionary<string, ObservableCollection<WorkflowInput>>();

            try
            {
                var workflow = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(workflowJson);
                if (workflow == null) return result;

                foreach (var (nodeId, node) in workflow)
                {
                    if (!node.TryGetProperty("_meta", out var meta)) continue;
                    if (!meta.TryGetProperty("title", out var titleElement)) continue;

                    var title = titleElement.GetString();
                    if (string.IsNullOrEmpty(title) || !title.StartsWith("[Input]")) continue;

                    var input = ParseNode(nodeId, title, node);
                    if (input != null)
                    {
                        if (!result.ContainsKey(input.Category))
                        {
                            result[input.Category] = new ObservableCollection<WorkflowInput>();
                        }
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

            // Parse title: "[Input] Category.DisplayName"
            var titleContent = title.Replace("[Input]", "").Trim();
            var parts = titleContent.Split('.');
            
            if (parts.Length < 2) return null;

            var category = parts[0].Trim();
            var displayName = string.Join(".", parts.Skip(1)).Trim();

            // Get the node class_type to infer input type
            if (!node.TryGetProperty("class_type", out var classTypeElement))
                return null;

            var classType = classTypeElement.GetString() ?? "";

            return InferInputFromNodeType(nodeId, title, category, displayName, classType, inputs);
        }

        private static WorkflowInput? InferInputFromNodeType(
            string nodeId, 
            string title, 
            string category, 
            string displayName, 
            string classType, 
            JsonElement inputs)
        {
            // Determine input type based on node class_type and available inputs
            switch (classType)
            {
                case "CR Text":
                case "StringFunction|pysssss":
                    {
                        // Multiline text input
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

                case "PrimitiveNode|pysssss":
                    {
                        // Check the control_after_generate to determine type
                        if (inputs.TryGetProperty("value", out var valueElement))
                        {
                            if (valueElement.ValueKind == JsonValueKind.String)
                            {
                                var value = valueElement.GetString() ?? "";
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
                            else if (valueElement.ValueKind == JsonValueKind.Number)
                            {
                                var value = valueElement.GetDouble();
                                var isInteger = Math.Abs(value % 1) < 0.0001;
                                return new NumberInput
                                {
                                    NodeId = nodeId,
                                    NodeTitle = title,
                                    Category = category,
                                    DisplayName = displayName,
                                    InputType = "number",
                                    InputKey = "value",
                                    Value = isInteger ? ((int)value).ToString() : value.ToString("F2"),
                                    IsInteger = isInteger
                                };
                            }
                        }
                        break;
                    }

                case "EmptyLatentImage":
                    {
                        // Width x Height pair
                        var width = inputs.TryGetProperty("width", out var w) ? w.GetInt32() : 1024;
                        var height = inputs.TryGetProperty("height", out var h) ? h.GetInt32() : 1024;

                        return new NumberPairInput
                        {
                            NodeId = nodeId,
                            NodeTitle = title,
                            Category = category,
                            DisplayName = displayName,
                            InputType = "number_pair",
                            InputKey1 = "width",
                            InputKey2 = "height",
                            Label1 = "Width",
                            Label2 = "Height",
                            Value1 = width,
                            Value2 = height
                        };
                    }

                case "CR Integer Multiple":
                    {
                        var value = inputs.TryGetProperty("int", out var intVal) ? intVal.GetInt32().ToString() : "1";
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

                case "CR Float":
                    {
                        var value = inputs.TryGetProperty("float", out var floatVal) ? floatVal.GetDouble().ToString("F2") : "1.0";
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

                case "CheckpointLoaderSimple":
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

                case "Power Lora Loader (rgthree)":
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

                case "KSampler":
                    {
                        // Could have multiple inputs - handle based on what's present
                        if (inputs.TryGetProperty("seed", out var seedElement))
                        {
                            var value = seedElement.GetInt64().ToString();
                            return new NumberInput
                            {
                                NodeId = nodeId,
                                NodeTitle = title,
                                Category = category,
                                DisplayName = displayName,
                                InputType = "number",
                                InputKey = "seed",
                                Value = value,
                                IsInteger = true
                            };
                        }
                        else if (inputs.TryGetProperty("steps", out var stepsElement))
                        {
                            var value = stepsElement.GetInt32().ToString();
                            return new NumberInput
                            {
                                NodeId = nodeId,
                                NodeTitle = title,
                                Category = category,
                                DisplayName = displayName,
                                InputType = "number",
                                InputKey = "steps",
                                Value = value,
                                IsInteger = true
                            };
                        }
                        else if (inputs.TryGetProperty("cfg", out var cfgElement))
                        {
                            var value = cfgElement.GetDouble().ToString("F1");
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
                        else if (inputs.TryGetProperty("sampler_name", out var samplerElement))
                        {
                            var value = samplerElement.GetString() ?? "";
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
                        else if (inputs.TryGetProperty("scheduler", out var schedulerElement))
                        {
                            var value = schedulerElement.GetString() ?? "";
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
                        else if (inputs.TryGetProperty("denoise", out var denoiseElement))
                        {
                            var value = denoiseElement.GetDouble().ToString("F2");
                            return new NumberInput
                            {
                                NodeId = nodeId,
                                NodeTitle = title,
                                Category = category,
                                DisplayName = displayName,
                                InputType = "number",
                                InputKey = "denoise",
                                Value = value,
                                IsInteger = false
                            };
                        }
                        break;
                    }
            }

            // Default fallback: try to infer from the first input property
            if (inputs.EnumerateObject().Any())
            {
                var firstProp = inputs.EnumerateObject().First();
                var key = firstProp.Name;
                var value = firstProp.Value;

                if (value.ValueKind == JsonValueKind.String)
                {
                    return new TextInput
                    {
                        NodeId = nodeId,
                        NodeTitle = title,
                        Category = category,
                        DisplayName = displayName,
                        InputType = "text",
                        InputKey = key,
                        Value = value.GetString() ?? ""
                    };
                }
                else if (value.ValueKind == JsonValueKind.Number)
                {
                    var numValue = value.GetDouble();
                    var isInteger = Math.Abs(numValue % 1) < 0.0001;
                    return new NumberInput
                    {
                        NodeId = nodeId,
                        NodeTitle = title,
                        Category = category,
                        DisplayName = displayName,
                        InputType = "number",
                        InputKey = key,
                        Value = isInteger ? ((int)numValue).ToString() : numValue.ToString("F2"),
                        IsInteger = isInteger
                    };
                }
            }

            return null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using chara2img.Models;

namespace chara2img.Services
{
    public class WorkflowInputApplier
    {
        public static string ApplyInputs(string workflowJson, Dictionary<string, System.Collections.ObjectModel.ObservableCollection<WorkflowInput>> inputs)
        {
            try
            {
                var workflow = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(workflowJson);
                if (workflow == null) return workflowJson;

                // Flatten all inputs
                var allInputs = inputs.Values.SelectMany(x => x).ToList();

                foreach (var input in allInputs)
                {
                    if (!workflow.ContainsKey(input.NodeId)) continue;

                    var node = workflow[input.NodeId];
                    var nodeDict = JsonSerializer.Deserialize<Dictionary<string, object>>(node.GetRawText());
                    if (nodeDict == null || !nodeDict.ContainsKey("inputs")) continue;

                    // Use JsonObject to preserve property order
                    var nodeInputsJson = JsonSerializer.Deserialize<JsonObject>(
                        JsonSerializer.Serialize(nodeDict["inputs"]));
                    
                    if (nodeInputsJson == null) continue;

                    // Apply based on input type
                    if (input is TextInput textInput)
                    {
                        nodeInputsJson[textInput.InputKey] = textInput.Value;
                    }
                    else if (input is NumberInput numberInput)
                    {
                        // Parse and convert the value
                        if (numberInput.IsInteger)
                        {
                            if (int.TryParse(numberInput.Value, out var intValue))
                            {
                                nodeInputsJson[numberInput.InputKey] = intValue;
                            }
                        }
                        else
                        {
                            if (double.TryParse(numberInput.Value, out var doubleValue))
                            {
                                nodeInputsJson[numberInput.InputKey] = doubleValue;
                            }
                        }
                    }
                    else if (input is NumberPairInput numberPair)
                    {
                        nodeInputsJson[numberPair.InputKey1] = numberPair.Value1;
                        nodeInputsJson[numberPair.InputKey2] = numberPair.Value2;
                        // Sync float variants
                        nodeInputsJson["Xf"] = numberPair.Value1;
                        nodeInputsJson["Yf"] = numberPair.Value2;
                    }
                    else if (input is LoraListInput loraList)
                    {
                        // Get all existing lora keys sorted by their index
                        var existingLoraKeys = nodeInputsJson
                            .Where(kvp => kvp.Key.StartsWith("lora_"))
                            .Select(kvp => kvp.Key)
                            .OrderBy(k => 
                            {
                                if (int.TryParse(k.Substring(5), out var index))
                                    return index;
                                return int.MaxValue;
                            })
                            .ToList();

                        // Remove all existing lora entries
                        foreach (var key in existingLoraKeys)
                        {
                            nodeInputsJson.Remove(key);
                        }

                        // Add loras preserving their sequential order
                        int loraIndex = 1;
                        foreach (var lora in loraList.Loras)
                        {
                            var loraKey = $"lora_{loraIndex}";
                            var loraObject = new JsonObject
                            {
                                ["on"] = lora.Enabled,
                                ["lora"] = lora.LoraName,
                                ["strength"] = lora.Strength
                            };
                            nodeInputsJson[loraKey] = loraObject;
                            loraIndex++;
                        }
                    }

                    nodeDict["inputs"] = JsonSerializer.Deserialize<object>(nodeInputsJson.ToJsonString());
                    workflow[input.NodeId] = JsonSerializer.SerializeToElement(nodeDict);
                }

                return JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return workflowJson; // Return original if modification fails
            }
        }
    }
}
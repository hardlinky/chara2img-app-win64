using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

                    var nodeInputs = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(nodeDict["inputs"])) ?? new();

                    // Apply based on input type
                    if (input is TextInput textInput)
                    {
                        nodeInputs[textInput.InputKey] = textInput.Value;
                    }
                    else if (input is NumberInput numberInput)
                    {
                        // Parse and convert the value
                        if (numberInput.IsInteger)
                        {
                            if (int.TryParse(numberInput.Value, out var intValue))
                            {
                                nodeInputs[numberInput.InputKey] = intValue;
                            }
                        }
                        else
                        {
                            if (double.TryParse(numberInput.Value, out var doubleValue))
                            {
                                nodeInputs[numberInput.InputKey] = doubleValue;
                            }
                        }
                    }
                    else if (input is NumberPairInput numberPair)
                    {
                        nodeInputs[numberPair.InputKey1] = numberPair.Value1;
                        nodeInputs[numberPair.InputKey2] = numberPair.Value2;
                        // Sync float variants
                        nodeInputs["Xf"] = numberPair.Value1;
                        nodeInputs["Yf"] = numberPair.Value2;
                    }
                    else if (input is LoraListInput loraList)
                    {
                        int loraIndex = 1;
                        foreach (var lora in loraList.Loras)
                        {
                            var loraKey = $"lora_{loraIndex}";
                            nodeInputs[loraKey] = new Dictionary<string, object>
                            {
                                ["on"] = lora.Enabled,
                                ["lora"] = lora.LoraName,
                                ["strength"] = lora.Strength
                            };
                            loraIndex++;
                        }
                    }

                    nodeDict["inputs"] = nodeInputs;
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
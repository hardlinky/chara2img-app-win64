using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace chara2img.Models
{
    public class RunpodJob
    {
        public string? Id { get; set; }
        public string Status { get; set; } = "pending";
        public string? ImageBase64 { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RawStatusResponse { get; set; }
    }

    public class RunpodRequest
    {
        [JsonPropertyName("input")]
        public RunpodRequestInput Input { get; set; } = new();
    }

    public class RunpodRequestInput
    {
        [JsonPropertyName("workflow")]
        public object? Workflow { get; set; }
    }

    public class RunpodResponse
    {
        [JsonPropertyName("delayTime")]
        public int DelayTime { get; set; }

        [JsonPropertyName("executionTime")]
        public int ExecutionTime { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("output")]
        public RunpodOutput? Output { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class RunpodOutput
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("images")]
        public List<RunpodImageOutput>? Images { get; set; }
    }

    public class RunpodImageOutput
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("filename")]
        public string? FileName { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}

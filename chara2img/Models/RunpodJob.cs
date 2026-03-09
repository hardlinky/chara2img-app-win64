using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace chara2img.Models
{
    public class RunpodJob : INotifyPropertyChanged
    {
        private string _status = "pending";
        private DateTime? _completedAt;
        private string? _workerId;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Id { get; set; }
        
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? WorkerId
        {
            get => _workerId;
            set
            {
                if (_workerId != value)
                {
                    _workerId = value;
                    OnPropertyChanged();
                }
            }
        }

        // Don't serialize base64 image data - it's huge!
        [JsonIgnore]
        public string? ImageBase64 { get; set; }
        
        [JsonIgnore]
        public List<string>? AllImagesBase64 { get; set; }
        
        // Only serialize file paths - these are small
        public string? ImageFilePath { get; set; }
        public List<string>? ImageFilePaths { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set
            {
                if (_completedAt != value)
                {
                    _completedAt = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        public string? ErrorMessage { get; set; }
        
        // Also don't serialize the full raw response - it can be huge too
        [JsonIgnore]
        public string? RawStatusResponse { get; set; }
        
        // Store the workflow inputs used for this job to enable rerun
        public string? WorkflowInputsJson { get; set; }

        [JsonIgnore]
        public string Duration
        {
            get
            {
                if (CompletedAt.HasValue)
                {
                    var duration = CompletedAt.Value - CreatedAt;
                    if (duration.TotalHours >= 1)
                        return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
                    else if (duration.TotalMinutes >= 1)
                        return $"{duration.Minutes}m {duration.Seconds}s";
                    else
                        return $"{duration.Seconds}s";
                }
                return "-";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
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
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("workerId")]
        public string? WorkerId { get; set; }

        [JsonPropertyName("output")]
        public RunpodOutput? Output { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class RunpodOutput
    {
        [JsonPropertyName("images")]
        public List<RunpodImage>? Images { get; set; }
    }

    public class RunpodImage
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}

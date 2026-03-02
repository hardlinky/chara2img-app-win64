using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using chara2img.Models;

namespace chara2img.Services
{
    public class RunpodService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _endpointId;

        public RunpodService(string apiKey, string endpointId)
        {
            _apiKey = apiKey;
            _endpointId = endpointId;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<string?> SubmitJobAsync(object workflow)
        {
            var url = $"https://api.runpod.ai/v2/{_endpointId}/run";
            var request = new RunpodRequest { Input = new() { Workflow = workflow } };

            var response = await _httpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RunpodResponse>();
            return result?.Id;
        }

        public async Task<(RunpodResponse? response, string rawJson)> GetJobStatusAsync(string jobId)
        {
            var url = $"https://api.runpod.ai/v2/{_endpointId}/status/{jobId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync();
            var parsedResponse = await response.Content.ReadFromJsonAsync<RunpodResponse>();
            
            return (parsedResponse, rawJson);
        }

        public async Task<RunpodJob?> PollJobUntilCompleteAsync(string jobId, 
            int pollingIntervalMs = 2000, 
            int maxAttempts = 150,
            IProgress<string>? progress = null,
            Action<string>? onStatusUpdate = null)
        {
            string? latestRawResponse = null;

            for (int i = 0; i < maxAttempts; i++)
            {
                var (response, rawJson) = await GetJobStatusAsync(jobId);
                latestRawResponse = rawJson;
                onStatusUpdate?.Invoke(rawJson);
                
                progress?.Report($"Attempt {i + 1}/{maxAttempts}: Status = {response?.Status}");

                if (response?.Status == "COMPLETED")
                {
                    return new RunpodJob
                    {
                        Id = jobId,
                        Status = "completed",
                        ImageBase64 = ExtractImageFromResponse(response),
                        CompletedAt = DateTime.Now,
                        RawStatusResponse = TruncateBase64InJson(rawJson)
                    };
                }
                else if (response?.Status == "FAILED")
                {
                    return new RunpodJob
                    {
                        Id = jobId,
                        Status = "failed",
                        ErrorMessage = response.Error,
                        CompletedAt = DateTime.Now,
                        RawStatusResponse = rawJson
                    };
                }

                await Task.Delay(pollingIntervalMs);
            }

            return new RunpodJob
            {
                Id = jobId,
                Status = "timeout",
                ErrorMessage = "Job polling timeout exceeded",
                RawStatusResponse = latestRawResponse
            };
        }

        private static string? ExtractImageFromResponse(RunpodResponse response)
        {
            // Try to get image from the Images array
            if (response.Output?.Images != null && response.Output.Images.Count > 0)
            {
                return response.Output.Images[0]?.Data;
            }

            return null;
        }

        private static string TruncateBase64InJson(string json)
        {
            // Find base64 strings (they're typically very long alphanumeric strings)
            // and truncate them to show first 100 and last 50 characters
            var regex = new Regex(@"""([A-Za-z0-9+/]{200,}={0,2})""");
            return regex.Replace(json, match =>
            {
                var base64 = match.Groups[1].Value;
                if (base64.Length > 200)
                {
                    var truncated = $"{base64.Substring(0, 100)}...[{base64.Length - 150} characters truncated]...{base64.Substring(base64.Length - 50)}";
                    return $"\"{truncated}\"";
                }
                return match.Value;
            });
        }
    }
}

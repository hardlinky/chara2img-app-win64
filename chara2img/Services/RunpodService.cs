using System;
using System.Collections.Generic;
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

        public async Task<RunpodJob?> PollJobUntilCompleteAsync(
            RunpodJob job,
            int pollingIntervalMs = 6000, 
            int maxAttempts = 150,
            IProgress<string>? progress = null,
            Action<string>? onStatusUpdate = null)
        {
            string? latestRawResponse = null;
            int actualAttempts = 0; // Count only non-queued attempts

            for (int i = 0; i < maxAttempts; i++)
            {
                var (response, rawJson) = await GetJobStatusAsync(job.Id!);
                latestRawResponse = rawJson;
                onStatusUpdate?.Invoke(rawJson);
                
                // Update worker ID if available
                if (!string.IsNullOrEmpty(response?.WorkerId))
                {
                    job.WorkerId = response.WorkerId;
                }
                
                // Only count attempts when job is actually processing
                if (response?.Status != "IN_QUEUE")
                {
                    actualAttempts++;
                }

                var statusDisplay = response?.Status == "IN_QUEUE" 
                    ? $"Status = {response?.Status} (waiting...)"
                    : $"Attempt {actualAttempts}/{maxAttempts}: Status = {response?.Status}";
                
                progress?.Report(statusDisplay);

                if (response?.Status == "COMPLETED")
                {
                    job.Status = "completed";
                    job.AllImagesBase64 = ExtractAllImagesFromResponse(response);
                    job.ImageBase64 = job.AllImagesBase64?.FirstOrDefault();
                    job.CompletedAt = DateTime.Now;
                    job.RawStatusResponse = rawJson; // Store raw, untruncated
                    return job;
                }
                else if (response?.Status == "FAILED")
                {
                    job.Status = "failed";
                    job.ErrorMessage = response.Error;
                    job.CompletedAt = DateTime.Now;
                    job.RawStatusResponse = rawJson; // Store raw, untruncated
                    return job;
                }

                await Task.Delay(pollingIntervalMs);
            }

            job.Status = "timeout";
            job.ErrorMessage = "Job polling timeout exceeded";
            job.RawStatusResponse = latestRawResponse;
            return job;
        }

        private static List<string> ExtractAllImagesFromResponse(RunpodResponse response)
        {
            var images = new List<string>();

            if (response.Output?.Images != null)
            {
                foreach (var imageOutput in response.Output.Images)
                {
                    if (!string.IsNullOrEmpty(imageOutput?.Data))
                    {
                        images.Add(imageOutput.Data);
                    }
                }
            }

            return images;
        }

        public static string TruncateBase64InJson(string json, bool removeCompletely = false)
        {
            // Match "data": followed by a long base64 string (could be on multiple lines in formatted JSON)
            // This pattern handles both compact and formatted JSON
            var regex = new Regex(@"""data"":\s*""([A-Za-z0-9+/\\u002B\\u002F=\r\n\s]{100,})""", RegexOptions.Singleline);
            
            var result = regex.Replace(json, match =>
            {
                var base64 = match.Groups[1].Value;
                // Remove any whitespace/newlines/escape sequences to get true length
                var cleanBase64 = base64.Replace("\\u002B", "+").Replace("\\u002F", "/").Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                if (removeCompletely)
                {
                    // When collapsed, completely hide the base64 value
                    return $"\"data\": \"[truncated {cleanBase64.Length} characters]\"";
                }
                else
                {
                    // When expanded, don't truncate at all - show full base64
                    return match.Value;
                }
            });
            
            return result;
        }

        public async Task<bool> CancelJobAsync(string jobId)
        {
            try
            {
                var url = $"https://api.runpod.ai/v2/{_endpointId}/cancel/{jobId}";
                var response = await _httpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using chara2img.Models;

namespace chara2img.Services
{
    public class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Chara2IMG",
            "settings.json");

        public string ApiKey { get; set; } = "";
        public string EndpointId { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public string LastWorkflowPath { get; set; } = "";
        public List<RunpodJob>? RecentJobs { get; set; }
        public bool SaveWorkflowWithJob { get; set; } = true;
        public string Theme { get; set; } = "Light";
        public int MaxPollingAttempts { get; set; } = 150;
        public Dictionary<string, CategoryPreference>? CategoryPreferences { get; set; }
        public string? LastInputValuesJson { get; set; }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // If loading fails, return new settings
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail if we can't save settings
            }
        }
    }

    public class CategoryPreference
    {
        public int Order { get; set; }
        public bool IsCollapsed { get; set; }
        public int ViewIndex { get; set; } // 0 = primary, 1 = secondary
    }
}
using System;
using System.IO;
using System.Text.Json;

namespace ProxyMonitor.Services
{
    public class AppConfig
    {
        public string TtsText { get; set; } = "系统代理开启";
        public string TtsTextDisabled { get; set; } = "系统代理关闭";
        public int Volume { get; set; } = 100;
        public int Rate { get; set; } = 0;
        public string? VoiceName { get; set; }
        public string? AudioFileEnabled { get; set; }
        public string? AudioFileDisabled { get; set; }
        public bool RunOnStartup { get; set; } = false;
    }

    public class ConfigService
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        public AppConfig CurrentConfig { get; private set; }

        public ConfigService()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    CurrentConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    CurrentConfig = new AppConfig();
                }
            }
            else
            {
                CurrentConfig = new AppConfig();
            }
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
    }
}

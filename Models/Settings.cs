using System.IO;
using System.Text.Json;

namespace ROMMend.Avalonia.Models;

public class Settings
{
    private const string SettingsFile = "settings.json";
    private SettingsData _settings;

    public Settings()
    {
        _settings = LoadSettings();
    }

    private class SettingsData
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string DownloadDirectory { get; set; } = string.Empty;
    }

    private SettingsData LoadSettings()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? GetDefaultSettings();
            }
            catch (JsonException)
            {
                return GetDefaultSettings();
            }
        }
        return GetDefaultSettings();
    }

    public void SaveSettings(string username, string password, string host, string downloadDirectory)
    {
        _settings.Username = username;
        _settings.Password = password;
        _settings.Host = host;
        _settings.DownloadDirectory = downloadDirectory;
        
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    public void ClearSettings()
    {
        _settings = GetDefaultSettings();
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    private SettingsData GetDefaultSettings() => new();

    public string Get(string key) => key switch
    {
        "username" => _settings.Username,
        "password" => _settings.Password,
        "host" => _settings.Host,
        "download_directory" => _settings.DownloadDirectory,
        _ => string.Empty
    };

    public bool HasLoginCredentials() =>
        !string.IsNullOrEmpty(_settings.Username) &&
        !string.IsNullOrEmpty(_settings.Password) &&
        !string.IsNullOrEmpty(_settings.Host);
}

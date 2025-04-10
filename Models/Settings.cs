using System.IO;
using System.Text.Json;
using ROMMend.Services;

namespace ROMMend.Models;

public class Settings
{
    private const string SettingsFile = "settings.json";
    private readonly EncryptionService _encryptionService;
    private SettingsData _settings;

    public Settings()
    {
        _encryptionService = new EncryptionService();
        _settings = LoadSettings();
    }

    private class SettingsData
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string DownloadDirectory { get; set; } = string.Empty;
        public bool UseHttps { get; set; } = true;
    }

    private SettingsData LoadSettings()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                var encryptedJson = File.ReadAllText(SettingsFile);
                var json = _encryptionService.Decrypt(encryptedJson);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? GetDefaultSettings();
            }
            catch
            {
                return GetDefaultSettings();
            }
        }
        return GetDefaultSettings();
    }

    public void SaveSettings(string username, string password, string host, string downloadDirectory, bool useHttps)
    {
        _settings.Username = username;
        _settings.Password = password;
        _settings.Host = host;
        _settings.DownloadDirectory = downloadDirectory;
        _settings.UseHttps = useHttps;
        
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        var encryptedJson = _encryptionService.Encrypt(json);
        File.WriteAllText(SettingsFile, encryptedJson);
    }

    public void ClearSettings()
    {
        _settings = GetDefaultSettings();
        if (File.Exists(SettingsFile))
        {
            File.Delete(SettingsFile);
        }
    }

    private SettingsData GetDefaultSettings() => new();

    public string Get(string key) => key switch
    {
        "username" => _settings.Username,
        "password" => _settings.Password,
        "host" => _settings.Host,
        "download_directory" => _settings.DownloadDirectory,
        "use_https" => _settings.UseHttps.ToString(),
        _ => string.Empty
    };

    public bool HasLoginCredentials() =>
        !string.IsNullOrEmpty(_settings.Username) &&
        !string.IsNullOrEmpty(_settings.Password) &&
        !string.IsNullOrEmpty(_settings.Host);
}

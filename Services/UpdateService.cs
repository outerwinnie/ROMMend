using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ROMMend.Services;

public class UpdateService
{
    private const string GithubApi = "https://api.github.com/repos/OWNER/REPO/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ROMMend");
    }

    public async Task<(bool available, string version, string url)> CheckForUpdateAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(GithubApi);
            var release = JsonSerializer.Deserialize<GithubRelease>(response);

            if (release == null || string.IsNullOrEmpty(release.TagName)) 
                return (false, string.Empty, string.Empty);

            var currentVersion = GetCurrentVersion();
            var latestVersion = release.TagName.TrimStart('v');

            if (Version.Parse(latestVersion) > Version.Parse(currentVersion))
            {
                var asset = GetPlatformAsset(release);
                return asset != null 
                    ? (true, latestVersion, asset.BrowserDownloadUrl) 
                    : (false, string.Empty, string.Empty);
            }

            return (false, string.Empty, string.Empty);
        }
        catch
        {
            return (false, string.Empty, string.Empty);
        }
    }

    private string GetCurrentVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.0.0";
    }

    private GithubAsset? GetPlatformAsset(GithubRelease release)
    {
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : null;

        return platform == null ? null 
            : release.Assets.Find(a => a.Name.Contains(platform, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DownloadAndInstallUpdateAsync(string url, IProgress<(int percentage, string status)>? progress = null)
    {
        try
        {
            // Download the update
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var tempFile = Path.GetTempFileName();
            
            using (var fileStream = File.Create(tempFile))
            using (var downloadStream = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[81920];
                var totalBytesRead = 0L;
                
                while (true)
                {
                    var bytesRead = await downloadStream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var percentage = (int)((totalBytesRead * 100) / totalBytes);
                        progress.Report((percentage, $"Downloading update: {percentage}%"));
                    }
                }
            }

            // Create update script
            var scriptPath = Path.Combine(Path.GetTempPath(), "update.bat");
            var appPath = Environment.ProcessPath ?? "ROMMend.exe";
            var updateScript = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $@"@echo off
                   timeout /t 1
                   move /y ""{tempFile}"" ""{appPath}""
                   start """" ""{appPath}""
                   del ""%~f0""
                   exit"
                : $"#!/bin/bash\nsleep 1\nmv \"{tempFile}\" \"{appPath}\"\n\"{appPath}\"";

            File.WriteAllText(scriptPath, updateScript);

            // Make script executable on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x {scriptPath}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
            }

            // Execute update script
            Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            throw new Exception($"Update failed: {ex.Message}");
        }
    }

    private class GithubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GithubAsset> Assets { get; set; } = new();
    }

    private class GithubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
} 
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;

namespace ROMMend.Services;

public class UpdateService
{
    private const string GithubApi = "https://api.github.com/repos/outerwinnie/ROMMend/releases/latest";
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
            Console.WriteLine($"GitHub Response: {response}"); // Debug log

            var release = JsonSerializer.Deserialize<GithubRelease>(response);
            Console.WriteLine($"Release Tag: {release?.TagName}"); // Debug log

            if (release == null || string.IsNullOrEmpty(release.TagName)) 
                return (false, string.Empty, string.Empty);

            var currentVersion = GetCurrentVersion();
            var latestVersion = release.TagName.TrimStart('v');
            
            Console.WriteLine($"Current Version: {currentVersion}"); // Debug log
            Console.WriteLine($"Latest Version: {latestVersion}"); // Debug log

            if (Version.Parse(latestVersion) > Version.Parse(currentVersion))
            {
                var asset = GetPlatformAsset(release);
                Console.WriteLine($"Found Asset: {asset?.Name}"); // Debug log
                return asset != null 
                    ? (true, latestVersion, asset.BrowserDownloadUrl) 
                    : (false, string.Empty, string.Empty);
            }

            return (false, string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update check failed: {ex.Message}"); // Debug log
            return (false, string.Empty, string.Empty);
        }
    }

    private string GetCurrentVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                // Use only major.minor.build
                var versionString = $"{version.Major}.{version.Minor}.{version.Build}";
                Console.WriteLine($"Found version in assembly: {versionString}"); // Debug log
                return versionString;
            }
            return "0.1.5"; // Fallback to current version
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting version: {ex.Message}"); // Debug log
            return "0.1.5"; // Fallback to current version
        }
    }

    private GithubAsset? GetPlatformAsset(GithubRelease release)
    {
        var assetName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ROMMend-Windows.zip"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "ROMMend-OSX.zip"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "ROMMend-Linux.zip"
            : null;

        return assetName == null ? null 
            : release.Assets.Find(a => a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DownloadAndInstallUpdateAsync(string url, IProgress<(int percentage, string status)>? progress = null)
    {
        try
        {
            // Download the update
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var tempZipFile = Path.Combine(Path.GetTempPath(), "ROMMend-update.zip");
            var tempExtractPath = Path.Combine(Path.GetTempPath(), "ROMMend-update");
            
            // Download zip file
            using (var fileStream = File.Create(tempZipFile))
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

            // Extract zip file
            progress?.Report((0, "Extracting update..."));
            if (Directory.Exists(tempExtractPath))
                Directory.Delete(tempExtractPath, true);
            Directory.CreateDirectory(tempExtractPath);
            
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZipFile, tempExtractPath);

            // Find the executable in the extracted files
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ROMMend.exe" : "ROMMend";
            var extractedExe = Directory.GetFiles(tempExtractPath, exeName, SearchOption.AllDirectories).FirstOrDefault();
            
            if (extractedExe == null)
                throw new FileNotFoundException("Could not find application executable in update package");

            // Create update script
            var scriptPath = Path.Combine(Path.GetTempPath(), "update.bat");
            var appPath = Environment.ProcessPath ?? "ROMMend.exe";
            var updateScript = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $@"@echo off
                   timeout /t 1
                   move /y ""{extractedExe}"" ""{appPath}""
                   rmdir /s /q ""{tempExtractPath}""
                   del ""{tempZipFile}""
                   start """" ""{appPath}""
                   del ""%~f0""
                   exit"
                : $"#!/bin/bash\nsleep 1\nmv \"{extractedExe}\" \"{appPath}\"\nrm -rf \"{tempExtractPath}\"\nrm \"{tempZipFile}\"\n\"{appPath}\"";

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
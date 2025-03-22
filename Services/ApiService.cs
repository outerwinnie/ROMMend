using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ROMMend.Models;
using System.Threading;

namespace ROMMend.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    public string Host { get; }

    public ApiService(string host, string username, string password)
    {
        Host = host;
        _httpClient = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<(bool success, string error)> LoginAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{Host}/api/login", null);
            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }
            
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => (false, "Invalid username or password"),
                System.Net.HttpStatusCode.NotFound => (false, "Invalid host or API endpoint not found"),
                _ => (false, $"Server error: {response.StatusCode}")
            };
        }
        catch (HttpRequestException ex)
        {
            return (false, "Could not connect to server. Please check the host address and your internet connection.");
        }
        catch (Exception)
        {
            return (false, "An unexpected error occurred while connecting to the server.");
        }
    }

    public async Task<List<Rom>> GetRomsAsync(IProgress<(int percentage, string status)>? progress = null)
    {
        try
        {
            progress?.Report((0, "Fetching ROM list..."));
            var response = await _httpClient.GetAsync($"{Host}/api/roms");
            if (!response.IsSuccessStatusCode) return new List<Rom>();

            var content = await response.Content.ReadAsStringAsync();
            var roms = JsonSerializer.Deserialize<List<Rom>>(content) ?? new List<Rom>();

            // Report progress for each ROM's details
            if (roms.Count > 0 && progress != null)
            {
                for (int i = 0; i < roms.Count; i++)
                {
                    var rom = roms[i];
                    var percentage = (int)((i + 1) * 100.0 / roms.Count);
                    progress.Report((percentage, $"Loading ROM details ({i + 1}/{roms.Count})"));
                    
                    // Add a small delay to show progress
                    await Task.Delay(50);
                }
            }

            return roms;
        }
        catch
        {
            return new List<Rom>();
        }
    }

    public async Task<string?> DownloadRomAsync(int id, string fsName, string romName, string destinationPath,
        IProgress<(int percentage, string status)>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{Host}/api/roms/{id}/content/{fsName}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var buffer = new byte[81920];
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var totalBytesRead = 0L;
            var startTime = DateTime.Now;

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Stream directly to file instead of loading into memory
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                while (true)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0) break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var percentage = (int)((totalBytesRead * 100) / totalBytes);
                        var elapsedTime = DateTime.Now - startTime;
                        var speed = totalBytesRead / (1024.0 * 1024.0 * elapsedTime.TotalSeconds);
                        var downloadedMB = totalBytesRead / (1024.0 * 1024.0);
                        var totalMB = totalBytes / (1024.0 * 1024.0);
                        
                        progress.Report((percentage, $"Downloading {romName}: {downloadedMB:F1} MB / {totalMB:F1} MB ({speed:F1} MB/s)"));
                    }
                }
            }

            return destinationPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> DownloadImageAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return null;
        }
    }

    private string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }

    private string FormatSize(double bytes)
    {
        const double GB = 1024 * 1024 * 1024;
        const double MB = 1024 * 1024;
        
        if (bytes >= GB)
        {
            return $"{bytes / GB:F2} GB";
        }
        
        return $"{bytes / MB:F1} MB";
    }
}

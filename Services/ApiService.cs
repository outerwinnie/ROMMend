using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ROMMend.Models;

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
            var response = await _httpClient.PostAsync($"https://{Host}/api/login", null);
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

    public async Task<List<Rom>> GetRomsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://{Host}/api/roms");
            if (!response.IsSuccessStatusCode) return new List<Rom>();

            var content = await response.Content.ReadAsStringAsync();
            var roms = JsonSerializer.Deserialize<List<Rom>>(content);
            return roms ?? new List<Rom>();
        }
        catch
        {
            return new List<Rom>();
        }
    }

    public async Task<byte[]?> DownloadRomAsync(int romId, string fsName, IProgress<(int percentage, string status)> progress)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"https://{Host}/api/roms/{romId}/content/{fsName}",
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Server returned {response.StatusCode}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var buffer = new byte[81920]; // Increased buffer size for better performance
            var totalBytesRead = 0L;
            var startTime = DateTime.Now;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();

            int retryCount = 0;
            const int maxRetries = 3;
            const int retryDelayMs = 1000;

            while (true)
            {
                try
                {
                    var bytesRead = await contentStream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (int)((totalBytesRead * 100) / totalBytes);
                        var elapsedTime = DateTime.Now - startTime;
                        var speed = totalBytesRead / (1024.0 * 1024.0 * elapsedTime.TotalSeconds);
                        var remainingBytes = totalBytes - totalBytesRead;
                        var etaSeconds = speed > 0 ? remainingBytes / (speed * 1024 * 1024) : 0;
                        var eta = TimeSpan.FromSeconds(etaSeconds);
                        var downloadedMB = totalBytesRead / (1024.0 * 1024.0);
                        var totalMB = totalBytes / (1024.0 * 1024.0);
                        var status = $"{percentage}% - {downloadedMB:F1}/{totalMB:F1} MB - {speed:F1} MB/s - ETA: {FormatTime(eta)}";
                        
                        progress.Report((percentage, status));
                    }

                    retryCount = 0; // Reset retry count on successful read
                }
                catch (IOException) when (retryCount < maxRetries)
                {
                    retryCount++;
                    await Task.Delay(retryDelayMs * retryCount);
                    progress.Report((
                        (int)((totalBytesRead * 100) / totalBytes), 
                        $"Connection error, retry attempt {retryCount}/{maxRetries}..."
                    ));
                    continue;
                }
            }

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            progress.Report((0, $"Download failed: {ex.Message}"));
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
}

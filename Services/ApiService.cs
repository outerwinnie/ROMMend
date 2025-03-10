using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ROMMend.Avalonia.Models;

namespace ROMMend.Avalonia.Services;

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

    public async Task<bool> LoginAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"https://{Host}/api/login", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
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
            var response = await _httpClient.GetAsync(
                $"https://{Host}/api/roms/{romId}/content/{fsName}",
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode) return null;

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            var startTime = DateTime.Now;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    var percentage = (int)((totalBytesRead * 100) / totalBytes);
                    var elapsedTime = DateTime.Now - startTime;
                    var speed = totalBytesRead / (1024.0 * 1024.0 * elapsedTime.TotalSeconds); // MB/s
                    var remainingBytes = totalBytes - totalBytesRead;
                    var etaSeconds = speed > 0 ? remainingBytes / (speed * 1024 * 1024) : 0;
                    var eta = TimeSpan.FromSeconds(etaSeconds);
                    var status = $"{percentage}% - {speed:F1} MB/s - ETA: {FormatTime(eta)}";
                    
                    progress.Report((percentage, status));
                }
            }

            return memoryStream.ToArray();
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
}

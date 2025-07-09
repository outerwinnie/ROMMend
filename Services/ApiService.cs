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
    private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), "debug.txt");
    internal static void LogToFile(string message)
    {
        string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        try
        {
            File.AppendAllText(LogFilePath, logLine);
        }
        catch (Exception ex)
        {
            // Fallback: write to console if file logging fails
            Console.WriteLine($"[LogToFile-Fallback] {logLine} (File error: {ex.Message})");
        }
    }
    private readonly HttpClient _httpClient;
    public string Host { get; }

    public ApiService(string host, string username, string password)
    {
        LogToFile($"ApiService instantiated. Host={host}, Username={username}");
        Host = host;
        _httpClient = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<(bool success, string error)> LoginAsync()
    {
        string url = $"{Host}/api/login";
        try
        {
            LogToFile($"[ApiService] Attempting login: POST {url}");
            var response = await _httpClient.PostAsync(url, null);
            LogToFile($"[ApiService] Login response: StatusCode={(int)response.StatusCode} {response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                LogToFile("[ApiService] Login successful.");
                return (true, string.Empty);
            }
            string respBody = await response.Content.ReadAsStringAsync();
            LogToFile($"[ApiService] Login failed: Body={respBody}");
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => (false, "Invalid username or password"),
                System.Net.HttpStatusCode.NotFound => (false, "Invalid host or API endpoint not found"),
                _ => (false, $"Server error: {response.StatusCode} - {respBody}")
            };
        }
        catch (HttpRequestException ex)
        {
            LogToFile($"[ApiService] HttpRequestException: {ex.Message}");
            return (false, "Could not connect to server. Please check the host address and your internet connection.");
        }
        catch (Exception ex)
        {
            LogToFile($"[ApiService] Unexpected Exception: {ex}");
            return (false, "An unexpected error occurred while connecting to the server.");
        }
    }

    public class PaginatedResponse<T>
    {
        public List<T>? Results { get; set; }
        public List<T>? Items { get; set; } // fallback for alternate key
        public int Count { get; set; } // total count if provided
    }

    public async Task<List<Rom>> GetRomsAsync(IProgress<(int percentage, string status)>? progress = null)
    {
        const int pageSize = 1000;
        int offset = 0;
        int total = -1;
        List<Rom> allRoms = new List<Rom>();
        bool more = true;

        try
        {
            while (more)
            {
                string url = $"{Host}/api/roms?limit={pageSize}&offset={offset}";
                LogToFile($"[ApiService] Fetching ROMs: GET {url}");

                var response = await _httpClient.GetAsync(url);
                LogToFile($"[ApiService] GetRoms response: StatusCode={(int)response.StatusCode} {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    string respBody = await response.Content.ReadAsStringAsync();
                    LogToFile($"[ApiService] GetRoms failed: Body={respBody}");
                    break;
                }

                var content = await response.Content.ReadAsStringAsync();
                LogToFile($"[ApiService] GetRoms success: Body length={content.Length}");

                try
                {
                    var paged = JsonSerializer.Deserialize<PaginatedResponse<Rom>>(content, new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true
                    });
                    List<Rom>? roms = paged?.Results ?? paged?.Items;
                    if (roms == null)
                    {
                        LogToFile($"[ApiService] Paginated response did not contain 'results' or 'items'. Logging first 1000 chars: {content.Substring(0, Math.Min(1000, content.Length))}");
                        break;
                    }
                    if (total < 0 && paged != null)
                        total = paged.Count > 0 ? paged.Count : Math.Max(roms.Count, 1);

                    allRoms.AddRange(roms);
                    LogToFile($"[ApiService] Page loaded: {roms.Count} ROMs, total so far: {allRoms.Count}");
                    if (roms.Count < pageSize)
                    {
                        more = false;
                    }
                    else
                    {
                        offset += pageSize;
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"[ApiService] Deserialization error: {ex}\nFirst 1000 chars: {content.Substring(0, Math.Min(1000, content.Length))}");
                    break;
                }
            }


            return allRoms;
        }
        catch (Exception ex)
        {
            LogToFile($"[ApiService] Exception in GetRomsAsync: {ex}");
            return allRoms;
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

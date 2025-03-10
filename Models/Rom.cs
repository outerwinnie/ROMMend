using System.Text.Json.Serialization;

namespace ROMMend.Models;

public class Rom
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("platform_fs_slug")]
    public string PlatformFsSlug { get; set; } = string.Empty;

    [JsonPropertyName("fs_name")]
    public string FsName { get; set; } = string.Empty;

    [JsonPropertyName("url_cover")]
    public string? UrlCover { get; set; }

    [JsonPropertyName("fs_size_bytes")]
    public long FsSizeBytes { get; set; }

    public string DisplayName => $"{Name} ({PlatformFsSlug})";

    public string Size
    {
        get
        {
            const double GB = 1024 * 1024 * 1024;
            const double MB = 1024 * 1024;
            
            if (FsSizeBytes >= GB)
            {
                return $"{FsSizeBytes / GB:F2} GB";
            }
            
            return $"{FsSizeBytes / MB:F1} MB";
        }
    }
}

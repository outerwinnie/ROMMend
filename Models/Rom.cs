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

    public string DisplayName => $"{Name} ({PlatformFsSlug})";
}

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ROMMend.Avalonia.Models;
using ROMMend.Avalonia.Services;

namespace ROMMend.Avalonia.ViewModels;

public partial class RomViewModel : ViewModelBase
{
    private readonly Rom _rom;
    private readonly CacheService _cacheService;

    [ObservableProperty]
    private Bitmap? _coverImage;

    public int Id => _rom.Id;
    public string Name => _rom.Name;
    public string PlatformFsSlug => _rom.PlatformFsSlug;
    public string FsName => _rom.FsName;
    public string DisplayName => _rom.DisplayName;

    public RomViewModel(Rom rom, CacheService cacheService)
    {
        _rom = rom;
        _cacheService = cacheService;
    }

    public async Task LoadCoverImageAsync(ApiService apiService)
    {
        try
        {
            // Try to load from cache first
            CoverImage = await _cacheService.LoadCoverImageAsync(_rom.Name);
            if (CoverImage != null) return;

            // If cache miss or error, load from API
            if (string.IsNullOrEmpty(_rom.UrlCover)) return;

            var coverUrl = _rom.UrlCover;
            if (!coverUrl.StartsWith("http"))
            {
                coverUrl = $"https://{apiService.Host}{coverUrl}";
            }

            var imageData = await apiService.DownloadImageAsync(coverUrl);
            if (imageData != null)
            {
                using var stream = new MemoryStream(imageData);
                CoverImage = new Bitmap(stream);
                await _cacheService.SaveCoverImageAsync(_rom.Name, imageData);
            }
        }
        catch
        {
            // Failed to load cover image
        }
    }
}

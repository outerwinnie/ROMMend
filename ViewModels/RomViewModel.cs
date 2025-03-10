using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ROMMend.Models;
using ROMMend.Services;

namespace ROMMend.ViewModels;

public partial class RomViewModel : ViewModelBase
{
    private readonly Rom _rom;
    private readonly CacheService _cacheService;

    [ObservableProperty]
    private Bitmap? _coverImage;

    [ObservableProperty]
    private bool _isDownloaded;

    public int Id => _rom.Id;
    public string Name => _rom.Name;
    public string PlatformFsSlug => _rom.PlatformFsSlug;
    public string FsName => _rom.FsName;
    public string DisplayName => _rom.DisplayName;
    public string Size => _rom.Size;

    public RomViewModel(Rom rom, CacheService cacheService)
    {
        _rom = rom;
        _cacheService = cacheService;
    }

    public async Task LoadCoverImageAsync(ApiService apiService)
    {
        try
        {
            CoverImage = await _cacheService.LoadCoverImageAsync(_rom.Id);
            if (CoverImage != null) return;

            // If not in cache and we have a URL, download and cache it
            if (!string.IsNullOrEmpty(_rom.UrlCover))
            {
                var imageData = await apiService.DownloadImageAsync(_rom.UrlCover);
                if (imageData != null)
                {
                    CoverImage = await _cacheService.SaveCoverImageAsync(_rom.Id, imageData);
                }
            }
        }
        catch
        {
            // Ignore image loading errors
        }
    }

    public void CheckIfDownloaded(string downloadDirectory)
    {
        var filePath = Path.Combine(downloadDirectory, PlatformFsSlug.ToLower(), FsName);
        IsDownloaded = File.Exists(filePath);
    }
}

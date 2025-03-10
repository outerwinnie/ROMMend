using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ROMMend.Models;
using ROMMend.Services;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace ROMMend.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly Settings _settings;
    private ApiService? _apiService;
    private readonly CacheService _cacheService;
    private readonly IStorageProvider _storageProvider;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _downloadDirectory = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RomViewModel> _roms = new();

    [ObservableProperty]
    private ObservableCollection<RomViewModel> _filteredRoms = new();

    [ObservableProperty]
    private ObservableCollection<string> _platforms = new();

    [ObservableProperty]
    private string? _selectedPlatform;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    partial void OnSelectedPlatformChanged(string? value)
    {
        FilterRoms();
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterRoms();
    }

    private void FilterRoms()
    {
        FilteredRoms.Clear();
        var romsToShow = Roms.AsEnumerable();

        // Filter by platform
        if (!string.IsNullOrEmpty(SelectedPlatform) && SelectedPlatform != "All Platforms")
        {
            romsToShow = romsToShow.Where(r => r.PlatformFsSlug == SelectedPlatform);
        }

        // Filter by search query
        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.Trim().ToLower();
            romsToShow = romsToShow.Where(r => r.Name.ToLower().Contains(query));
        }
        
        foreach (var rom in romsToShow)
        {
            FilteredRoms.Add(rom);
        }
    }

    private void SortPlatforms()
    {
        var sorted = Platforms.OrderBy(p => p == "All Platforms" ? "" : p).ToList();
        Platforms.Clear();
        foreach (var platform in sorted)
        {
            Platforms.Add(platform);
        }
    }

    public MainViewModel(IStorageProvider storageProvider)
    {
        _settings = new Settings();
        _cacheService = new CacheService();
        _storageProvider = storageProvider;
        LoadSettings();
        
        if (_settings.HasLoginCredentials())
        {
            _ = LoginAsync();
        }
    }

    private void LoadSettings()
    {
        Username = _settings.Get("username");
        Password = _settings.Get("password");
        Host = _settings.Get("host");
        DownloadDirectory = _settings.Get("download_directory");
    }

    [RelayCommand]
    private async Task SelectDownloadDirectoryAsync()
    {
        var folder = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Download Directory",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            DownloadDirectory = folder[0].Path.LocalPath;
            _settings.SaveSettings(Username, Password, Host, DownloadDirectory);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Host) || 
                string.IsNullOrWhiteSpace(Username) || 
                string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please fill in all fields (Host, Username, and Password)";
                return;
            }

            if (string.IsNullOrWhiteSpace(DownloadDirectory))
            {
                StatusMessage = "Please select a download directory before logging in";
                return;
            }

            IsLoading = true;
            StatusMessage = "Connecting to server...";

            _apiService = new ApiService(Host, Username, Password);
            var (success, error) = await _apiService.LoginAsync();

            if (success)
            {
                _settings.SaveSettings(Username, Password, Host, DownloadDirectory);
                IsLoggedIn = true;
                StatusMessage = "Connected successfully!";
                await LoadRomsAsync();
            }
            else
            {
                StatusMessage = error;
                _settings.ClearSettings();
                _cacheService.ClearCache();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Login error: {ex.Message}";
            _settings.ClearSettings();
            _cacheService.ClearCache();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _apiService = null;
        IsLoggedIn = false;
        Roms.Clear();
        FilteredRoms.Clear();
        Platforms.Clear();
        SelectedPlatform = null;
        SearchQuery = string.Empty;
        StatusMessage = string.Empty;
        _settings.ClearSettings();
        _cacheService.ClearCache();
    }

    private async Task LoadRomsAsync()
    {
        if (_apiService == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Loading ROMs...";
            Roms.Clear();
            FilteredRoms.Clear();
            Platforms.Clear();

            var roms = await _apiService.GetRomsAsync();
            
            // Add "All Platforms" option
            Platforms.Add("All Platforms");
            
            // Get unique platforms
            var uniquePlatforms = roms.Select(r => r.PlatformFsSlug).Distinct();
            foreach (var platform in uniquePlatforms)
            {
                Platforms.Add(platform);
            }
            
            SortPlatforms();
            SelectedPlatform = "All Platforms";

            foreach (var rom in roms)
            {
                var romViewModel = new RomViewModel(rom, _cacheService);
                await romViewModel.LoadCoverImageAsync(_apiService);
                Roms.Add(romViewModel);
            }

            FilterRoms();
            StatusMessage = $"Loaded {Roms.Count} ROMs";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading ROMs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadRomAsync(RomViewModel? rom)
    {
        if (_apiService == null || rom == null || IsLoading) return;

        try
        {
            IsLoading = true;
            var progress = new Progress<(int percentage, string status)>(update =>
            {
                DownloadProgress = update.percentage;
                DownloadStatus = update.status;
            });

            var data = await _apiService.DownloadRomAsync(rom.Id, rom.Name, progress);
            if (data != null)
            {
                var filePath = Path.Combine(DownloadDirectory, $"{rom.Name}.rom");
                await File.WriteAllBytesAsync(filePath, data);
                StatusMessage = $"Successfully downloaded {rom.Name}";
            }
            else
            {
                StatusMessage = $"Failed to download {rom.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading {rom.Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            DownloadProgress = 0;
            DownloadStatus = string.Empty;
        }
    }
}

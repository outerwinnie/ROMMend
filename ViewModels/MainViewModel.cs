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
        StatusMessage = string.Empty;
        _settings.ClearSettings();
        _cacheService.ClearCache();
        Username = string.Empty;
        Password = string.Empty;
        Host = string.Empty;
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
            Platforms.Add("All Platforms");

            var roms = await _apiService.GetRomsAsync();
            foreach (var rom in roms)
            {
                var romViewModel = new RomViewModel(rom, _cacheService);
                await romViewModel.LoadCoverImageAsync(_apiService);
                Roms.Add(romViewModel);
                if (!Platforms.Contains(rom.PlatformFsSlug))
                {
                    Platforms.Add(rom.PlatformFsSlug);
                }
            }

            SortPlatforms();
            StatusMessage = $"Loaded {roms.Count} ROMs";
            SelectedPlatform = "All Platforms";
            FilterRoms();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading ROMs: {ex.Message}";
            Roms.Clear();
            FilteredRoms.Clear();
            Platforms.Clear();
            Platforms.Add("All Platforms");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadRomAsync(RomViewModel rom)
    {
        if (_apiService == null || string.IsNullOrEmpty(DownloadDirectory)) return;

        try
        {
            var filePath = Path.Combine(DownloadDirectory, rom.FsName);
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                StatusMessage = $"{rom.FsName} already exists ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)";
                return;
            }

            IsLoading = true;
            StatusMessage = $"Downloading {rom.Name}...";
            DownloadProgress = 0;
            DownloadStatus = "Starting download...";

            // Create a temporary file path for downloading
            var tempFilePath = Path.Combine(DownloadDirectory, $"{rom.FsName}.tmp");

            var progress = new Progress<(int percentage, string status)>(update =>
            {
                DownloadProgress = update.percentage;
                DownloadStatus = update.status;
            });

            var data = await _apiService.DownloadRomAsync(rom.Id, rom.FsName, progress);
            if (data != null)
            {
                // Write to temporary file first
                await File.WriteAllBytesAsync(tempFilePath, data);

                // Move the temporary file to the final location
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempFilePath, filePath);

                var fileSize = new FileInfo(filePath).Length / (1024.0 * 1024.0);
                StatusMessage = $"Downloaded {rom.Name} successfully! ({fileSize:F1} MB)";
            }
            else
            {
                StatusMessage = $"Failed to download {rom.Name}";
                // Clean up temporary file if it exists
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
        catch (IOException ex)
        {
            StatusMessage = $"File error: {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied. Please check folder permissions.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading ROM: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            DownloadProgress = 0;
            DownloadStatus = string.Empty;
        }
    }
}

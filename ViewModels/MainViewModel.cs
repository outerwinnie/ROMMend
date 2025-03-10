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
using System.Threading;

namespace ROMMend.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly Settings _settings;
    private ApiService? _apiService;
    private readonly CacheService _cacheService;
    private readonly IStorageProvider _storageProvider;
    private CancellationTokenSource? _downloadCancellation;
    private readonly CompressionService _compressionService = new();

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

    [ObservableProperty]
    private bool _isDownloading;

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
                _apiService = null; // Reset API service to allow retry
                IsLoggedIn = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Login error: {ex.Message}";
            _apiService = null; // Reset API service to allow retry
            IsLoggedIn = false;
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
        IsLoading = false;
        
        // Clear all input fields
        Username = string.Empty;
        Password = string.Empty;
        Host = string.Empty;
        DownloadDirectory = string.Empty;
        
        // Clear ROM-related data
        Roms.Clear();
        FilteredRoms.Clear();
        Platforms.Clear();
        SelectedPlatform = null;
        SearchQuery = string.Empty;
        
        // Clear progress and status
        DownloadProgress = 0;
        DownloadStatus = string.Empty;
        StatusMessage = string.Empty;
        
        // Clear settings and cache
        _settings.ClearSettings();
        _cacheService.ClearCache();
    }

    private async Task LoadRomsAsync()
    {
        if (_apiService == null) return;

        try
        {
            IsLoading = true;
            DownloadProgress = 0;
            DownloadStatus = string.Empty;
            StatusMessage = string.Empty;
            Roms.Clear();
            FilteredRoms.Clear();
            Platforms.Clear();

            var progress = new Progress<(int percentage, string status)>(update =>
            {
                DownloadProgress = update.percentage;
                DownloadStatus = update.status;
            });

            var roms = await _apiService.GetRomsAsync(progress);
            
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

            // Load cover images with progress
            if (roms.Count > 0)
            {
                for (int i = 0; i < roms.Count; i++)
                {
                    var rom = roms[i];
                    var percentage = (int)((i + 1) * 100.0 / roms.Count);
                    DownloadProgress = percentage;
                    DownloadStatus = $"Loading cover images ({i + 1}/{roms.Count})";

                    var romViewModel = new RomViewModel(rom, _cacheService);
                    await romViewModel.LoadCoverImageAsync(_apiService);
                    Roms.Add(romViewModel);
                }
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
            DownloadProgress = 0;
            DownloadStatus = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCancellation?.Cancel();
        StatusMessage = "Download cancelled";
    }

    [RelayCommand]
    private async Task DownloadRomAsync(RomViewModel? rom)
    {
        if (_apiService == null || rom == null || IsLoading) return;

        try
        {
            IsLoading = true;
            IsDownloading = true;
            StatusMessage = string.Empty;
            _downloadCancellation = new CancellationTokenSource();

            // Create platform subfolder
            var platformDir = Path.Combine(DownloadDirectory, rom.PlatformFsSlug.ToLower());
            Directory.CreateDirectory(platformDir);

            // Use the original filename
            var filePath = Path.Combine(platformDir, rom.FsName);
            
            // Check if file already exists
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                StatusMessage = $"{rom.Name} already exists ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)";
                return;
            }

            var progress = new Progress<(int percentage, string status)>(update =>
            {
                DownloadProgress = update.percentage;
                DownloadStatus = update.status;
            });

            var data = await _apiService.DownloadRomAsync(rom.Id, rom.FsName, rom.Name, progress, _downloadCancellation.Token);
            if (data != null)
            {
                await File.WriteAllBytesAsync(filePath, data, _downloadCancellation.Token);
                
                if (Path.GetExtension(filePath).ToLower() == ".zip")
                {
                    try 
                    {
                        StatusMessage = "Extracting ZIP file...";
                        var gameFolderName = Path.GetFileNameWithoutExtension(rom.FsName);
                        
                        var extractProgress = new Progress<(int percentage, string status)>(update =>
                        {
                            DownloadProgress = update.percentage;
                            DownloadStatus = update.status;
                        });

                        await _compressionService.ExtractZipAsync(filePath, platformDir, gameFolderName, extractProgress);
                        File.Delete(filePath); // Delete the ZIP after extraction
                        rom.IsDownloaded = true;
                        StatusMessage = $"Downloaded and extracted {rom.Name} successfully!";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Error extracting ZIP: {ex.Message}";
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        return;
                    }
                }
                else
                {
                    var fileSize = new FileInfo(filePath).Length / (1024.0 * 1024.0);
                    StatusMessage = $"Downloaded {rom.Name} successfully! ({fileSize:F1} MB)";
                    rom.IsDownloaded = true;
                }
            }
            else
            {
                StatusMessage = $"Failed to download {rom.Name}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled";
            // Clean up partial download if it exists
            var tempFilePath = Path.Combine(DownloadDirectory, rom.PlatformFsSlug, rom.FsName);
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading ROM: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsDownloading = false;
            DownloadProgress = 0;
            DownloadStatus = string.Empty;
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
        }
    }

    [RelayCommand]
    private void DeleteRom(RomViewModel? rom)
    {
        if (rom == null) return;

        try
        {
            var filePath = Path.Combine(DownloadDirectory, rom.PlatformFsSlug.ToLower(), rom.FsName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                rom.IsDownloaded = false;
                StatusMessage = $"Deleted {rom.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting ROM: {ex.Message}";
        }
    }
}

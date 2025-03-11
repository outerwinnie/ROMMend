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
    private readonly PlatformFolders _platformFolders = new();
    private readonly UpdateService _updateService = new();

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

    [ObservableProperty]
    private bool _useHttps = true;

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string _updateVersion = string.Empty;

    [ObservableProperty]
    private bool _isUpdating;

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
        
        // Check for updates first, then attempt login if no update is available
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Check for updates first
            var (available, version, url) = await _updateService.CheckForUpdateAsync();
            UpdateAvailable = available;
            UpdateVersion = version;

            if (available)
            {
                StatusMessage = $"Update v{version} available";
                return; // Don't proceed with login if update is available
            }

            // Proceed with login if we have credentials and no update is available
            if (_settings.HasLoginCredentials())
            {
                await LoginAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization error: {ex.Message}";
        }
    }

    private void LoadSettings()
    {
        Username = _settings.Get("username");
        Password = _settings.Get("password");
        Host = _settings.Get("host");
        DownloadDirectory = _settings.Get("download_directory");
        UseHttps = bool.Parse(_settings.Get("use_https"));
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
            _settings.SaveSettings(Username, Password, Host, DownloadDirectory, UseHttps);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            if (UpdateAvailable)
            {
                StatusMessage = $"Please install the update to version {UpdateVersion} before connecting.";
                return;
            }

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

            var protocol = UseHttps ? "https" : "http";
            var cleanHost = Host.Replace("http://", "").Replace("https://", "");
            var fullHost = $"{protocol}://{cleanHost}";
            
            _apiService = new ApiService(fullHost, Username, Password);
            var (success, error) = await _apiService.LoginAsync();

            if (success)
            {
                _settings.SaveSettings(Username, Password, Host, DownloadDirectory, UseHttps);
                IsLoggedIn = true;
                await LoadRomsAsync();
            }
            else
            {
                StatusMessage = error;
                _apiService = null;
                IsLoggedIn = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Login error: {ex.Message}";
            _apiService = null;
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

            // Load cover images and check downloads with progress
            if (roms.Count > 0)
            {
                for (int i = 0; i < roms.Count; i++)
                {
                    var rom = roms[i];
                    var percentage = (int)((i + 1) * 100.0 / roms.Count);
                    DownloadProgress = percentage;
                    DownloadStatus = $"Loading ROM info ({i + 1}/{roms.Count})";

                    var romViewModel = new RomViewModel(rom, _cacheService);
                    await romViewModel.LoadCoverImageAsync(_apiService);
                    romViewModel.CheckIfDownloaded(DownloadDirectory, _platformFolders);
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

            var platformFolderName = _platformFolders.GetFolderName(rom.PlatformFsSlug);
            var platformDir = Path.Combine(DownloadDirectory, platformFolderName);
            Directory.CreateDirectory(platformDir);

            // Use the original filename
            var filePath = Path.Combine(platformDir, rom.FsName);
            
            // Add .zip extension if missing
            if (string.IsNullOrEmpty(Path.GetExtension(filePath)))
            {
                filePath = Path.ChangeExtension(filePath, ".zip");
            }
            
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
            var platformFolderName = _platformFolders.GetFolderName(rom.PlatformFsSlug);
            var tempFilePath = Path.Combine(DownloadDirectory, platformFolderName, rom.FsName);
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
            var platformFolderName = _platformFolders.GetFolderName(rom.PlatformFsSlug);
            var platformDir = Path.Combine(DownloadDirectory, platformFolderName);
            var filePath = Path.Combine(platformDir, rom.FsName);
            var zipPath = Path.ChangeExtension(filePath, ".zip");
            var folderPath = Path.Combine(platformDir, Path.GetFileNameWithoutExtension(rom.FsName));

            // Delete the file if it exists (for non-ZIP files)
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Delete the ZIP file if it exists
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // Delete the extracted folder if it exists
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }

            rom.IsDownloaded = false;
            StatusMessage = $"Deleted {rom.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting {rom.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        try
        {
            var (available, version, _) = await _updateService.CheckForUpdateAsync();
            UpdateAvailable = available;
            UpdateVersion = version;
            if (available)
            {
                StatusMessage = $"Update {version} available!";
            }
        }
        catch
        {
            // Ignore update check errors
        }
    }

    [RelayCommand]
    private async Task UpdateApplicationAsync()
    {
        if (!UpdateAvailable) return;

        try
        {
            IsLoading = true;
            IsUpdating = true;
            StatusMessage = "Downloading update...";
            
            var (_, _, url) = await _updateService.CheckForUpdateAsync();
            if (string.IsNullOrEmpty(url))
            {
                StatusMessage = "Update failed: Could not get download URL";
                return;
            }

            var progress = new Progress<(int percentage, string status)>(update =>
            {
                DownloadProgress = update.percentage;
                DownloadStatus = update.status;
            });

            await _updateService.DownloadAndInstallUpdateAsync(url, progress);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsUpdating = false;
            DownloadProgress = 0;
            DownloadStatus = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SkipUpdateAsync()
    {
        UpdateAvailable = false;
        // Proceed with login if we have credentials
        if (_settings.HasLoginCredentials())
        {
            await LoginAsync();
        }
    }
}

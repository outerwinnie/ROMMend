using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ROMMend.Models;

namespace ROMMend.Services;

public class CacheService
{
    private const string CacheDirectory = "cache";
    private const string RomsListFile = "roms_list.json";
    private const string CoverImagesDirectory = "cover_images";

    public CacheService()
    {
        EnsureCacheDirectories();
    }

    private void EnsureCacheDirectories()
    {
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(Path.Combine(CacheDirectory, CoverImagesDirectory));
    }

    public async Task SaveRomsListAsync(List<Rom> roms)
    {
        var filePath = Path.Combine(CacheDirectory, RomsListFile);
        var json = JsonSerializer.Serialize(roms, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<Rom>?> LoadRomsListAsync()
    {
        var filePath = Path.Combine(CacheDirectory, RomsListFile);
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<Rom>>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveCoverImageAsync(string romName, byte[] imageData)
    {
        var fileName = GetSafeFileName(romName) + ".png";
        var filePath = Path.Combine(CacheDirectory, CoverImagesDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, imageData);
    }

    public async Task<Bitmap?> LoadCoverImageAsync(string romName)
    {
        var fileName = GetSafeFileName(romName) + ".png";
        var filePath = Path.Combine(CacheDirectory, CoverImagesDirectory, fileName);
        
        if (!File.Exists(filePath)) return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    public void ClearCache()
    {
        try
        {
            Directory.Delete(CacheDirectory, true);
            EnsureCacheDirectories();
        }
        catch
        {
            // Ignore errors during cache clearing
        }
    }
}

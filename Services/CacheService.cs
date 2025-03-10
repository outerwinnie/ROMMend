using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ROMMend.Models;

namespace ROMMend.Services;

public class CacheService
{
    private const string CacheDirectory = "cache";
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

    public async Task<Bitmap?> SaveCoverImageAsync(int romId, byte[] imageData)
    {
        var fileName = $"{romId}.png";
        var filePath = Path.Combine(CacheDirectory, CoverImagesDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, imageData);

        try
        {
            using var stream = new MemoryStream(imageData);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Bitmap?> LoadCoverImageAsync(int romId)
    {
        var fileName = $"{romId}.png";
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

    public void ClearCache()
    {
        try
        {
            var coverImagesPath = Path.Combine(CacheDirectory, CoverImagesDirectory);
            if (Directory.Exists(coverImagesPath))
            {
                Directory.Delete(coverImagesPath, true);
                Directory.CreateDirectory(coverImagesPath);
            }
        }
        catch
        {
            // Ignore errors during cache clearing
        }
    }
}

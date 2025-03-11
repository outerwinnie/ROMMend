using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace ROMMend.Services;

public class CompressionService
{
    public async Task ExtractZipAsync(string zipPath, string outputDirectory, string gameName, 
        IProgress<(int percentage, string status)>? progress = null)
    {
        var gameFolder = Path.Combine(outputDirectory, gameName);
        Directory.CreateDirectory(gameFolder);

        using var archive = ZipArchive.Open(zipPath);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        var totalEntries = entries.Count;
        var processedEntries = 0;
        var startTime = DateTime.Now;

        // Special handling for single-file ZIPs
        if (totalEntries == 1)
        {
            var entry = entries[0];
            var fileName = entry.Key.Split('/').Last(); // Handle nested paths
            var extension = Path.GetExtension(fileName);

            // For files that should keep their original name (like .chd)
            var shouldKeepOriginalName = !string.IsNullOrEmpty(extension) || 
                                       fileName.EndsWith(".chd", StringComparison.OrdinalIgnoreCase);

            var targetFileName = shouldKeepOriginalName ? fileName : gameName;
            var fullPath = Path.Combine(gameFolder, targetFileName);

            using (var entryStream = entry.OpenEntryStream())
            using (var fileStream = File.Create(fullPath))
            {
                await entryStream.CopyToAsync(fileStream);
            }
            
            progress?.Report((100, "Extraction complete"));
            return;
        }

        // Handle multi-file ZIPs
        foreach (var entry in entries)
        {
            // Clean up entry path and get proper file name
            var entryPath = entry.Key.Replace('\\', '/');
            var relativePath = entryPath.Split('/').Length > 1 
                ? string.Join("/", entryPath.Split('/').Skip(1))
                : entryPath;
            
            var fullPath = Path.GetFullPath(Path.Combine(gameFolder, relativePath));

            if (!fullPath.StartsWith(gameFolder))
                continue;

            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            using (var entryStream = entry.OpenEntryStream())
            using (var fileStream = File.Create(fullPath))
            {
                await entryStream.CopyToAsync(fileStream);
            }

            processedEntries++;
            if (progress != null)
            {
                var percentage = (int)((processedEntries * 100.0) / totalEntries);
                var elapsedTime = DateTime.Now - startTime;
                var estimatedTotalTime = TimeSpan.FromTicks((long)(elapsedTime.Ticks / (processedEntries / (double)totalEntries)));
                var remainingTime = estimatedTotalTime - elapsedTime;
                
                progress.Report((percentage, 
                    $"Extracting: {processedEntries}/{totalEntries} files (ETA: {FormatTime(remainingTime)})"));
            }
        }
    }

    private string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        if (time.TotalMinutes >= 1)
            return $"{time.Minutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }
} 
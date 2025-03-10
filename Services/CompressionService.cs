using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace ROMMend.Services;

public class CompressionService
{
    public async Task ExtractZipAsync(string zipPath, string outputDirectory, string gameName, 
        IProgress<(int percentage, string status)>? progress = null)
    {
        var gameFolder = Path.Combine(outputDirectory, gameName);
        Directory.CreateDirectory(gameFolder);

        using var archive = ZipArchive.Open(zipPath);
        var totalEntries = archive.Entries.Count();
        var processedEntries = 0;
        var startTime = DateTime.Now;

        foreach (var entry in archive.Entries)
        {
            var fullPath = Path.Combine(gameFolder, entry.Key);
            var directory = Path.GetDirectoryName(fullPath);

            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            if (!entry.IsDirectory)
            {
                using var entryStream = entry.OpenEntryStream();
                using var fileStream = File.Create(fullPath);
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
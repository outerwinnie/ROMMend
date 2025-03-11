using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ROMMend.Models;

public class PlatformFolders
{
    private const string SettingsFile = "platform_folders.json";
    private readonly Dictionary<string, string> _folderMappings;

    public PlatformFolders()
    {
        _folderMappings = LoadMappings();
    }

    private Dictionary<string, string> LoadMappings()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return mappings ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        // If file doesn't exist, create template
        if (File.Exists("platform_folders.template.json"))
        {
            File.Copy("platform_folders.template.json", SettingsFile);
            try
            {
                var json = File.ReadAllText(SettingsFile);
                var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return mappings ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
        
        return new Dictionary<string, string>();
    }

    public string GetFolderName(string platformSlug)
    {
        var slug = platformSlug.ToLower();
        return _folderMappings.TryGetValue(slug, out var folderName) 
            ? folderName 
            : slug;
    }
} 
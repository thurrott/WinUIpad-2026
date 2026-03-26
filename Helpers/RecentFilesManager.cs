using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinUIpad.Helpers;

public static class RecentFilesManager
{
    private const int MaxRecentFiles = 10;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinUIpad", "recentfiles.json");

    public static List<string> GetRecentFiles()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch { }
        return [];
    }

    public static void AddRecentFile(string path)
    {
        var list = GetRecentFiles();
        list.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxRecentFiles) list.RemoveRange(MaxRecentFiles, list.Count - MaxRecentFiles);
        Save(list);
    }

    public static void ClearRecentFiles() => Save([]);

    private static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}

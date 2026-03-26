using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinUIpad.Services;

public record SessionTab(string FilePath, bool IsNew, string UnsavedContent, int ActiveTabIndex);

public class SessionData
{
    public List<SessionEntry> Tabs   { get; set; } = [];
    public int ActiveTabIndex        { get; set; } = 0;
}

public class SessionEntry
{
    public string FilePath       { get; set; } = string.Empty;
    public bool IsNew            { get; set; } = true;
    public string UnsavedContent { get; set; } = string.Empty;
}

public static class SessionService
{
    private static readonly string SessionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinUIpad", "session.json");

    public static void Save(SessionData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFile)!);
            File.WriteAllText(SessionFile,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static SessionData? Load()
    {
        try
        {
            if (File.Exists(SessionFile))
                return JsonSerializer.Deserialize<SessionData>(File.ReadAllText(SessionFile));
        }
        catch { }
        return null;
    }

    public static void Clear()
    {
        try { if (File.Exists(SessionFile)) File.Delete(SessionFile); } catch { }
    }
}

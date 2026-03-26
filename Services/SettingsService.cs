using System;
using System.IO;
using System.Text.Json;

namespace WinUIpad.Services;

public class AppSettings
{
    public int Theme          { get; set; } = 2;        // 0=Light 1=Dark 2=System
    public string FontFamily  { get; set; } = "Consolas";
    public double FontSize    { get; set; } = 14.0;
    public bool FontBold      { get; set; } = false;
    public bool FontItalic    { get; set; } = false;
    public bool WordWrap      { get; set; } = false;
    public bool ShowStatusBar { get; set; } = true;
    public bool SpellCheck    { get; set; } = true;
    public bool Autocorrect   { get; set; } = true;
    public double ZoomLevel   { get; set; } = 100.0;
    public int WindowLeft     { get; set; } = -1;
    public int WindowTop      { get; set; } = -1;
    public int WindowWidth    { get; set; } = 1100;
    public int WindowHeight   { get; set; } = 750;
    public bool WindowMaximized { get; set; } = false;
}

public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinUIpad");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private AppSettings _settings = new();
    public AppSettings Settings => _settings;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new();
        }
        catch { _settings = new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile,
                JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

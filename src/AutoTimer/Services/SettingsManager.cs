using System;
using System.IO;
using System.Text.Json;
using AutoTimer.Models;

namespace AutoTimer.Services;

public static class SettingsManager
{
    private static readonly object _lock = new();

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoTimer");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(SettingsPath))
            {
                Current = new AppSettings();
                Save();
                return;
            }

            try
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                // Ensure all sub-objects are non-null after deserialization
                loaded.General ??= new();
                loaded.Display ??= new();
                loaded.Playback ??= new();
                loaded.Window ??= new();
                loaded.Schedules ??= [];
                loaded.OneTimeSchedules ??= [];
                Current = loaded;
            }
            catch
            {
                // 손상된 설정 파일 백업 후 초기화
                try
                {
                    var backupPath = SettingsPath + ".bak";
                    File.Copy(SettingsPath, backupPath, true);
                }
                catch { }

                Current = new AppSettings();
            }
        }
    }

    public static void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var tempPath = SettingsPath + ".tmp";
                var json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(tempPath, json);
                try
                {
                    File.Move(tempPath, SettingsPath, true);
                }
                catch
                {
                    // Move failed (e.g. disk full for rename) — try direct write as fallback
                    try { File.WriteAllText(SettingsPath, json); } catch { }
                    try { File.Delete(tempPath); } catch { }
                }
            }
            catch
            {
                // 저장 실패 시 무시 — 다음 저장 시 재시도
            }
        }
    }
}

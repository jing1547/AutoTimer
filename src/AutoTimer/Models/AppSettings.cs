using System;
using System.Collections.Generic;

namespace AutoTimer.Models;

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public PlaybackSettings Playback { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    public List<WeeklySchedule> Schedules { get; set; } = [];
    public List<OneTimeSchedule> OneTimeSchedules { get; set; } = [];
}

public sealed class WindowSettings
{
    public double Width { get; set; } = 540;
    public double Height { get; set; } = 680;
    public double Left { get; set; } = -1;
    public double Top { get; set; } = -1;
}

public sealed class GeneralSettings
{
    public bool RunOnStartup { get; set; }
    public string TimeSource { get; set; } = "server"; // "server" or "local"
    public int SyncIntervalMinutes { get; set; } = 60;
    public int SyncIntervalSeconds { get; set; } = 0;
    public bool PersistSettings { get; set; } = true;
    public string Language { get; set; } = "ko"; // "ko" or "en"
    public string Theme { get; set; } = "dark"; // "dark", "light", "system"
}

public sealed class DisplaySettings
{
    public string TargetMonitor { get; set; } = "";
    public bool FadeOutEnabled { get; set; } = true;
    public int FadeOutDurationMs { get; set; } = 500;
}

public sealed class PlaybackSettings
{
    public string DefaultVideoPath { get; set; } = "";
    public bool UsePerScheduleVideo { get; set; }
    public bool MouseClickThrough { get; set; }
    public bool LockWindow { get; set; } = true;
}

public sealed class WeeklySchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool Enabled { get; set; } = true;
    public DayOfWeek DayOfWeek { get; set; }
    public string Time { get; set; } = "00:00";
    public string? VideoPath { get; set; }
    public string Label { get; set; } = "";
}

public sealed class OneTimeSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Date { get; set; } = "";
    public string Time { get; set; } = "00:00";
    public string? VideoPath { get; set; }
    public string Label { get; set; } = "";
    public bool AutoDelete { get; set; } = true;
}

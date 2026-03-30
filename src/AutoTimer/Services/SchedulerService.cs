using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoTimer.Models;

namespace AutoTimer.Services;

public sealed class SchedulerService : IDisposable
{
    private readonly TimeSyncService _timeSync;
    private Timer? _tickTimer;
    private readonly HashSet<string> _triggeredKeys = [];
    private DateTime _lastCleanup = DateTime.MinValue;

    public event Action<string, string>? ScheduleTriggered;

    public SchedulerService(TimeSyncService timeSync)
    {
        _timeSync = timeSync;
    }

    public void Start()
    {
        var now = DateTime.Now;
        var delayToNextSecond = 1000 - now.Millisecond;
        _tickTimer = new Timer(OnTick, null, delayToNextSecond, 1000);
    }

    private void OnTick(object? state)
    {
        try
        {
            var now = _timeSync.Now;
            var settings = SettingsManager.Current;

            // 오래된 키 정리 (1분마다) — 오늘 이전 키 제거
            if ((now - _lastCleanup).TotalMinutes >= 1)
            {
                var today = now.ToString("yyyy-MM-dd");
                _triggeredKeys.RemoveWhere(k =>
                {
                    // key format: "{id}:{yyyy-MM-dd} {HH:mm}"
                    var colonIdx = k.IndexOf(':');
                    if (colonIdx < 0 || colonIdx + 1 >= k.Length) return true;
                    var datepart = k.Substring(colonIdx + 1, Math.Min(10, k.Length - colonIdx - 1));
                    return string.Compare(datepart, today, StringComparison.Ordinal) < 0;
                });
                _lastCleanup = now;
            }

            // 정각(0초)에만 트리거 체크 — 중복 방지
            if (now.Second == 0)
                CheckTrigger(settings, now);
        }
        catch
        {
            // 다음 틱에서 재시도
        }
    }

    private void CheckTrigger(AppSettings settings, DateTime now)
    {
        var currentTime = now.ToString("HH:mm");
        var currentDate = now.ToString("yyyy-MM-dd");
        var currentDay = now.DayOfWeek;

        // 주간 스케줄
        foreach (var s in settings.Schedules.Where(s => s.Enabled))
        {
            if (s.DayOfWeek != currentDay || s.Time != currentTime)
                continue;

            var key = $"{s.Id}:{currentDate} {currentTime}";
            if (!_triggeredKeys.Add(key))
                return;

            var videoPath = ResolveVideoPath(s.VideoPath, settings);
            ScheduleTriggered?.Invoke(videoPath, s.Label);
            return;
        }

        // 일회성 스케줄
        var needSave = false;
        foreach (var s in settings.OneTimeSchedules.ToList())
        {
            if (s.Date != currentDate || s.Time != currentTime)
                continue;

            var key = $"{s.Id}:{currentDate} {currentTime}";
            if (!_triggeredKeys.Add(key))
                continue;

            var videoPath = ResolveVideoPath(s.VideoPath, settings);
            ScheduleTriggered?.Invoke(videoPath, s.Label);

            if (s.AutoDelete)
            {
                settings.OneTimeSchedules.Remove(s);
                needSave = true;
            }
        }
        if (needSave)
            SettingsManager.Save();
    }

    private static string ResolveVideoPath(string? scheduleVideoPath, AppSettings settings)
    {
        if (settings.Playback.UsePerScheduleVideo && !string.IsNullOrWhiteSpace(scheduleVideoPath))
            return scheduleVideoPath;
        return settings.Playback.DefaultVideoPath;
    }

    public void Dispose()
    {
        _tickTimer?.Dispose();
        _tickTimer = null;
    }
}

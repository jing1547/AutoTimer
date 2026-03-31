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
    private readonly HashSet<string> _preNotifiedKeys = [];
    private DateTime _lastCleanup = DateTime.MinValue;
    private int _lastCheckedMinute = -1;
    private int _lastPreCheckedMinute = -1;

    public event Action<string, string>? ScheduleTriggered;

    /// <summary>스케줄 1분 전 사전 알림 (videoPath, label, 스케줄시각)</summary>
    public event Action<string, string, DateTime>? PreNotification;

    public SchedulerService(TimeSyncService timeSync)
    {
        _timeSync = timeSync;
    }

    public void Start()
    {
        // NTP 기준으로 다음 초 경계에 맞춰 시작
        var now = _timeSync.Now;
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
                    var colonIdx = k.IndexOf(':');
                    if (colonIdx < 0 || colonIdx + 1 >= k.Length) return true;
                    var datepart = k.Substring(colonIdx + 1, Math.Min(10, k.Length - colonIdx - 1));
                    return string.Compare(datepart, today, StringComparison.Ordinal) < 0;
                });
                _preNotifiedKeys.RemoveWhere(k =>
                {
                    var firstColon = k.IndexOf(':');
                    if (firstColon < 0) return true;
                    var secondColon = k.IndexOf(':', firstColon + 1);
                    if (secondColon < 0 || secondColon + 1 >= k.Length) return true;
                    var datepart = k.Substring(secondColon + 1, Math.Min(10, k.Length - secondColon - 1));
                    return string.Compare(datepart, today, StringComparison.Ordinal) < 0;
                });
                _lastCleanup = now;
            }

            // 분이 바뀔 때 체크 (NTP 기준 분 변경 감지 — Second==0 놓침 방지)
            var currentMinute = now.Hour * 60 + now.Minute;

            if (currentMinute != _lastPreCheckedMinute)
            {
                _lastPreCheckedMinute = currentMinute;
                CheckPreNotification(settings, now);
            }

            if (currentMinute != _lastCheckedMinute)
            {
                _lastCheckedMinute = currentMinute;
                CheckTrigger(settings, now);
            }
        }
        catch
        {
            // 다음 틱에서 재시도
        }
    }

    private void CheckPreNotification(AppSettings settings, DateTime now)
    {
        if (PreNotification is null) return;

        // 1분 후 시각과 매칭되는 스케줄 찾기
        var target = now.AddMinutes(1);
        var targetTime = target.ToString("HH:mm");
        var targetDate = target.ToString("yyyy-MM-dd");
        var targetDay = target.DayOfWeek;

        // 스케줄 시각을 정확히 계산
        if (!TryParseTime(targetTime, out var h, out var m)) return;
        var scheduleTime = now.Date.AddHours(h).AddMinutes(m);

        foreach (var s in settings.Schedules.Where(s => s.Enabled))
        {
            if (s.DayOfWeek != targetDay || s.Time != targetTime) continue;
            var key = $"pre:{s.Id}:{targetDate} {targetTime}";
            if (!_preNotifiedKeys.Add(key)) continue;
            PreNotification.Invoke(ResolveVideoPath(s.VideoPath, settings), s.Label, scheduleTime);
            return;
        }

        foreach (var s in settings.OneTimeSchedules)
        {
            if (s.Date != targetDate || s.Time != targetTime) continue;
            var key = $"pre:{s.Id}:{targetDate} {targetTime}";
            if (!_preNotifiedKeys.Add(key)) continue;
            PreNotification.Invoke(ResolveVideoPath(s.VideoPath, settings), s.Label, scheduleTime);
            return;
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

    /// <summary>
    /// 현재 시간 기준으로 재생 중이어야 할 스케줄을 찾는다.
    /// 영상 길이(videoDurationMs)를 기반으로, 스케줄 시작 시각 + 영상 길이 &gt; 현재 시각인 스케줄을 반환한다.
    /// </summary>
    public (string videoPath, TimeSpan elapsed)? FindCurrentSchedule(long videoDurationMs)
    {
        var now = _timeSync.Now;
        var settings = SettingsManager.Current;
        var currentDate = now.ToString("yyyy-MM-dd");
        var currentDay = now.DayOfWeek;

        // 후보 스케줄의 (시작시각, 비디오경로) 수집
        var candidates = new List<(DateTime startTime, string videoPath)>();

        foreach (var s in settings.Schedules.Where(s => s.Enabled))
        {
            if (s.DayOfWeek != currentDay) continue;
            if (!TryParseTime(s.Time, out var h, out var m)) continue;

            var startTime = now.Date.AddHours(h).AddMinutes(m);
            candidates.Add((startTime, ResolveVideoPath(s.VideoPath, settings)));
        }

        foreach (var s in settings.OneTimeSchedules)
        {
            if (s.Date != currentDate) continue;
            if (!TryParseTime(s.Time, out var h, out var m)) continue;

            var startTime = now.Date.AddHours(h).AddMinutes(m);
            candidates.Add((startTime, ResolveVideoPath(s.VideoPath, settings)));
        }

        // 현재 시각 기준으로 아직 영상이 끝나지 않은 가장 최근 스케줄 찾기
        (DateTime startTime, string videoPath)? best = null;
        foreach (var c in candidates)
        {
            var elapsed = now - c.startTime;
            if (elapsed.TotalMilliseconds < 0) continue; // 아직 시작 안 됨
            if (elapsed.TotalMilliseconds >= videoDurationMs) continue; // 이미 끝남

            if (best is null || c.startTime > best.Value.startTime)
                best = c;
        }

        if (best is null) return null;
        return (best.Value.videoPath, now - best.Value.startTime);
    }

    private static bool TryParseTime(string time, out int hour, out int minute)
    {
        hour = 0; minute = 0;
        var parts = time.Split(':');
        if (parts.Length < 2) return false;
        return int.TryParse(parts[0], out hour) && int.TryParse(parts[1], out minute);
    }

    public void Dispose()
    {
        _tickTimer?.Dispose();
        _tickTimer = null;
    }
}

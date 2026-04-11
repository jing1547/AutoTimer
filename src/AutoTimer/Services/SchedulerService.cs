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
    private int _lastCheckedSecond = -1;
    private int _lastPreCheckedSecond = -1;

    public event Action<string, string>? ScheduleTriggered;

    /// <summary>스케줄 1분 전 사전 알림 (videoPath, label, 스케줄시각)</summary>
    public event Action<string, string, DateTime>? PreNotification;

    public SchedulerService(TimeSyncService timeSync)
    {
        _timeSync = timeSync;
    }

    public void Start()
    {
        // 200ms 간격으로 체크 — 분 변경을 즉시 감지하여 지연 최소화
        _tickTimer = new Timer(OnTick, null, 0, 200);
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

            // 초가 바뀔 때마다 체크 — HH:MM:SS 단위 스케줄 지원
            var currentSecond = (now.Hour * 60 + now.Minute) * 60 + now.Second;

            if (currentSecond != _lastPreCheckedSecond)
            {
                _lastPreCheckedSecond = currentSecond;
                CheckPreNotification(settings, now);
            }

            if (currentSecond != _lastCheckedSecond)
            {
                _lastCheckedSecond = currentSecond;
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

        // 주간 스케줄: 1분 전 알림
        foreach (var s in settings.Schedules.Where(s => s.Enabled))
        {
            if (!TryParseTime(s.Time, out var h, out var m, out var sec)) continue;

            // 이번 주의 해당 요일 스케줄 시각 계산 (오늘 기준)
            var scheduleTime = now.Date.AddHours(h).AddMinutes(m).AddSeconds(sec);
            // DayOfWeek가 오늘이 아니면 스킵 (1분 전 시점도 오늘이어야 함)
            if (scheduleTime.DayOfWeek != s.DayOfWeek) continue;

            var preWindowStart = scheduleTime.AddMinutes(-1);
            // 현재 시각이 [scheduleTime-1분, scheduleTime) 구간에 진입했을 때 알림
            if (now < preWindowStart || now >= scheduleTime) continue;

            var key = $"pre:{s.Id}:{scheduleTime:yyyy-MM-dd HH:mm:ss}";
            if (!_preNotifiedKeys.Add(key)) continue;
            PreNotification.Invoke(ResolveVideoPath(s.VideoPath, settings), s.Label, scheduleTime);
            return;
        }

        // 일회성 스케줄: 1분 전 알림
        foreach (var s in settings.OneTimeSchedules)
        {
            if (!DateTime.TryParse(s.Date, out var date)) continue;
            if (!TryParseTime(s.Time, out var h, out var m, out var sec)) continue;
            var scheduleTime = date.Date.AddHours(h).AddMinutes(m).AddSeconds(sec);

            var preWindowStart = scheduleTime.AddMinutes(-1);
            if (now < preWindowStart || now >= scheduleTime) continue;

            var key = $"pre:{s.Id}:{scheduleTime:yyyy-MM-dd HH:mm:ss}";
            if (!_preNotifiedKeys.Add(key)) continue;
            PreNotification.Invoke(ResolveVideoPath(s.VideoPath, settings), s.Label, scheduleTime);
            return;
        }
    }

    private void CheckTrigger(AppSettings settings, DateTime now)
    {
        var currentTime = now.ToString("HH:mm:ss");
        var currentDate = now.ToString("yyyy-MM-dd");
        var currentDay = now.DayOfWeek;

        // 주간 스케줄
        foreach (var s in settings.Schedules.Where(s => s.Enabled))
        {
            if (s.DayOfWeek != currentDay) continue;
            if (NormalizeTime(s.Time) != currentTime) continue;

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
            if (s.Date != currentDate) continue;
            if (NormalizeTime(s.Time) != currentTime) continue;

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

    /// <summary>"HH:mm" 또는 "HH:mm:ss"를 "HH:mm:ss"로 정규화</summary>
    private static string NormalizeTime(string time)
    {
        if (!TryParseTime(time, out var h, out var m, out var s)) return "00:00:00";
        return $"{h:D2}:{m:D2}:{s:D2}";
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
            if (!TryParseTime(s.Time, out var h, out var m, out var sec)) continue;

            var startTime = now.Date.AddHours(h).AddMinutes(m).AddSeconds(sec);
            candidates.Add((startTime, ResolveVideoPath(s.VideoPath, settings)));
        }

        foreach (var s in settings.OneTimeSchedules)
        {
            if (s.Date != currentDate) continue;
            if (!TryParseTime(s.Time, out var h, out var m, out var sec)) continue;

            var startTime = now.Date.AddHours(h).AddMinutes(m).AddSeconds(sec);
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

    private static bool TryParseTime(string time, out int hour, out int minute, out int second)
    {
        hour = 0; minute = 0; second = 0;
        if (string.IsNullOrWhiteSpace(time)) return false;
        var parts = time.Split(':');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out hour)) return false;
        if (!int.TryParse(parts[1], out minute)) return false;
        if (parts.Length >= 3)
            int.TryParse(parts[2], out second);
        return true;
    }

    public void Dispose()
    {
        _tickTimer?.Dispose();
        _tickTimer = null;
    }
}

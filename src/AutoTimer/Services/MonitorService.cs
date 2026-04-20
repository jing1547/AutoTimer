using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoTimer.Services;

public static class MonitorService
{
    private static List<ScreenInfo>? _cachedScreens;
    private static DateTime _cacheTime = DateTime.MinValue;
    private const int CacheTtlSeconds = 3;

    /// <summary>캐시를 강제 무효화한다 (모니터 새로고침 등)</summary>
    public static void InvalidateCache() => _cachedScreens = null;

    public static List<ScreenInfo> GetScreens()
    {
        var now = DateTime.UtcNow;
        if (_cachedScreens is not null && (now - _cacheTime).TotalSeconds < CacheTtlSeconds)
            return _cachedScreens;

        var result = GetScreensCore();
        _cachedScreens = result;
        _cacheTime = now;
        return result;
    }

    private static List<ScreenInfo> GetScreensCore()
    {
        var screens = new List<ScreenInfo>();

        try
        {
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
            {
                try
                {
                    var mi = new NativeMethods.MONITORINFOEX();
                    mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(mi);
                    if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                    {
                        screens.Add(new ScreenInfo
                        {
                            DeviceName = mi.szDevice,
                            Left = mi.rcMonitor.Left,
                            Top = mi.rcMonitor.Top,
                            Width = mi.rcMonitor.Right - mi.rcMonitor.Left,
                            Height = mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                            IsPrimary = (mi.dwFlags & 1) != 0
                        });
                    }
                }
                catch { }
                return true;
            }, nint.Zero);
        }
        catch { }

        return screens;
    }

    /// <summary>
    /// 타겟 모니터를 반환한다.
    /// 설정된 모니터가 연결 해제되었으면 주 모니터로 폴백하고, fallback 여부를 알려준다.
    /// </summary>
    public static (ScreenInfo screen, bool isFallback) GetTargetScreenSafe()
    {
        var screens = GetScreens();

        // 모니터가 하나도 없으면 기본값
        if (screens.Count == 0)
        {
            var dummy = new ScreenInfo { DeviceName = "NONE", Width = 1920, Height = 1080, IsPrimary = true };
            return (dummy, true);
        }

        var target = SettingsManager.Current.Display.TargetMonitor;

        if (!string.IsNullOrWhiteSpace(target))
        {
            var match = screens.FirstOrDefault(s => s.DeviceName == target);
            if (match is not null)
                return (match, false);
        }

        // 설정된 모니터가 없거나 연결 해제됨 → 주 모니터로 폴백
        var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        return (primary, !string.IsNullOrWhiteSpace(target));
    }

    /// <summary>하위 호환용 — 기존 호출부</summary>
    public static ScreenInfo GetTargetScreen()
    {
        return GetTargetScreenSafe().screen;
    }

    /// <summary>
    /// 영상 재생용이 아닌 "보조 모니터"를 반환한다.
    /// UI 창(설정창, 1분전 알림 팝업)이 영상 재생 모니터를 가리지 않도록 자동 선택.
    /// 우선순위: (1) 영상용이 아닌 주 모니터 → (2) 영상용이 아닌 첫 모니터 → (3) 영상용 모니터(1개뿐인 경우).
    /// </summary>
    public static ScreenInfo GetAuxiliaryScreen()
    {
        var screens = GetScreens();
        if (screens.Count == 0)
            return new ScreenInfo { DeviceName = "NONE", Width = 1920, Height = 1080, IsPrimary = true };

        var (videoScreen, _) = GetTargetScreenSafe();
        var videoName = videoScreen.DeviceName;

        var nonVideo = screens.Where(s => s.DeviceName != videoName).ToList();
        if (nonVideo.Count == 0)
            return videoScreen; // 모니터가 1개뿐 — 선택지 없음

        var primary = nonVideo.FirstOrDefault(s => s.IsPrimary);
        return primary ?? nonVideo[0];
    }

    /// <summary>설정된 모니터가 현재 연결되어 있는지 확인</summary>
    public static bool IsTargetMonitorAvailable()
    {
        var target = SettingsManager.Current.Display.TargetMonitor;
        if (string.IsNullOrWhiteSpace(target))
            return true; // 미설정이면 주 모니터 사용하므로 OK

        return GetScreens().Any(s => s.DeviceName == target);
    }
}

public sealed class ScreenInfo
{
    public string DeviceName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public bool IsPrimary { get; set; }

    public string ShortName => DeviceName.Replace(@"\\.\", "");

    public override string ToString() =>
        $"{ShortName} — {Width}x{Height} {(IsPrimary ? "[주]" : "[보조]")}";
}

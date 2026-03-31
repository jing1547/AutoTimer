using System;
using System.Windows;
using System.Windows.Threading;
using AutoTimer.Services;

namespace AutoTimer;

public partial class App : Application
{
    private TrayIconManager? _trayManager;
    private TimeSyncService? _timeSync;
    private SchedulerService? _scheduler;
    private VideoWindow? _activeVideo;
    private Controls.PrePlaybackNotification? _activeNotification;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        if (!SingleInstance.Acquire())
        {
            Controls.CustomDialog.ShowInfo("AutoTimer가 이미 실행 중입니다.");
            Shutdown();
            return;
        }

        try { SettingsManager.Load(); }
        catch { SettingsManager.Load(); }

        // LibVLC 미리 초기화 (첫 재생 시 지연 방지)
        _ = PreloadService.GetLibVLC();

        _timeSync = new TimeSyncService();
        try { await _timeSync.StartAsync(); } catch { }

        _scheduler = new SchedulerService(_timeSync);
        _scheduler.ScheduleTriggered += OnScheduleTriggered;
        _scheduler.PreNotification += OnPreNotification;
        _scheduler.Start();

        _trayManager = new TrayIconManager(_timeSync);
        _trayManager.TestPlayRequested += OnTestPlayRequested;
        _trayManager.RefreshRequested += OnRefreshRequested;
        _trayManager.ForceStopRequested += OnForceStopRequested;
        _trayManager.Initialize();

        _trayManager.OpenSettings();
    }

    private bool _playbackSuppressed;

    private void OnPreNotification(string videoPath, string label, DateTime scheduleTime)
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_activeVideo is not null || _activeNotification is not null)
                return;

            _playbackSuppressed = false;

            // 스케줄 시각 기준으로 카운트다운 — NTP 시간과 정확히 일치
            var popup = new Controls.PrePlaybackNotification(label, scheduleTime, () => _timeSync!.Now);
            _activeNotification = popup;
            popup.Completed += cancelled =>
            {
                _activeNotification = null;
                if (cancelled)
                    _playbackSuppressed = true;
            };
            popup.Show();
        });
    }

    private void OnScheduleTriggered(string videoPath, string label)
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            // 팝업에서 취소했으면 재생 안 함
            if (_playbackSuppressed)
            {
                _playbackSuppressed = false;
                return;
            }

            // 팝업이 아직 떠있으면 닫기 (카운트다운 중 정각 도달)
            if (_activeNotification is not null)
            {
                _activeNotification.Close();
                _activeNotification = null;
            }

            if (_trayManager?.HasUnsavedSettings == true)
                return;

            if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
                return;

            if (_activeVideo is not null && _activeVideo.IsTestPlay)
            {
                _activeVideo.ForceClose();
                _activeVideo = null;
            }

            if (_activeVideo is not null)
                return;

            var (screen, isFallback) = MonitorService.GetTargetScreenSafe();
            if (screen.DeviceName == "NONE" || isFallback)
                return;

            var window = new VideoWindow(screen, isTestPlay: false);
            window.Closed += (_, _) => { if (_activeVideo == window) _activeVideo = null; };
            _activeVideo = window;
            window.Show();
            window.Play(videoPath);
        });
    }

    private void OnTestPlayRequested()
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_trayManager?.HasUnsavedSettings == true)
            {
                var lang = SettingsManager.Current.General.Language;
                var msg = lang == "ko" ? "설정이 변경되었습니다. 먼저 저장해주세요." : "Settings changed. Please save first.";
                Controls.CustomDialog.ShowWarning(msg);
                return;
            }

            if (_activeVideo is not null)
                return;

            var videoPath = SettingsManager.Current.Playback.DefaultVideoPath;
            if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
            {
                var lang = SettingsManager.Current.General.Language;
                var msg = lang == "ko" ? "영상 파일이 설정되지 않았거나 존재하지 않습니다." : "Video file is not set or does not exist.";
                Controls.CustomDialog.ShowWarning(msg);
                return;
            }

            var (screen, isFallback) = MonitorService.GetTargetScreenSafe();
            if (screen.DeviceName == "NONE")
            {
                var lang2 = SettingsManager.Current.General.Language;
                var msg2 = lang2 == "ko" ? "연결된 모니터를 찾을 수 없습니다." : "No connected monitor found.";
                Controls.CustomDialog.ShowWarning(msg2);
                return;
            }

            if (isFallback)
            {
                var lang2 = SettingsManager.Current.General.Language;
                var msg2 = lang2 == "ko"
                    ? "설정된 모니터를 찾을 수 없습니다. 모니터 설정을 확인해주세요."
                    : "Target monitor not found. Please check monitor settings.";
                Controls.CustomDialog.ShowWarning(msg2);
                return;
            }

            var window = new VideoWindow(screen, isTestPlay: true);
            window.Closed += (_, _) => { if (_activeVideo == window) _activeVideo = null; };
            _activeVideo = window;
            window.Show();
            window.Play(videoPath);
        });
    }

    private async void OnRefreshRequested()
    {
        if (Dispatcher.HasShutdownStarted) return;

        if (_scheduler is null) return;

        var videoPath = SettingsManager.Current.Playback.DefaultVideoPath;
        if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
            return;

        // 영상 길이를 가져오기 위해 파싱 (백그라운드에서)
        long videoDurationMs;
        try
        {
            videoDurationMs = await System.Threading.Tasks.Task.Run(() =>
            {
                var libvlc = PreloadService.GetLibVLC();
                using var media = new LibVLCSharp.Shared.Media(libvlc, new Uri(videoPath));
                media.Parse(LibVLCSharp.Shared.MediaParseOptions.ParseLocal).Wait();
                return media.Duration;
            });
            if (videoDurationMs <= 0) return;
        }
        catch { return; }

        // UI 스레드로 복귀
        var result = _scheduler.FindCurrentSchedule(videoDurationMs);
        if (result is null) return; // 현재 재생할 스케줄 없음 — 무응답

        var (resolvedPath, elapsed) = result.Value;
        if (string.IsNullOrWhiteSpace(resolvedPath))
            resolvedPath = videoPath;
        if (!System.IO.File.Exists(resolvedPath))
            return;

        if (_activeVideo is not null)
        {
            // 이미 재생 중 — seek으로 동기화
            _activeVideo.SeekTo(resolvedPath, elapsed);
        }
        else
        {
            // 재생 중이 아님 — 새 창 열고 seek
            var (screen, isFallback) = MonitorService.GetTargetScreenSafe();
            if (screen.DeviceName == "NONE" || isFallback) return;

            var window = new VideoWindow(screen, isTestPlay: false);
            window.Closed += (_, _) => { if (_activeVideo == window) _activeVideo = null; };
            _activeVideo = window;
            window.Show();
            window.SeekTo(resolvedPath, elapsed);
        }
    }

    private void OnForceStopRequested()
    {
        if (Dispatcher.HasShutdownStarted) return;

        if (_activeVideo is null) return;
        _activeVideo.ForceClose();
        _activeVideo = null;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoTimer", "error.log"),
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n\n");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) { }

    protected override void OnExit(ExitEventArgs e)
    {
        // If settings window is open and dirty, save before shutdown
        if (_trayManager?.HasUnsavedSettings == true)
        {
            try { SettingsManager.Save(); } catch { }
        }

        if (_scheduler is not null)
        {
            _scheduler.ScheduleTriggered -= OnScheduleTriggered;
            _scheduler.PreNotification -= OnPreNotification;
            _scheduler.Dispose();
        }
        if (_trayManager is not null)
        {
            _trayManager.TestPlayRequested -= OnTestPlayRequested;
            _trayManager.RefreshRequested -= OnRefreshRequested;
            _trayManager.ForceStopRequested -= OnForceStopRequested;
        }
        _timeSync?.Dispose();
        _trayManager?.Dispose();
        PreloadService.Shutdown();
        SingleInstance.Release();
        base.OnExit(e);
    }
}

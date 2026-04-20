using System;
using System.Linq;
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
        _scheduler.OneTimeScheduleAutoDelete += OnOneTimeScheduleAutoDelete;
        _scheduler.Start();

        _trayManager = new TrayIconManager(_timeSync);
        _trayManager.TestPlayRequested += OnTestPlayRequested;
        _trayManager.RefreshRequested += OnRefreshRequested;
        _trayManager.ForceStopRequested += OnForceStopRequested;
        _trayManager.Initialize();
    }

    private bool _playbackSuppressed;
    private string? _pendingVideoPath;

    private void OnPreNotification(string videoPath, string label, DateTime scheduleTime)
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_activeVideo is not null || _activeNotification is not null)
                return;

            _playbackSuppressed = false;
            _pendingVideoPath = videoPath;

            var popup = new Controls.PrePlaybackNotification(label, scheduleTime, () => _timeSync!.Now);
            _activeNotification = popup;
            popup.Completed += cancelled =>
            {
                _activeNotification = null;
                if (cancelled)
                {
                    // 재생 취소 — 이 스케줄의 ScheduleTriggered가 와도 무시
                    _playbackSuppressed = true;
                    _pendingVideoPath = null;
                }
                else
                {
                    // 확인 — 안내 창만 닫음. 실제 재생은 ScheduleTriggered에서 처리.
                    _pendingVideoPath = null;
                }
            };
            popup.Show();
        });
    }

    /// <summary>
    /// 스케줄러가 일회성 스케줄 자동삭제를 요청. 설정창이 열려있으면 UI 컬렉션도 제거해
    /// HasUnsavedSettings가 false positive를 내지 않게 한다. 어느 경우든 설정 저장.
    /// </summary>
    private void OnOneTimeScheduleAutoDelete(string id)
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            var ui = _trayManager?.ActiveSettingsWindow;
            if (ui is not null)
            {
                ui.RemoveOneTimeScheduleById(id);
            }

            var list = SettingsManager.Current.OneTimeSchedules;
            var match = list.FirstOrDefault(s => s.Id == id);
            if (match is not null)
                list.Remove(match);

            SettingsManager.Save();
        });
    }

    private void OnScheduleTriggered(string videoPath, string label)
    {
        if (Dispatcher.HasShutdownStarted) { DbgLog("OnScheduleTriggered shutdown"); return; }
        DbgLog($"OnScheduleTriggered enqueue videoPath={videoPath}");

        Dispatcher.BeginInvoke(() =>
        {
            DbgLog($"OnScheduleTriggered run suppressed={_playbackSuppressed} activeVideo={_activeVideo is not null} activeNotif={_activeNotification is not null}");
            if (_playbackSuppressed)
            {
                _playbackSuppressed = false;
                DbgLog("OnScheduleTriggered return: suppressed");
                return;
            }

            // 팝업에서 이미 재생 시작했으면 스킵
            if (_activeVideo is not null && !_activeVideo.IsTestPlay)
            {
                DbgLog("OnScheduleTriggered return: activeVideo not test");
                return;
            }

            // 팝업이 아직 떠있으면 닫기
            if (_activeNotification is not null)
            {
                _activeNotification.Close();
                _activeNotification = null;
            }

            StartPlayback(videoPath);
        });
    }

    private static void DbgLog(string msg)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoTimer");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "app.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    private void StartPlayback(string videoPath)
    {
        DbgLog($"StartPlayback enter videoPath={videoPath}");
        if (_trayManager?.HasUnsavedSettings == true)
        { DbgLog("StartPlayback return: HasUnsavedSettings"); return; }

        if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
        { DbgLog($"StartPlayback return: invalid path exists={System.IO.File.Exists(videoPath)}"); return; }

        if (_activeVideo is not null && _activeVideo.IsTestPlay)
        {
            _activeVideo.ForceClose();
            _activeVideo = null;
        }

        if (_activeVideo is not null)
        { DbgLog("StartPlayback return: activeVideo exists"); return; }

        var (screen, isFallback) = MonitorService.GetTargetScreenSafe();
        if (screen.DeviceName == "NONE" || isFallback)
        { DbgLog($"StartPlayback return: monitor fallback device={screen.DeviceName} fb={isFallback}"); return; }
        DbgLog("StartPlayback creating VideoWindow");

        // JW Library(UWP 전체화면)는 Win+D/SendInput을 무시하므로 공식 UWP API인
        // AppDiagnosticInfo.StartSuspendAsync로 직접 서스펜드시킨다.
        // 비동기이지만 await 안 해도 OS 레벨 효과는 즉시 시작됨 — VideoWindow 생성과 병렬 진행.
        _ = UwpSuspender.SuspendJwLibraryAsync();

        var window = new VideoWindow(screen, isTestPlay: false);
        window.Closed += (_, _) => { if (_activeVideo == window) _activeVideo = null; };
        _activeVideo = window;
        window.Show();
        window.Play(videoPath);
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

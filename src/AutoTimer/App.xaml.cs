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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        if (!SingleInstance.Acquire())
        {
            MessageBox.Show("AutoTimer가 이미 실행 중입니다.", "AutoTimer", MessageBoxButton.OK, MessageBoxImage.Information);
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
        _scheduler.Start();

        _trayManager = new TrayIconManager(_timeSync);
        _trayManager.TestPlayRequested += OnTestPlayRequested;
        _trayManager.Initialize();

        _trayManager.OpenSettings();
    }

    private void OnScheduleTriggered(string videoPath, string label)
    {
        if (Dispatcher.HasShutdownStarted) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_trayManager?.HasUnsavedSettings == true)
                return;

            if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
                return;

            // 기존 테스트 재생이 있으면 닫고 실제 재생 우선
            if (_activeVideo is not null && _activeVideo.IsTestPlay)
            {
                _activeVideo.ForceClose();
                _activeVideo = null;
            }

            if (_activeVideo is not null)
                return;

            var (screen, _) = MonitorService.GetTargetScreenSafe();
            if (screen.DeviceName == "NONE")
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
                MessageBox.Show(msg, "AutoTimer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_activeVideo is not null)
                return;

            var videoPath = SettingsManager.Current.Playback.DefaultVideoPath;
            if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
            {
                var lang = SettingsManager.Current.General.Language;
                var msg = lang == "ko" ? "영상 파일이 설정되지 않았거나 존재하지 않습니다." : "Video file is not set or does not exist.";
                MessageBox.Show(msg, "AutoTimer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (screen, isFallback) = MonitorService.GetTargetScreenSafe();
            if (screen.DeviceName == "NONE")
            {
                var lang2 = SettingsManager.Current.General.Language;
                var msg2 = lang2 == "ko" ? "연결된 모니터를 찾을 수 없습니다." : "No connected monitor found.";
                MessageBox.Show(msg2, "AutoTimer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isFallback)
            {
                var lang2 = SettingsManager.Current.General.Language;
                var msg2 = lang2 == "ko"
                    ? $"설정된 모니터가 연결되지 않아 주 모니터({screen.DeviceName})에서 재생합니다."
                    : $"Target monitor disconnected. Playing on primary ({screen.DeviceName}).";
                MessageBox.Show(msg2, "AutoTimer", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            var window = new VideoWindow(screen, isTestPlay: true);
            window.Closed += (_, _) => { if (_activeVideo == window) _activeVideo = null; };
            _activeVideo = window;
            window.Show();
            window.Play(videoPath);
        });
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
        _scheduler?.Dispose();
        _timeSync?.Dispose();
        _trayManager?.Dispose();
        PreloadService.Shutdown();
        SingleInstance.Release();
        base.OnExit(e);
    }
}

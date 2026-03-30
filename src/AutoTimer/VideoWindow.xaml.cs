using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AutoTimer.Services;
using LibVLCSharp.Shared;

namespace AutoTimer;

public partial class VideoWindow : Window
{
    private readonly ScreenInfo _screen;
    private readonly bool _lockWindow;
    private readonly bool _clickThrough;
    private readonly bool _fadeOut;
    private readonly int _fadeOutMs;
    private MediaPlayer? _mediaPlayer;
    private bool _closing;
    private DispatcherTimer? _watchdogTimer;
    private DispatcherTimer? _monitorCheckTimer;

    public bool IsTestPlay { get; }

    public VideoWindow(ScreenInfo screen, bool isTestPlay = false)
    {
        IsTestPlay = isTestPlay;
        InitializeComponent();

        _screen = screen;

        var settings = SettingsManager.Current;
        _lockWindow = settings.Playback.LockWindow;
        _clickThrough = settings.Playback.MouseClickThrough;
        _fadeOut = settings.Display.FadeOutEnabled;
        _fadeOutMs = settings.Display.FadeOutDurationMs;

        if (_lockWindow)
            Topmost = true;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = _screen.Left;
        Top = _screen.Top;
        Width = _screen.Width;
        Height = _screen.Height;

        if (_clickThrough)
        {
            // Loaded 후 1프레임 뒤에 적용 — VideoView HWND 생성 대기
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, SetClickThrough);
        }
    }

    public void Play(string videoPath)
    {
        try
        {
            // 기존 타이머 정리 (Play 재호출 시 누수 방지)
            _watchdogTimer?.Stop();
            _monitorCheckTimer?.Stop();

            CleanupPlayer();
            var libvlc = PreloadService.GetLibVLC();
            _mediaPlayer = new MediaPlayer(libvlc);
            _mediaPlayer.EndReached += OnEndReached;
            VideoView.MediaPlayer = _mediaPlayer;

            using var media = new Media(libvlc, new Uri(videoPath));
            _mediaPlayer.Play(media);

            // Watchdog: if EndReached doesn't fire within a reasonable time, allow close
            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _watchdogTimer.Tick += (_, _) => { _watchdogTimer?.Stop(); CleanupAndClose(); };
            _watchdogTimer.Start();

            // Monitor disconnect detection
            _monitorCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _monitorCheckTimer.Tick += OnMonitorCheck;
            _monitorCheckTimer.Start();
        }
        catch
        {
            // Bad file or LibVLC error — close gracefully
            CleanupAndClose();
        }
    }

    private void OnMonitorCheck(object? sender, EventArgs e)
    {
        if (_closing) return;

        // Check if window is off-screen (monitor disconnected)
        var screens = MonitorService.GetScreens();
        bool onScreen = false;
        foreach (var s in screens)
        {
            if (Left >= s.Left && Left < s.Left + s.Width &&
                Top >= s.Top && Top < s.Top + s.Height)
            {
                onScreen = true;
                break;
            }
        }

        if (!onScreen)
            CleanupAndClose();
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        if (_closing) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;

            if (_fadeOut && _fadeOutMs > 0)
            {
                // 검은 오버레이를 0→1로 페이드인 = 영상이 페이드아웃되는 효과
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_fadeOutMs));
                anim.Completed += (_, _) => CleanupAndClose();
                FadeOverlay.BeginAnimation(OpacityProperty, anim);
            }
            else
            {
                CleanupAndClose();
            }
        });
    }

    private void CleanupPlayer()
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.EndReached -= OnEndReached;
            VideoView.MediaPlayer = null;
            try { _mediaPlayer.Stop(); } catch { }
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }
    }

    private void CleanupAndClose()
    {
        if (_closing) return;
        _closing = true;
        _watchdogTimer?.Stop();
        _monitorCheckTimer?.Stop();
        CleanupPlayer();
        Close();
    }

    public void ForceClose()
    {
        _closing = true;
        CleanupPlayer();
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_lockWindow && e.Key == Key.System && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void SetClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero) return;
        var extStyle = NativeMethods.GetWindowLong(hwnd, -20);
        NativeMethods.SetWindowLong(hwnd, -20, extStyle | 0x20);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closing = true;
        _watchdogTimer?.Stop();
        _monitorCheckTimer?.Stop();
        CleanupPlayer();
        base.OnClosed(e);
    }
}

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AutoTimer.Controls;

public partial class PrePlaybackNotification : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Func<DateTime> _getNow;
    private readonly string _label;
    private readonly string _lang;
    private readonly int _totalSeconds;
    private DateTime _endTime;

    public bool Cancelled { get; private set; } = true;
    public event Action<bool>? Completed;

    public PrePlaybackNotification(string label, int countdownSeconds, Func<DateTime>? getNow = null)
    {
        InitializeComponent();

        _getNow = getNow ?? (() => DateTime.Now);
        _label = label;
        _lang = Services.SettingsManager.Current.General.Language;
        _totalSeconds = countdownSeconds;

        // 목표 시각 = 현재 NTP 시각 + countdownSeconds
        _endTime = _getNow().AddSeconds(countdownSeconds);

        BtnCancel.Content = _lang == "ko" ? "재생 취소" : "Cancel";
        BtnCancel.Foreground = (Brush)FindResource("AccentBrush");
        BtnCancel.Background = (Brush)FindResource("BgHoverBrush");
        BtnCancel.BorderBrush = (Brush)FindResource("AccentDimBrush");
        BtnCancel.BorderThickness = new Thickness(1);

        UpdateMessage();

        // 200ms 간격으로 체크 — 시간 표시가 NTP와 정확히 일치
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTick;

        Loaded += (_, _) =>
        {
            Activate();
            _timer.Start();
        };
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _getNow();
        var remaining = _endTime - now;

        if (remaining.TotalSeconds <= 0)
        {
            _timer.Stop();
            Cancelled = false;
            Close();
            return;
        }

        UpdateMessage();
    }

    private void UpdateMessage()
    {
        var now = _getNow();
        var remaining = (int)Math.Ceiling((_endTime - now).TotalSeconds);
        if (remaining < 0) remaining = 0;

        var labelPart = string.IsNullOrWhiteSpace(_label) ? "" : $"[{_label}] ";

        TxtMessage.Text = _lang == "ko"
            ? $"{labelPart}{remaining}초 후 영상 재생 시작\n현재 시각: {now:HH:mm:ss}"
            : $"{labelPart}Playback in {remaining}s\nTime: {now:HH:mm:ss}";
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Cancelled = true;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        Completed?.Invoke(Cancelled);
        base.OnClosed(e);
    }
}

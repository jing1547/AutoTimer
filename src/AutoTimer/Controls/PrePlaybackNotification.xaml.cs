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
    private readonly DateTime _scheduleTime;
    private readonly string _label;
    private readonly string _lang;

    public bool Cancelled { get; private set; } = true;
    public event Action<bool>? Completed;

    /// <param name="label">스케줄 라벨</param>
    /// <param name="scheduleTime">실제 스케줄 시각 (NTP 기준)</param>
    /// <param name="getNow">NTP 현재 시각 제공 함수</param>
    public PrePlaybackNotification(string label, DateTime scheduleTime, Func<DateTime> getNow)
    {
        InitializeComponent();

        _getNow = getNow;
        _scheduleTime = scheduleTime;
        _label = label;
        _lang = Services.SettingsManager.Current.General.Language;

        BtnCancel.Content = _lang == "ko" ? "재생 취소" : "Cancel";
        BtnCancel.Foreground = (Brush)FindResource("AccentBrush");
        BtnCancel.Background = (Brush)FindResource("BgHoverBrush");
        BtnCancel.BorderBrush = (Brush)FindResource("AccentDimBrush");
        BtnCancel.BorderThickness = new Thickness(1);

        UpdateMessage();

        // 200ms 간격 — NTP 시간과 항상 동기
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTick;

        Loaded += (_, _) =>
        {
            Activate();
            _timer.Start();
        };
    }

    /// <summary>테스트용 — scheduleTime 없이 고정 카운트다운</summary>
    public PrePlaybackNotification(string label, int countdownSeconds, Func<DateTime> getNow)
        : this(label, getNow().AddSeconds(countdownSeconds), getNow)
    {
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _getNow();
        if (now >= _scheduleTime)
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
        var remaining = (int)Math.Ceiling((_scheduleTime - now).TotalSeconds);
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

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

        BtnOk.Content = _lang == "ko" ? "확인" : "OK";
        BtnOk.Foreground = (Brush)FindResource("AccentBrush");
        BtnOk.Background = (Brush)FindResource("BgHoverBrush");
        BtnOk.BorderBrush = (Brush)FindResource("AccentDimBrush");
        BtnOk.BorderThickness = new Thickness(1);

        BtnCancel.Content = _lang == "ko" ? "재생 취소" : "Cancel";
        BtnCancel.Foreground = (Brush)FindResource("FgBrush");
        BtnCancel.Background = (Brush)FindResource("BgHoverBrush");
        BtnCancel.BorderBrush = (Brush)FindResource("BorderBrush");
        BtnCancel.BorderThickness = new Thickness(1);

        UpdateMessage();

        // 200ms 간격 — NTP 시간과 항상 동기
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTick;

        Loaded += (_, _) =>
        {
            // 영상 재생 모니터가 아닌 보조 모니터 중앙에 배치
            var screen = Services.MonitorService.GetAuxiliaryScreen();
            Left = screen.Left + (screen.Width - Width) / 2;
            Top = screen.Top + (screen.Height - Height) / 2;

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

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // 안내 창만 닫음 — 재생은 스케줄 시각에 맞춰 자동 트리거됨
        _timer.Stop();
        Cancelled = false;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        // 재확인 다이얼로그 — 이 창은 닫지 않고 유지
        _timer.Stop();
        var msg = _lang == "ko"
            ? "정말 재생을 취소하시겠습니까?"
            : "Are you sure you want to cancel playback?";
        var title = _lang == "ko" ? "재생 취소 확인" : "Cancel playback";
        bool confirmed = CustomDialog.ShowYesNo(msg, title, this);
        if (confirmed)
        {
            Cancelled = true;
            Close();
        }
        else
        {
            // 재확인에서 '아니오' — 카운트다운 재개
            _timer.Start();
        }
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

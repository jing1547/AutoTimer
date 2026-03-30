using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AutoTimer.Models;
using AutoTimer.Services;
using Microsoft.Win32;

namespace AutoTimer;

public partial class SettingsWindow : Window
{
    private readonly TimeSyncService _timeSync;
    private readonly DispatcherTimer _clockTimer;
    private string _lang = "ko";
    private string _theme = "dark";
    private bool _initialized;

    private List<ScreenInfo>? _cachedScreens;
    private DateTime _screensCacheTime = DateTime.MinValue;

    private List<ScreenInfo> GetCachedScreens()
    {
        var now = DateTime.UtcNow;
        if (_cachedScreens is null || (now - _screensCacheTime).TotalSeconds >= 5)
        {
            _cachedScreens = MonitorService.GetScreens();
            _screensCacheTime = now;
        }
        return _cachedScreens;
    }

    public bool IsDirty
    {
        get
        {
            if (!_initialized) return false;
            var s = SettingsManager.Current;

            if ((ChkRunOnStartup.IsChecked == true) != s.General.RunOnStartup) return true;
            if ((RbServer.IsChecked == true ? "server" : "local") != s.General.TimeSource) return true;
            if (_lang != s.General.Language) return true;
            if (_theme != s.General.Theme) return true;

            var screens = GetCachedScreens();
            if (CmbMonitor.SelectedIndex >= 0 && CmbMonitor.SelectedIndex < screens.Count)
            {
                if (screens[CmbMonitor.SelectedIndex].DeviceName != s.Display.TargetMonitor) return true;
            }

            if (_fullVideoPath != s.Playback.DefaultVideoPath) return true;

            // 스케줄 비교
            if (WeeklyItems.Count != s.Schedules.Count) return true;
            for (int i = 0; i < WeeklyItems.Count; i++)
            {
                var ui = WeeklyItems[i];
                var saved = s.Schedules[i];
                if (ui.Enabled != saved.Enabled) return true;
                var uiModel = ui.ToModel();
                if (uiModel.DayOfWeek != saved.DayOfWeek) return true;
                if (ui.Time != saved.Time) return true;
                if (ui.Label != saved.Label) return true;
            }

            if (OneTimeItems.Count != s.OneTimeSchedules.Count) return true;
            for (int i = 0; i < OneTimeItems.Count; i++)
            {
                var ui = OneTimeItems[i];
                var saved = s.OneTimeSchedules[i];
                if (ui.Date != saved.Date) return true;
                if (ui.Time != saved.Time) return true;
                if (ui.Label != saved.Label) return true;
            }

            return false;
        }
    }

    public static readonly List<string> DayOptionsEn = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    public static readonly List<string> DayOptionsKo = ["월", "화", "수", "목", "금", "토", "일"];
    public static readonly List<string> DayKeys = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private List<string> _dayOptions = DayOptionsKo;
    public List<string> DayOptions => _dayOptions;

    public ObservableCollection<WeeklyScheduleVM> WeeklyItems { get; } = [];
    public ObservableCollection<OneTimeScheduleVM> OneTimeItems { get; } = [];

    public event Action? TestPlayRequested;
    public event Action? RefreshRequested;
    public event Action? ForceStopRequested;

    public SettingsWindow(TimeSyncService timeSync)
    {
        InitializeComponent();
        _timeSync = timeSync;

        // 언어/테마 콤보 초기화
        CmbLanguage.Items.Add("한국어");
        CmbLanguage.Items.Add("English");

        LoadFromSettings();
        RestoreWindowSize();
        ApplyTheme(_theme);
        ApplyLanguage(_lang);

        WeeklyList.ItemsSource = WeeklyItems;
        OneTimeList.ItemsSource = OneTimeItems;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        _initialized = true;
    }

    // ===== 테마 =====
    private void ApplyTheme(string theme)
    {
        var actual = theme;
        if (actual == "system")
        {
            // Windows 앱 모드 감지
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                actual = val is int i && i == 1 ? "light" : "dark";
            }
            catch { actual = "dark"; }
        }

        var res = Application.Current.Resources;

        if (actual == "dark")
        {
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12));
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            res["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xAA));
            res["AccentDimBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x8B, 0x70));
            res["BgPanelBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
            res["BgInputBrush"] = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x1A));
            res["BgHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E));
            res["FgBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            res["FgDimBrush"] = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90));
            res["RedBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x40, 0x60));
            res["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E));
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            res["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x6B, 0x50));
            res["AccentDimBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x90, 0x70));
            res["BgPanelBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE8));
            res["BgInputBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xFC));
            res["BgHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD8));
            res["FgBrush"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            res["FgDimBrush"] = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80));
            res["RedBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0x30, 0x40));
            res["BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB8));
        }
    }

    // ===== 언어 =====
    private void ApplyLanguage(string lang)
    {
        // 테마 콤보 언어 갱신
        var themeIdx = CmbTheme.SelectedIndex;
        if (themeIdx < 0)
            themeIdx = _theme switch { "dark" => 0, "light" => 1, _ => 2 };
        CmbTheme.Items.Clear();
        if (lang == "ko")
        {
            CmbTheme.Items.Add("다크");
            CmbTheme.Items.Add("라이트");
            CmbTheme.Items.Add("시스템");
        }
        else
        {
            CmbTheme.Items.Add("Dark");
            CmbTheme.Items.Add("Light");
            CmbTheme.Items.Add("System");
        }
        CmbTheme.SelectedIndex = themeIdx;

        var oldOptions = _dayOptions;
        _dayOptions = lang == "ko" ? DayOptionsKo : DayOptionsEn;

        // 기존 스케줄 요일 표시를 새 언어로 변환
        foreach (var item in WeeklyItems)
        {
            var idx = oldOptions.IndexOf(item.DayOfWeekDisplay);
            if (idx >= 0)
                item.DayOfWeekDisplay = _dayOptions[idx];
        }
        WeeklyList.ItemsSource = null;
        WeeklyList.ItemsSource = WeeklyItems;

        if (lang == "ko")
        {
            TxtSubtitle.Text = " 설정";
            LblGeneral.Text = "일반";
            ChkRunOnStartup.Content = "시작 프로그램 등록";
            LblTimeSource.Text = "시간 소스";
            RbServer.Content = "서버";
            RbLocal.Content = "로컬";

            LblDisplay.Text = "화면";
            LblMonitor.Text = "모니터";
            LblPlayback.Text = "재생";
            LblDefaultVideo.Text = "영상 선택";
            LblWeekly.Text = "주간 스케줄";
            BtnAddWeekly.Content = "+ 스케줄 추가";
            LblOneTime.Text = "일회성 타이머";

            BtnAddOneTime.Content = "+ 타이머 추가";
            BtnTestPlay.Content = "테스트 재생";
            BtnRefresh.Content = "새로고침";
            BtnForceStop.Content = "영상 종료";
            BtnSync.Content = "서버 동기화";
            BtnSave.Content = "저장";
        }
        else
        {
            TxtSubtitle.Text = " Settings";
            LblGeneral.Text = "General";
            ChkRunOnStartup.Content = "Start with Windows";
            LblTimeSource.Text = "Time source";
            RbServer.Content = "Server";
            RbLocal.Content = "Local";

            LblDisplay.Text = "Display";
            LblMonitor.Text = "Monitor";
            LblPlayback.Text = "Playback";
            LblDefaultVideo.Text = "Select video";
            LblWeekly.Text = "Weekly schedules";
            BtnAddWeekly.Content = "+ Add schedule";
            LblOneTime.Text = "One-time timers";

            BtnAddOneTime.Content = "+ Add timer";
            BtnTestPlay.Content = "Test play";
            BtnRefresh.Content = "Refresh";
            BtnForceStop.Content = "Stop video";
            BtnSync.Content = "Sync now";
            BtnSave.Content = "SAVE";
        }
    }

    private void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        _lang = CmbLanguage.SelectedIndex == 0 ? "ko" : "en";
        ApplyLanguage(_lang);
    }

    private void OnThemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        _theme = CmbTheme.SelectedIndex switch { 0 => "dark", 1 => "light", _ => "system" };
        ApplyTheme(_theme);
    }

    // ===== 설정 로드/저장 =====
    private void LoadFromSettings()
    {
        var s = SettingsManager.Current;

        _lang = s.General.Language;
        _theme = s.General.Theme;
        CmbLanguage.SelectedIndex = _lang == "ko" ? 0 : 1;
        // CmbTheme는 ApplyLanguage에서 Items를 채운 뒤 설정됨

        ChkRunOnStartup.IsChecked = s.General.RunOnStartup;
        RbServer.IsChecked = s.General.TimeSource == "server";
        RbLocal.IsChecked = s.General.TimeSource == "local";

        var screens = MonitorService.GetScreens();
        CmbMonitor.Items.Clear();
        foreach (var screen in screens)
            CmbMonitor.Items.Add(screen.ToString());
        var targetIdx = screens.FindIndex(sc => sc.DeviceName == s.Display.TargetMonitor);
        CmbMonitor.SelectedIndex = targetIdx >= 0 ? targetIdx : 0;

        SetVideoPathDisplay(s.Playback.DefaultVideoPath);

        WeeklyItems.Clear();
        foreach (var w in s.Schedules)
            WeeklyItems.Add(new WeeklyScheduleVM(w));

        OneTimeItems.Clear();
        foreach (var o in s.OneTimeSchedules)
            OneTimeItems.Add(new OneTimeScheduleVM(o));
    }

    private void SaveToSettings()
    {
        var s = SettingsManager.Current;

        s.General.Language = _lang;
        s.General.Theme = _theme;
        s.General.RunOnStartup = ChkRunOnStartup.IsChecked == true;
        s.General.TimeSource = RbServer.IsChecked == true ? "server" : "local";
        var screens = MonitorService.GetScreens();
        if (CmbMonitor.SelectedIndex >= 0 && CmbMonitor.SelectedIndex < screens.Count)
            s.Display.TargetMonitor = screens[CmbMonitor.SelectedIndex].DeviceName;

        s.Playback.DefaultVideoPath = _fullVideoPath;

        s.Schedules = WeeklyItems.Select(vm => vm.ToModel()).ToList();
        s.OneTimeSchedules = OneTimeItems.Select(vm => vm.ToModel()).ToList();

        SettingsManager.Save();
        SetStartup(s.General.RunOnStartup);
    }

    private DateTime _lastTimeZoneClear = DateTime.MinValue;

    private void UpdateClock()
    {
        var isServer = RbServer.IsChecked == true;
        DateTime now;

        // 시간대 변경 감지 (5초마다 캐시 갱신)
        var utcNow = DateTime.UtcNow;
        if ((utcNow - _lastTimeZoneClear).TotalSeconds >= 5)
        {
            TimeZoneInfo.ClearCachedData();
            _lastTimeZoneClear = utcNow;
        }

        if (isServer)
        {
            now = _timeSync.GetNow(true);

            if (_timeSync.LastResult == SyncResult.NetworkError)
            {
                var offline = _lang == "ko" ? "[오프라인]" : "[OFFLINE]";
                TxtCurrentTime.Text = $"{now:yyyy-MM-dd  HH:mm:ss.ff}  {offline}";
            }
            else
            {
                TxtCurrentTime.Text = $"{now:yyyy-MM-dd  HH:mm:ss.ff}";
            }
        }
        else
        {
            now = DateTime.Now;
            TxtCurrentTime.Text = $"{now:yyyy-MM-dd  HH:mm:ss.ff}";
        }
    }

    private void OnBrowseVideo(object sender, RoutedEventArgs e)
    {
        var filter = _lang == "ko"
            ? "동영상 파일|*.mp4;*.avi;*.mkv;*.wmv;*.mov|모든 파일|*.*"
            : "Video files|*.mp4;*.avi;*.mkv;*.wmv;*.mov|All files|*.*";
        var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog() == true)
            SetVideoPathDisplay(dlg.FileName);
    }

    private string _fullVideoPath = "";

    private void SetVideoPathDisplay(string fullPath)
    {
        _fullVideoPath = fullPath;
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            TxtVideoPath.Text = "";
            TxtVideoPath.ToolTip = null;
        }
        else
        {
            TxtVideoPath.Text = System.IO.Path.GetFileName(fullPath);
            TxtVideoPath.ToolTip = fullPath;
        }
    }

    private void OnRefreshMonitors(object sender, RoutedEventArgs e)
    {
        MonitorService.InvalidateCache();
        _cachedScreens = null; // 로컬 캐시도 무효화
        var prevSelected = CmbMonitor.SelectedItem?.ToString();
        var screens = MonitorService.GetScreens();
        CmbMonitor.Items.Clear();
        foreach (var screen in screens)
            CmbMonitor.Items.Add(screen.ToString());

        // 이전 선택 복원 시도
        var restored = false;
        if (prevSelected is not null)
        {
            for (int i = 0; i < CmbMonitor.Items.Count; i++)
            {
                if (CmbMonitor.Items[i]?.ToString() == prevSelected)
                {
                    CmbMonitor.SelectedIndex = i;
                    restored = true;
                    break;
                }
            }
        }

        if (!restored)
        {
            CmbMonitor.SelectedIndex = screens.Count > 0 ? 0 : -1;
            if (prevSelected is not null && screens.Count > 0)
            {
                var msg = _lang == "ko"
                    ? $"이전 모니터가 연결 해제되었습니다. 목록이 갱신되었습니다."
                    : $"Previous monitor disconnected. List refreshed.";
                Controls.CustomDialog.ShowInfo(msg, "AutoTimer", this);
            }
        }
    }

    private void OnAddWeekly(object sender, RoutedEventArgs e)
        => WeeklyItems.Add(new WeeklyScheduleVM(new WeeklySchedule()));

    private void OnDeleteWeekly(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            var item = WeeklyItems.FirstOrDefault(x => x.Id == id);
            if (item is not null) WeeklyItems.Remove(item);
        }
    }

    private void OnAddOneTime(object sender, RoutedEventArgs e)
        => OneTimeItems.Add(new OneTimeScheduleVM(new OneTimeSchedule { Date = DateTime.Now.ToString("yyyy-MM-dd") }));

    private void OnDeleteOneTime(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            var item = OneTimeItems.FirstOrDefault(x => x.Id == id);
            if (item is not null) OneTimeItems.Remove(item);
        }
    }

    private void OnTestPlay(object sender, RoutedEventArgs e) => TestPlayRequested?.Invoke();
    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();
    private void OnForceStop(object sender, RoutedEventArgs e) => ForceStopRequested?.Invoke();

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        if (_timeSync.IsSyncing) return;

        BtnSync.IsEnabled = false;
        await _timeSync.SyncOnceAsync();
        BtnSync.IsEnabled = true;

        var result = _timeSync.LastResult;
        if (result == SyncResult.NetworkError)
        {
            var msg = _lang == "ko"
                ? "NTP 서버에 연결할 수 없습니다.\n인터넷 연결을 확인해주세요."
                : "Cannot reach NTP server.\nCheck your internet connection.";
            Controls.CustomDialog.ShowWarning(msg, "AutoTimer", this);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            // 저장 전: UI에서 선택한 모니터가 현재 연결되어 있는지 검증
            MonitorService.InvalidateCache();
            _cachedScreens = null;
            var currentScreens = MonitorService.GetScreens();
            if (CmbMonitor.SelectedIndex >= 0 && CmbMonitor.SelectedIndex < currentScreens.Count)
            {
                var selectedName = currentScreens[CmbMonitor.SelectedIndex].DeviceName;
                var liveScreens = MonitorService.GetScreens();
                if (!liveScreens.Any(s => s.DeviceName == selectedName))
                {
                    OnRefreshMonitors(sender, e);
                    var msg = _lang == "ko"
                        ? "선택한 모니터가 연결되지 않아 목록을 갱신했습니다. 모니터를 다시 선택해주세요."
                        : "Selected monitor disconnected. List refreshed. Please re-select.";
                    Controls.CustomDialog.ShowWarning(msg, "AutoTimer", this);
                    return;
                }
            }

            SaveToSettings();
            _timeSync.RestartTimer();
        }
        catch (Exception ex)
        {
            var msg = _lang == "ko" ? $"저장 실패: {ex.Message}" : $"Save failed: {ex.Message}";
            Controls.CustomDialog.ShowWarning(msg, "AutoTimer", this);
        }
    }

    private static void SetStartup(bool enable)
    {
        const string keyName = "AutoTimer";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null) return;
            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(keyName, $"\"{exePath}\"");
            }
            else
                key.DeleteValue(keyName, false);
        }
        catch { }
    }

    private void OnTimeLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb) return;

        var text = tb.Text.Trim();
        var parts = text.Split(':');

        int h = 0, m = 0;
        if (parts.Length >= 1) int.TryParse(parts[0], out h);
        if (parts.Length >= 2) int.TryParse(parts[1], out m);

        h = Math.Clamp(h, 0, 23);
        m = Math.Clamp(m, 0, 59);

        tb.Text = $"{h:D2}:{m:D2}";
    }

    private void OnDateLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb) return;

        if (!DateTime.TryParse(tb.Text, out var date))
            date = DateTime.Now;

        tb.Text = date.ToString("yyyy-MM-dd");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private const int WM_NCHITTEST = 0x0084;
    private const int BORDER = 6;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            var pt = PointFromScreen(new Point(
                (short)(lParam.ToInt32() & 0xFFFF),
                (short)(lParam.ToInt32() >> 16)));

            int result = 1; // HTCLIENT
            bool left = pt.X < BORDER;
            bool right = pt.X > ActualWidth - BORDER;
            bool top = pt.Y < BORDER;
            bool bottom = pt.Y > ActualHeight - BORDER;

            if (top && left) result = 13;       // HTTOPLEFT
            else if (top && right) result = 14;  // HTTOPRIGHT
            else if (bottom && left) result = 16; // HTBOTTOMLEFT
            else if (bottom && right) result = 17;// HTBOTTOMRIGHT
            else if (left) result = 10;           // HTLEFT
            else if (right) result = 11;          // HTRIGHT
            else if (top) result = 12;            // HTTOP
            else if (bottom) result = 15;         // HTBOTTOM

            if (result != 1)
            {
                handled = true;
                return new IntPtr(result);
            }
        }
        return IntPtr.Zero;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        base.OnStateChanged(e);
    }

    // ===== 피커 오버레이 =====
    private Controls.TimePicker? _activeTimePicker;
    private Controls.DatePicker? _activeDatePicker;
    private int _pickerHour, _pickerMin;
    private int _dateViewYear, _dateViewMonth;
    private int _dateSelYear, _dateSelMonth, _dateSelDay;
    private static readonly string[] KoDays = ["일", "월", "화", "수", "목", "금", "토"];

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        HookAllPickers();
        WeeklyItems.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, HookAllPickers);
        OneTimeItems.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, HookAllPickers);

        // 다른 곳 클릭 시 피커 닫기
        PreviewMouseDown += OnGlobalMouseDown;
    }

    private void HookAllPickers()
    {
        HookPickers(WeeklyList);
        HookPickers(OneTimeList);
    }

    private void OnGlobalMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PickerOverlay.Visibility != Visibility.Visible) return;

        // 피커 패널 위를 클릭한 건지 확인
        var pos = e.GetPosition(this);
        var hit = InputHitTest(pos) as DependencyObject;

        // 피커 패널 내부 클릭이면 무시
        while (hit is not null)
        {
            if (hit == TimePickerPanel || hit == DatePickerPanel)
                return;
            hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
        }

        // 피커 밖 클릭 → 닫기
        ClosePicker();
        e.Handled = true;
    }

    private void HookPickers(System.Windows.Controls.ItemsControl list)
    {
        foreach (var item in list.Items)
        {
            var container = list.ItemContainerGenerator.ContainerFromItem(item);
            if (container is null) continue;
            foreach (var tp in FindVisualChildren<Controls.TimePicker>(container))
                tp.PickerRequested -= OnTimePickerRequested; // 중복 방지
            foreach (var tp in FindVisualChildren<Controls.TimePicker>(container))
                tp.PickerRequested += OnTimePickerRequested;
            foreach (var dp in FindVisualChildren<Controls.DatePicker>(container))
                dp.PickerRequested -= OnDatePickerRequested;
            foreach (var dp in FindVisualChildren<Controls.DatePicker>(container))
                dp.PickerRequested += OnDatePickerRequested;
        }
    }

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
        }
    }

    private void OnTimePickerRequested(Controls.TimePicker tp)
    {
        _activeTimePicker = tp;
        var parts = (tp.Time ?? "00:00").Split(':');
        _pickerHour = parts.Length >= 1 && int.TryParse(parts[0], out var h) ? Math.Clamp(h, 0, 23) : 0;
        _pickerMin = parts.Length >= 2 && int.TryParse(parts[1], out var m) ? Math.Clamp(m, 0, 59) : 0;
        UpdateTimePickerDisplay();
        TimePickerPanel.Visibility = Visibility.Visible;
        DatePickerPanel.Visibility = Visibility.Collapsed;
        PickerOverlay.Visibility = Visibility.Visible;
    }

    private void OnDatePickerRequested(Controls.DatePicker dp)
    {
        _activeDatePicker = dp;
        if (DateTime.TryParse(dp.Date, out var dt))
        {
            _dateSelYear = dt.Year; _dateSelMonth = dt.Month; _dateSelDay = dt.Day;
        }
        else
        {
            var now = DateTime.Now;
            _dateSelYear = now.Year; _dateSelMonth = now.Month; _dateSelDay = now.Day;
        }
        _dateViewYear = _dateSelYear; _dateViewMonth = _dateSelMonth;
        BuildDateCalendar();
        DatePickerPanel.Visibility = Visibility.Visible;
        TimePickerPanel.Visibility = Visibility.Collapsed;
        PickerOverlay.Visibility = Visibility.Visible;
    }

    private void OnOverlayClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ClosePicker();
        e.Handled = true;
    }

    private void ClosePicker()
    {
        PickerOverlay.Visibility = Visibility.Collapsed;
        TimePickerPanel.Visibility = Visibility.Collapsed;
        DatePickerPanel.Visibility = Visibility.Collapsed;
        _activeTimePicker = null;
        _activeDatePicker = null;
    }

    private void UpdateTimePickerDisplay()
    {
        TxtPickerHour.Text = $"{_pickerHour:D2}";
        TxtPickerMin.Text = $"{_pickerMin:D2}";
    }

    private void OnHourUp(object sender, RoutedEventArgs e) { _pickerHour = (_pickerHour + 1) % 24; UpdateTimePickerDisplay(); }
    private void OnHourDown(object sender, RoutedEventArgs e) { _pickerHour = (_pickerHour + 23) % 24; UpdateTimePickerDisplay(); }
    private void OnMinUp(object sender, RoutedEventArgs e) { _pickerMin = (_pickerMin + 1) % 60; UpdateTimePickerDisplay(); }
    private void OnMinDown(object sender, RoutedEventArgs e) { _pickerMin = (_pickerMin + 59) % 60; UpdateTimePickerDisplay(); }

    private void OnTimeOk(object sender, RoutedEventArgs e)
    {
        if (_activeTimePicker is not null)
            _activeTimePicker.Time = $"{_pickerHour:D2}:{_pickerMin:D2}";
        ClosePicker();
    }

    private void OnDatePrevMonth(object sender, RoutedEventArgs e)
    {
        _dateViewMonth--;
        if (_dateViewMonth < 1) { _dateViewMonth = 12; _dateViewYear--; }
        BuildDateCalendar();
    }

    private void OnDateNextMonth(object sender, RoutedEventArgs e)
    {
        _dateViewMonth++;
        if (_dateViewMonth > 12) { _dateViewMonth = 1; _dateViewYear++; }
        BuildDateCalendar();
    }

    private void BuildDateCalendar()
    {
        TxtDateYearMonth.Text = $"{_dateViewYear}.{_dateViewMonth:D2}";

        DateDayHeaders.Children.Clear();
        for (int i = 0; i < 7; i++)
        {
            DateDayHeaders.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = KoDays[i],
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("FgDimBrush"),
                FontFamily = new System.Windows.Media.FontFamily("pack://application:,,,/Assets/#NanumSquare Neo Regular"),
                Width = 48, TextAlignment = TextAlignment.Center
            });
        }

        DateDayGrid.Children.Clear();
        var firstDay = new DateTime(_dateViewYear, _dateViewMonth, 1);
        var startDow = (int)firstDay.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(_dateViewYear, _dateViewMonth);

        for (int i = 0; i < 42; i++)
        {
            var dayNum = i - startDow + 1;
            if (dayNum >= 1 && dayNum <= daysInMonth)
            {
                var isSelected = _dateViewYear == _dateSelYear && _dateViewMonth == _dateSelMonth && dayNum == _dateSelDay;
                var isToday = _dateViewYear == DateTime.Now.Year && _dateViewMonth == DateTime.Now.Month && dayNum == DateTime.Now.Day;

                var btn = new System.Windows.Controls.Button
                {
                    Content = dayNum.ToString(),
                    Tag = dayNum,
                    FontSize = 15,
                    FontFamily = new System.Windows.Media.FontFamily("pack://application:,,,/Assets/#NanumSquare Neo Regular"),
                    Width = 48, Height = 38,
                    Margin = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = isSelected ? (System.Windows.Media.SolidColorBrush)FindResource("AccentDimBrush")
                               : System.Windows.Media.Brushes.Transparent,
                    Foreground = isSelected ? (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush")
                               : isToday ? (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush")
                               : (System.Windows.Media.SolidColorBrush)FindResource("FgBrush"),
                    BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                btn.Click += OnDateDayClick;
                DateDayGrid.Children.Add(btn);
            }
            else
            {
                DateDayGrid.Children.Add(new System.Windows.Controls.Border { Width = 48, Height = 38 });
            }
        }
    }

    private void OnDateDayClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is int day && _activeDatePicker is not null)
        {
            _activeDatePicker.Date = $"{_dateViewYear:D4}-{_dateViewMonth:D2}-{day:D2}";
            ClosePicker();
        }
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PickerOverlay.Visibility == Visibility.Visible)
            return;
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnHelp(object sender, RoutedEventArgs e)
    {
        var dlg = new Controls.TutorialDialog();
        dlg.Owner = this;
        dlg.SetLanguage(_lang);
        dlg.ShowDialog();
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        if (IsDirty)
        {
            var msg = _lang == "ko"
                ? "변경 사항이 저장되지 않았습니다."
                : "You have unsaved changes.";

            var result = Controls.CustomDialog.ShowYesNoCancel(msg, "AutoTimer", this);

            if (result == "yes")
            {
                try
                {
                    SaveToSettings();
                    _timeSync.RestartTimer();
                }
                catch { }
            }
            else if (result == "cancel" || result == null)
            {
                return;
            }
        }
        Close();
    }

    private void RestoreWindowSize()
    {
        Width = 540;
        Height = 680;
    }

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop();
        _cachedScreens = null;
        base.OnClosed(e);
    }
}

public sealed class WeeklyScheduleVM
{
    public string Id { get; set; }
    public bool Enabled { get; set; }
    public string DayOfWeekDisplay { get; set; } // 표시용 (한글/영어)
    public string DayKey { get; set; } // 내부 키 (Monday, Tuesday, ...)
    public string Time { get; set; }
    public string Label { get; set; }
    public string? VideoPath { get; set; }

    public WeeklyScheduleVM(WeeklySchedule model)
    {
        Id = model.Id;
        Enabled = model.Enabled;
        DayKey = model.DayOfWeek.ToString();
        // 표시는 현재 언어 기준
        var idx = SettingsWindow.DayKeys.IndexOf(DayKey);
        var lang = SettingsManager.Current.General.Language;
        var opts = lang == "ko" ? SettingsWindow.DayOptionsKo : SettingsWindow.DayOptionsEn;
        DayOfWeekDisplay = idx >= 0 ? opts[idx] : DayKey;
        Time = model.Time;
        Label = model.Label;
        VideoPath = model.VideoPath;
    }

    public WeeklySchedule ToModel()
    {
        // DayOfWeekDisplay에서 DayKey로 역변환
        var idxKo = SettingsWindow.DayOptionsKo.IndexOf(DayOfWeekDisplay);
        var idxEn = SettingsWindow.DayOptionsEn.IndexOf(DayOfWeekDisplay);
        var idx = idxKo >= 0 ? idxKo : idxEn;
        var key = idx >= 0 ? SettingsWindow.DayKeys[idx] : DayKey;

        return new()
        {
            Id = Id,
            Enabled = Enabled,
            DayOfWeek = Enum.TryParse<DayOfWeek>(key, out var d) ? d : DayOfWeek.Sunday,
            Time = Time,
            Label = Label,
            VideoPath = VideoPath
        };
    }
}

public sealed class OneTimeScheduleVM
{
    public string Id { get; set; }
    public string Date { get; set; }
    public string Time { get; set; }
    public string Label { get; set; }
    public string? VideoPath { get; set; }
    public bool AutoDelete { get; set; }

    public OneTimeScheduleVM(OneTimeSchedule model)
    {
        Id = model.Id;
        Date = model.Date;
        Time = model.Time;
        Label = model.Label;
        VideoPath = model.VideoPath;
        AutoDelete = model.AutoDelete;
    }

    public OneTimeSchedule ToModel() => new()
    {
        Id = Id,
        Date = Date,
        Time = Time,
        Label = Label,
        VideoPath = VideoPath,
        AutoDelete = AutoDelete
    };
}

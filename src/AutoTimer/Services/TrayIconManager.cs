using System;
using System.Windows;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace AutoTimer.Services;

public sealed class TrayIconManager : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private readonly TimeSyncService _timeSync;
    private SettingsWindow? _settingsWindow;

    private Action? _settingsTestPlayHandler;

    public event Action? TestPlayRequested;

    /// <summary>설정창이 열려 있고 미저장 변경이 있는지</summary>
    public bool HasUnsavedSettings => _settingsWindow is not null && _settingsWindow.IsVisible && _settingsWindow.IsDirty;

    public TrayIconManager(TimeSyncService timeSync)
    {
        _timeSync = timeSync;
    }

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "AutoTimer",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.ico")),
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.ForceCreate();
        _trayIcon.TrayMouseDoubleClick += OnTrayDoubleClick;
        _trayIcon.TrayLeftMouseDoubleClick += OnTrayDoubleClick;
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "설정 (_S)" };
        settingsItem.Click += OnSettingsClick;

        var testPlayItem = new System.Windows.Controls.MenuItem { Header = "테스트 재생 (_T)" };
        testPlayItem.Click += OnTestPlayClick;

        var syncItem = new System.Windows.Controls.MenuItem { Header = "서버 동기화 (_R)" };
        syncItem.Click += OnSyncClick;

        var separator = new System.Windows.Controls.Separator();

        var exitItem = new System.Windows.Controls.MenuItem { Header = "종료 (_X)" };
        exitItem.Click += OnExitClick;

        menu.Items.Add(settingsItem);
        menu.Items.Add(testPlayItem);
        menu.Items.Add(syncItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnTrayDoubleClick(object? sender, RoutedEventArgs e)
    {
        OnSettingsClick(sender, e);
    }

    public void OpenSettings() => OnSettingsClick(null, null!);

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow is not null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            _settingsWindow = new SettingsWindow(_timeSync);
            _settingsTestPlayHandler = () => TestPlayRequested?.Invoke();
            _settingsWindow.TestPlayRequested += _settingsTestPlayHandler;
            _settingsWindow.Closed += (_, _) =>
            {
                if (_settingsWindow is not null && _settingsTestPlayHandler is not null)
                    _settingsWindow.TestPlayRequested -= _settingsTestPlayHandler;
                _settingsTestPlayHandler = null;
                _settingsWindow = null;
            };
            _settingsWindow.Show();
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoTimer", "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SETTINGS: {ex}\n\n");
            _settingsWindow = null;
        }
    }

    private void OnTestPlayClick(object? sender, RoutedEventArgs e)
    {
        TestPlayRequested?.Invoke();
    }

    private async void OnSyncClick(object? sender, RoutedEventArgs e)
    {
        if (_timeSync.IsSyncing) return;

        await _timeSync.SyncOnceAsync();
        var lang = SettingsManager.Current.General.Language;
        var result = _timeSync.LastResult;

        var msg = result switch
        {
            SyncResult.Success => lang == "ko"
                ? $"NTP 동기화 성공\n서버: {_timeSync.LastServer}\nRTT: {_timeSync.LastRtt.TotalMilliseconds:F1}ms"
                : $"NTP sync success\nServer: {_timeSync.LastServer}\nRTT: {_timeSync.LastRtt.TotalMilliseconds:F1}ms",
            SyncResult.LocalMode => lang == "ko"
                ? "로컬 모드에서는 동기화가 필요하지 않습니다."
                : "Sync not needed in local mode.",
            SyncResult.NetworkError => lang == "ko"
                ? "NTP 서버에 연결할 수 없습니다.\n인터넷 연결을 확인해주세요."
                : "Cannot reach NTP server.\nCheck your internet connection.",
            _ => ""
        };

        if (!string.IsNullOrEmpty(msg))
        {
            if (result == SyncResult.Success)
                Controls.CustomDialog.ShowInfo(msg);
            else
                Controls.CustomDialog.ShowWarning(msg);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}

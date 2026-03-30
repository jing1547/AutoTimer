using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTimer.Services;

public enum SyncResult { Success, LocalMode, NetworkError, AlreadySyncing }

public sealed class TimeSyncService : IDisposable
{
    private Timer? _syncTimer;
    private readonly object _lock = new();
    private TimeSpan _offset = TimeSpan.Zero;
    private bool _synced;
    private int _syncing;
    private SyncResult _lastResult = SyncResult.Success;
    private string _lastServer = "";
    private TimeSpan _lastRtt = TimeSpan.Zero;

    public TimeSpan Offset
    {
        get { lock (_lock) return _offset; }
    }

    public bool IsSynced
    {
        get { lock (_lock) return _synced; }
    }

    public bool IsSyncing => Interlocked.CompareExchange(ref _syncing, 0, 0) == 1;

    public SyncResult LastResult => _lastResult;
    public string LastServer => _lastServer;
    public TimeSpan LastRtt => _lastRtt;

    private static readonly TimeZoneInfo Kst = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

    /// <summary>현재 정밀 시각 (서버 모드면 NTP UTC → KST 고정, 로컬이면 그대로)</summary>
    public DateTime Now
    {
        get
        {
            if (SettingsManager.Current.General.TimeSource == "local")
                return DateTime.Now;
            lock (_lock)
            {
                var utcNow = DateTime.UtcNow + _offset;
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, Kst);
            }
        }
    }

    /// <summary>UI에서 직접 호출 — 라디오 버튼 상태에 따라 시간 반환</summary>
    public DateTime GetNow(bool useServer)
    {
        if (!useServer)
            return DateTime.Now;
        lock (_lock)
        {
            var utcNow = DateTime.UtcNow + _offset;
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, Kst);
        }
    }

    public event Action<SyncResult>? SyncCompleted;

    public async Task StartAsync()
    {
        try { await SyncOnceAsync(); }
        catch { }
        RestartTimer();
    }

    public void RestartTimer()
    {
        _syncTimer?.Dispose();
        var intervalMs = 60 * 1000; // 1분 고정
        _syncTimer = new Timer(_ => _ = SyncOnceAsync(), null, intervalMs, intervalMs);
    }

    public async Task SyncOnceAsync()
    {
        if (Interlocked.Exchange(ref _syncing, 1) == 1)
        {
            _lastResult = SyncResult.AlreadySyncing;
            return;
        }

        try
        {
            if (SettingsManager.Current.General.TimeSource == "local")
            {
                lock (_lock)
                {
                    _offset = TimeSpan.Zero;
                    _synced = false;
                }
                _lastResult = SyncResult.LocalMode;
                _lastServer = "";
                _lastRtt = TimeSpan.Zero;
                SyncCompleted?.Invoke(SyncResult.LocalMode);
                return;
            }

            var result = await NtpClient.GetTimeAsync().ConfigureAwait(false);

            if (!result.Success)
            {
                _lastResult = SyncResult.NetworkError;
                SyncCompleted?.Invoke(SyncResult.NetworkError);
                return;
            }

            lock (_lock)
            {
                _offset = result.Offset;
                _synced = true;
            }

            _lastServer = result.Server;
            _lastRtt = result.RoundTripTime;
            _lastResult = SyncResult.Success;
            SyncCompleted?.Invoke(SyncResult.Success);
        }
        catch
        {
            _lastResult = SyncResult.NetworkError;
            SyncCompleted?.Invoke(SyncResult.NetworkError);
        }
        finally
        {
            Interlocked.Exchange(ref _syncing, 0);
        }
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _syncTimer = null;
    }
}

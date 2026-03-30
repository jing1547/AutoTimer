using System.Threading;

namespace AutoTimer.Services;

public static class SingleInstance
{
    private const string MutexName = "Global\\AutoTimer_SingleInstance_Mutex";
    private static Mutex? _mutex;

    public static bool Acquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        return true;
    }

    public static void Release()
    {
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }
}

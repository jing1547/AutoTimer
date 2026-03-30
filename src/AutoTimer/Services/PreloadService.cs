using LibVLCSharp.Shared;

namespace AutoTimer.Services;

/// <summary>
/// LibVLC 인스턴스 관리. 앱 시작 시 초기화하여 첫 재생 지연을 방지한다.
/// </summary>
public static class PreloadService
{
    private static LibVLC? _libvlc;

    public static LibVLC GetLibVLC()
    {
        _libvlc ??= new LibVLC("--quiet");
        return _libvlc;
    }

    public static void Shutdown()
    {
        _libvlc?.Dispose();
        _libvlc = null;
    }
}

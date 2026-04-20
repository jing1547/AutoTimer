using System;
using System.IO;
using System.Threading.Tasks;
using Windows.System;

namespace AutoTimer.Services;

/// <summary>
/// UWP 앱을 프로그래밍적으로 서스펜드(= 데스크톱에서 최소화 효과)시킨다.
/// Windows 11에서 UWP 전체화면 창이 Win+D/SendInput을 무시하는 케이스를 우회하기 위한 공식 경로.
///
/// 참고: AppDiagnosticInfo.RequestInfoForPackageAsync + AppResourceGroupInfo.StartSuspendAsync.
/// 최초 호출 시 사용자에게 "앱 진단 정보 접근 권한" 프롬프트가 뜰 수 있음.
/// </summary>
internal static class UwpSuspender
{
    // JW Library 패키지 식별자. Get-AppxPackage로 확인된 값.
    public const string JwLibraryPackageFamilyName = "WatchtowerBibleandTractSo.45909CDBADF3C_5rz59y55nfz3e";

    /// <summary>
    /// JW Library를 서스펜드 시도한다. 비동기이지만 대기하지 않아도 화면 효과는 즉시.
    /// 실패는 조용히 삼킨다(권한 거부/미설치 등). 진단 로그만 남김.
    /// </summary>
    public static async Task SuspendJwLibraryAsync()
    {
        try
        {
            // 진단 권한 요청 (이미 허용된 상태면 즉시 반환)
            var access = await AppDiagnosticInfo.RequestAccessAsync();
            Log($"RequestAccessAsync -> {access}");
            if (access != Windows.System.DiagnosticAccessStatus.Allowed)
                return;

            var infos = await AppDiagnosticInfo.RequestInfoForPackageAsync(JwLibraryPackageFamilyName);
            Log($"RequestInfoForPackageAsync count={infos.Count}");
            if (infos.Count == 0) return; // JW가 실행 중이 아님

            foreach (var info in infos)
            {
                var groups = info.GetResourceGroups();
                foreach (var g in groups)
                {
                    try
                    {
                        var result = await g.StartSuspendAsync();
                        Log($"StartSuspendAsync -> {result}");
                    }
                    catch (Exception ex)
                    {
                        Log($"StartSuspendAsync threw: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"SuspendJwLibraryAsync threw: {ex.Message}");
        }
    }

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoTimer");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "uwpsuspend.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }
}

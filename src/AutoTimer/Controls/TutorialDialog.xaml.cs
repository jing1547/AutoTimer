using System.Windows;

namespace AutoTimer.Controls;

public partial class TutorialDialog : Window
{
    public TutorialDialog()
    {
        InitializeComponent();
    }

    public void SetLanguage(string lang)
    {
        if (lang == "ko")
        {
            TxtTitle.Text = "AutoTimer 사용법";
            BtnClose.Content = "확인";
            TxtContent.Text =
                "[ 기본 설정 ]\n" +
                "\n" +
                "1. 영상 선택 : 재생할 동영상 파일을 선택합니다.\n" +
                "2. 모니터 : 영상을 표시할 모니터를 선택합니다.\n" +
                "   설정된 모니터가 연결되지 않으면 재생되지 않습니다.\n" +
                "3. 시간 소스 : 서버(NTP) 또는 로컬 시간 중 선택합니다.\n" +
                "   서버 모드는 NTP 서버와 동기화된 정확한 시간을 사용합니다.\n" +
                "\n" +
                "[ 스케줄 ]\n" +
                "\n" +
                "1. 주간 스케줄 : 매주 반복되는 스케줄을 설정합니다.\n" +
                "   요일, 시간, 라벨을 지정하고 토글로 ON/OFF 합니다.\n" +
                "2. 일회성 타이머 : 특정 날짜에 한 번만 실행됩니다.\n" +
                "   실행 후 자동 삭제됩니다.\n" +
                "\n" +
                "[ 하단 버튼 ]\n" +
                "\n" +
                "1. 테스트 재생 : 설정된 영상을 즉시 재생합니다.\n" +
                "   스케줄과 무관하게 동작을 확인할 수 있습니다.\n" +
                "2. 새로고침 : 현재 시간에 재생 중이어야 할 스케줄을\n" +
                "   찾아서 경과 시간에 맞춰 영상을 동기화합니다.\n" +
                "   예) 50분 스케줄인데 지금 51분이면\n" +
                "       영상의 1분 지점부터 재생됩니다.\n" +
                "   해당 시간에 스케줄이 없으면 아무 반응 없습니다.\n" +
                "3. 영상 종료 : 현재 재생 중인 영상을 강제 종료합니다.\n" +
                "4. 서버 동기화 : NTP 서버와 시간을 즉시 동기화합니다.\n" +
                "5. 저장 : 변경된 설정을 저장합니다.\n" +
                "\n" +
                "[ 트레이 아이콘 ]\n" +
                "\n" +
                "창을 닫으면 트레이로 최소화됩니다.\n" +
                "트레이 아이콘을 더블클릭하면 설정창이 열립니다.\n" +
                "우클릭 메뉴에서 새로고침, 영상 강제 종료,\n" +
                "서버 동기화 등을 사용할 수 있습니다.\n" +
                "\n" +
                "[ 팁 ]\n" +
                "\n" +
                "- 설정 변경 후 반드시 저장 버튼을 눌러주세요.\n" +
                "- 미저장 상태에서는 스케줄이 트리거되지 않습니다.\n" +
                "- 시작 프로그램 등록 시 Windows 부팅과 함께\n" +
                "  자동으로 실행됩니다.";
        }
        else
        {
            TxtTitle.Text = "AutoTimer Guide";
            BtnClose.Content = "OK";
            TxtContent.Text =
                "[ Basic Settings ]\n" +
                "\n" +
                "1. Select video : Choose the video file to play.\n" +
                "2. Monitor : Select which monitor to display on.\n" +
                "   Playback won't start if the monitor is disconnected.\n" +
                "3. Time source : Choose Server (NTP) or Local time.\n" +
                "   Server mode uses NTP-synced accurate time.\n" +
                "\n" +
                "[ Schedules ]\n" +
                "\n" +
                "1. Weekly : Set recurring weekly schedules.\n" +
                "   Specify day, time, label and toggle ON/OFF.\n" +
                "2. One-time : Runs once on a specific date.\n" +
                "   Automatically deleted after execution.\n" +
                "\n" +
                "[ Bottom Buttons ]\n" +
                "\n" +
                "1. Test play : Play the video immediately.\n" +
                "   Verify behavior regardless of schedules.\n" +
                "2. Refresh : Finds the schedule that should be\n" +
                "   playing now and syncs the video to elapsed time.\n" +
                "   e.g.) Schedule at :50, current time :51\n" +
                "         Video starts at the 1-minute mark.\n" +
                "   No response if no schedule matches.\n" +
                "3. Stop video : Force-close the current video.\n" +
                "4. Sync now : Sync time with NTP server.\n" +
                "5. Save : Save all changed settings.\n" +
                "\n" +
                "[ Tray Icon ]\n" +
                "\n" +
                "Closing the window minimizes to system tray.\n" +
                "Double-click the tray icon to open settings.\n" +
                "Right-click menu provides Refresh, Force stop,\n" +
                "Sync, and other quick actions.\n" +
                "\n" +
                "[ Tips ]\n" +
                "\n" +
                "- Always click Save after changing settings.\n" +
                "- Schedules won't trigger with unsaved changes.\n" +
                "- Enable 'Start with Windows' for automatic\n" +
                "  launch on boot.";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

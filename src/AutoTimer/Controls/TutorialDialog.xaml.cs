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
                "[ 타이틀바 ]\n" +
                "\n" +
                "언어 / 테마 : 변경 시 즉시 적용됩니다.\n" +
                "  저장 버튼을 누를 필요가 없습니다.\n" +
                "\n" +
                "[ 일반 ]\n" +
                "\n" +
                "1. 시작 프로그램 등록 : Windows 부팅 시\n" +
                "   자동으로 실행됩니다.\n" +
                "2. 시간 소스 : 서버(NTP) 또는 로컬 시간 중\n" +
                "   선택합니다. 서버 모드는 NTP 서버와\n" +
                "   동기화된 정확한 시간을 사용합니다.\n" +
                "\n" +
                "[ 화면 ]\n" +
                "\n" +
                "모니터 : 영상을 표시할 모니터를 선택합니다.\n" +
                "  설정된 모니터가 연결되지 않으면\n" +
                "  재생되지 않습니다.\n" +
                "\n" +
                "[ 재생 ]\n" +
                "\n" +
                "영상 선택 : 재생할 동영상 파일을 선택합니다.\n" +
                "\n" +
                "[ 주간 스케줄 ]\n" +
                "\n" +
                "매주 반복되는 스케줄을 설정합니다.\n" +
                "요일, 시간, 라벨을 지정하고\n" +
                "토글로 ON/OFF 합니다.\n" +
                "\n" +
                "[ 일회성 타이머 ]\n" +
                "\n" +
                "특정 날짜에 한 번만 실행됩니다.\n" +
                "실행 후 자동 삭제됩니다.\n" +
                "\n" +
                "[ 하단 버튼 ]\n" +
                "\n" +
                "테스트 재생 : 설정된 영상을 즉시 재생합니다.\n" +
                "새로고침 : 현재 시간에 재생 중이어야 할\n" +
                "  스케줄을 찾아서 경과 시간에 맞춰\n" +
                "  영상을 동기화합니다.\n" +
                "  예) 50분 스케줄, 현재 51분이면\n" +
                "      영상의 1분 지점부터 재생됩니다.\n" +
                "  해당 시간에 스케줄이 없으면 아무 반응 없음.\n" +
                "영상 종료 : 재생 중인 영상을 강제 종료합니다.\n" +
                "서버 동기화 : NTP 서버와 시간을 동기화합니다.\n" +
                "저장 : 변경된 설정을 저장합니다.\n" +
                "\n" +
                "[ 트레이 아이콘 ]\n" +
                "\n" +
                "창을 닫으면 트레이로 최소화됩니다.\n" +
                "더블클릭하면 설정창이 열립니다.\n" +
                "우클릭 메뉴에서 새로고침, 영상 강제 종료,\n" +
                "서버 동기화 등을 사용할 수 있습니다.\n" +
                "\n" +
                "[ 팁 ]\n" +
                "\n" +
                "- 설정 변경 후 반드시 저장 버튼을 눌러주세요.\n" +
                "- 미저장 상태에서는 스케줄이 트리거되지 않습니다.";
        }
        else
        {
            TxtTitle.Text = "AutoTimer Guide";
            BtnClose.Content = "OK";
            TxtContent.Text =
                "[ Title Bar ]\n" +
                "\n" +
                "Language / Theme : Applied immediately.\n" +
                "  No need to click Save.\n" +
                "\n" +
                "[ General ]\n" +
                "\n" +
                "1. Start with Windows : Launches automatically\n" +
                "   on boot.\n" +
                "2. Time source : Choose Server (NTP) or Local.\n" +
                "   Server mode uses NTP-synced accurate time.\n" +
                "\n" +
                "[ Display ]\n" +
                "\n" +
                "Monitor : Select which monitor to display on.\n" +
                "  Playback won't start if the monitor\n" +
                "  is disconnected.\n" +
                "\n" +
                "[ Playback ]\n" +
                "\n" +
                "Select video : Choose the video file to play.\n" +
                "\n" +
                "[ Weekly Schedules ]\n" +
                "\n" +
                "Set recurring weekly schedules.\n" +
                "Specify day, time, label and toggle ON/OFF.\n" +
                "\n" +
                "[ One-time Timers ]\n" +
                "\n" +
                "Runs once on a specific date.\n" +
                "Automatically deleted after execution.\n" +
                "\n" +
                "[ Bottom Buttons ]\n" +
                "\n" +
                "Test play : Play the video immediately.\n" +
                "Refresh : Finds the schedule that should be\n" +
                "  playing now and syncs to elapsed time.\n" +
                "  e.g.) Schedule at :50, now :51\n" +
                "        Video starts at the 1-min mark.\n" +
                "  No response if no schedule matches.\n" +
                "Stop video : Force-close the current video.\n" +
                "Sync now : Sync time with NTP server.\n" +
                "Save : Save all changed settings.\n" +
                "\n" +
                "[ Tray Icon ]\n" +
                "\n" +
                "Closing the window minimizes to tray.\n" +
                "Double-click to open settings.\n" +
                "Right-click for Refresh, Force stop,\n" +
                "Sync, and other quick actions.\n" +
                "\n" +
                "[ Tips ]\n" +
                "\n" +
                "- Always click Save after changing settings.\n" +
                "- Schedules won't trigger with unsaved changes.";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

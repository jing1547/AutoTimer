using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoTimer.Controls;

public partial class CustomDialog : Window
{
    public string? Result { get; private set; }

    private CustomDialog()
    {
        InitializeComponent();
    }

    private void AddButton(string text, string result, bool isAccent = false)
    {
        var btn = new Button
        {
            Content = text,
            Tag = result,
            MinWidth = 80,
            Padding = new Thickness(16, 7, 16, 7),
            Margin = new Thickness(4, 0, 4, 0),
            Cursor = Cursors.Hand,
            FontSize = 14,
            FontFamily = new FontFamily("pack://application:,,,/Assets/#NanumSquare Neo Regular"),
            Foreground = isAccent
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("FgBrush"),
            Background = (Brush)FindResource("BgHoverBrush"),
            BorderBrush = isAccent
                ? (Brush)FindResource("AccentDimBrush")
                : (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1)
        };
        btn.Click += (_, _) => { Result = result; Close(); };
        ButtonPanel.Children.Add(btn);
    }

    /// <summary>OK 다이얼로그</summary>
    public static void ShowInfo(string message, string title = "AutoTimer", Window? owner = null)
    {
        var lang = Services.SettingsManager.Current.General.Language;
        var dlg = new CustomDialog();
        dlg.TxtTitle.Text = title;
        dlg.TxtMessage.Text = message;
        dlg.AddButton(lang == "ko" ? "확인" : "OK", "ok", true);
        if (owner is not null) dlg.Owner = owner;
        dlg.ShowDialog();
    }

    /// <summary>경고 다이얼로그</summary>
    public static void ShowWarning(string message, string title = "AutoTimer", Window? owner = null)
    {
        var lang = Services.SettingsManager.Current.General.Language;
        var dlg = new CustomDialog();
        dlg.TxtTitle.Text = "⚠ " + title;
        dlg.TxtMessage.Text = message;
        dlg.AddButton(lang == "ko" ? "확인" : "OK", "ok", true);
        if (owner is not null) dlg.Owner = owner;
        dlg.ShowDialog();
    }

    /// <summary>예/아니오/취소 다이얼로그. 반환: "yes", "no", "cancel" 또는 null</summary>
    public static string? ShowYesNoCancel(string message, string title = "AutoTimer", Window? owner = null)
    {
        var lang = Services.SettingsManager.Current.General.Language;
        var dlg = new CustomDialog();
        dlg.TxtTitle.Text = title;
        dlg.TxtMessage.Text = message;
        dlg.AddButton(lang == "ko" ? "저장" : "Save", "yes", true);
        dlg.AddButton(lang == "ko" ? "저장 안 함" : "Don't save", "no");
        dlg.AddButton(lang == "ko" ? "취소" : "Cancel", "cancel");
        if (owner is not null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.Result;
    }

    /// <summary>예/아니오 다이얼로그</summary>
    public static bool ShowYesNo(string message, string title = "AutoTimer", Window? owner = null)
    {
        var lang = Services.SettingsManager.Current.General.Language;
        var dlg = new CustomDialog();
        dlg.TxtTitle.Text = title;
        dlg.TxtMessage.Text = message;
        dlg.AddButton(lang == "ko" ? "예" : "Yes", "yes", true);
        dlg.AddButton(lang == "ko" ? "아니오" : "No", "no");
        if (owner is not null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.Result == "yes";
    }
}

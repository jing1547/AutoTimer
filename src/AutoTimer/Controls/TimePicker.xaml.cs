using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoTimer.Controls;

public partial class TimePicker : UserControl
{
    public static readonly DependencyProperty TimeProperty =
        DependencyProperty.Register("Time", typeof(string), typeof(TimePicker),
            new FrameworkPropertyMetadata("00:00:00", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

    public string Time
    {
        get => (string)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>클릭 시 발생 — SettingsWindow에서 공용 피커를 열기 위해</summary>
    public event Action<TimePicker>? PickerRequested;

    public TimePicker()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimePicker tp) tp.UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        // 저장된 값이 "HH:mm"이면 "HH:mm:00"으로 정규화해서 표시
        var v = Time;
        if (string.IsNullOrWhiteSpace(v)) { TxtDisplay.Text = "00:00:00"; return; }
        var parts = v.Split(':');
        if (parts.Length == 2) v = v + ":00";
        TxtDisplay.Text = v;
    }

    private void OnDisplayClick(object sender, MouseButtonEventArgs e)
    {
        PickerRequested?.Invoke(this);
    }
}

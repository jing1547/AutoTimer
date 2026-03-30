using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoTimer.Controls;

public partial class DatePicker : UserControl
{
    public static readonly DependencyProperty DateProperty =
        DependencyProperty.Register("Date", typeof(string), typeof(DatePicker),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));

    public string Date
    {
        get => (string)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public event Action<DatePicker>? PickerRequested;

    public DatePicker()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DatePicker dp) dp.UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var date = Date;
        if (string.IsNullOrWhiteSpace(date))
        {
            var now = DateTime.Now;
            TxtDisplay.Text = $"{now:yyyy-MM-dd}";
        }
        else
        {
            TxtDisplay.Text = date;
        }
    }

    private void OnDisplayClick(object sender, MouseButtonEventArgs e)
    {
        PickerRequested?.Invoke(this);
    }
}

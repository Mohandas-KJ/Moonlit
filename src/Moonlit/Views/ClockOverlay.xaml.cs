using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Moonlit.Views;

/// <summary>
/// UserControl rendering a dynamic digital clock and date overlay over the video background.
/// </summary>
public partial class ClockOverlay : UserControl
{
    private readonly DispatcherTimer _clockTimer;
    private bool _use24HourFormat;
    private bool _showSeconds;
    private string _clockAlignment = "Center";

    public ClockOverlay()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _clockTimer.Tick += (_, _) => UpdateTimeDisplay();

        Loaded += (_, _) =>
        {
            UpdateTimeDisplay();
            _clockTimer.Start();
        };

        Unloaded += (_, _) => _clockTimer.Stop();
    }

    public bool Use24HourFormat
    {
        get => _use24HourFormat;
        set
        {
            _use24HourFormat = value;
            UpdateTimeDisplay();
        }
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set
        {
            _showSeconds = value;
            ClockSecondsText.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            UpdateTimeDisplay();
        }
    }

    public string ClockAlignment
    {
        get => _clockAlignment;
        set
        {
            _clockAlignment = value;
            ApplyAlignment(value);
        }
    }

    public double DimmingOpacity
    {
        get => DimmingLayer.Opacity;
        set => DimmingLayer.Opacity = Math.Clamp(value, 0.0, 0.85);
    }

    public bool ShowDismissHint
    {
        get => DismissHintBorder.Visibility == Visibility.Visible;
        set => DismissHintBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Smoothly animates the opacity of the bottom dismiss / sign-in hint label.
    /// </summary>
    public void AnimateHintOpacity(double targetOpacity, TimeSpan duration)
    {
        var anim = new System.Windows.Media.Animation.DoubleAnimation(targetOpacity, duration);
        DismissHintBorder.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    public void ApplyAlignment(string alignment)
    {
        switch (alignment)
        {
            case "BottomLeft":
                ClockContainer.HorizontalAlignment = HorizontalAlignment.Left;
                ClockContainer.VerticalAlignment = VerticalAlignment.Bottom;
                ClockStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                break;
            case "TopLeft":
                ClockContainer.HorizontalAlignment = HorizontalAlignment.Left;
                ClockContainer.VerticalAlignment = VerticalAlignment.Top;
                ClockStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                break;
            case "BottomRight":
                ClockContainer.HorizontalAlignment = HorizontalAlignment.Right;
                ClockContainer.VerticalAlignment = VerticalAlignment.Bottom;
                ClockStackPanel.HorizontalAlignment = HorizontalAlignment.Right;
                break;
            case "Center":
            default:
                ClockContainer.HorizontalAlignment = HorizontalAlignment.Center;
                ClockContainer.VerticalAlignment = VerticalAlignment.Center;
                ClockStackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                break;
        }
    }

    public void UpdateTimeDisplay()
    {
        var now = DateTime.Now;

        if (_use24HourFormat)
        {
            ClockTimeText.Text = now.ToString("HH:mm");
            ClockAmPmText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ClockTimeText.Text = now.ToString("h:mm");
            ClockAmPmText.Text = now.ToString("tt");
            ClockAmPmText.Visibility = Visibility.Visible;
        }

        if (_showSeconds)
        {
            ClockSecondsText.Text = now.ToString(":ss");
            ClockSecondsText.Visibility = Visibility.Visible;
        }
        else
        {
            ClockSecondsText.Visibility = Visibility.Collapsed;
        }

        ClockDateText.Text = now.ToString("dddd, MMMM d");
    }
}

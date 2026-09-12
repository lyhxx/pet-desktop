using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DesktopPet.UI;

/// <summary>
/// 应用内通知（右下角轻提示），替代系统托盘气泡。
/// 多条通知自下而上堆叠，几秒后自动淡出；带链接时点击可打开。
/// </summary>
public partial class ToastWindow : Window
{
    private const double EdgeMargin = 12;
    private const double Gap = 10;
    private const double VisibleSeconds = 6.0;

    private static readonly List<ToastWindow> Active = new();

    private readonly DispatcherTimer _timer;
    private readonly string? _url;
    private bool _closing;

    private ToastWindow(string title, string message, string? url, string icon)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        IconText.Text = icon;
        _url = url;

        MouseLeftButtonDown += (_, _) => Dismiss(openUrl: true);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(VisibleSeconds) };
        _timer.Tick += (_, _) => Dismiss(openUrl: false);

        Loaded += (_, _) =>
        {
            UpdateLayout();
            RepositionAll();
            FadeIn();
        };
    }

    /// <summary>显示一条通知。url 非空时点击通知会打开该地址。</summary>
    public static void Show(string title, string message, string? url = null, string icon = "🐧")
    {
        var toast = new ToastWindow(title, message, url, icon);
        Active.Insert(0, toast);
        toast.Closed += (_, _) =>
        {
            Active.Remove(toast);
            RepositionAll();
        };
        toast.Show();
    }

    private void FadeIn()
    {
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        _timer.Start();
    }

    private void Dismiss(bool openUrl)
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();

        if (openUrl && !string.IsNullOrEmpty(_url))
            OpenUrl(_url);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private static void RepositionAll()
    {
        Rect work = SystemParameters.WorkArea;
        double bottom = work.Bottom;

        foreach (ToastWindow toast in Active)
        {
            if (!toast.IsLoaded) continue;

            double height = toast.ActualHeight > 0 ? toast.ActualHeight : toast.Height;
            toast.Left = work.Right - toast.Width;
            toast.Top = bottom - height;
            bottom -= height + Gap;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打开失败忽略。
        }
    }
}

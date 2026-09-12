using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.UI;

/// <summary>悬停时显示在宠物头顶的迷你状态条：图标 + 百分比，鼠标穿透。</summary>
public partial class StatusWindow : Window
{
    private readonly PetState _state;
    private readonly DispatcherTimer _timer;

    public StatusWindow(PetState state)
    {
        InitializeComponent();
        _state = state;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) => Refresh();

        SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
                source.AddHook(WndProc);
        };
        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    public void Refresh()
    {
        // 显示"饱食度" = 100 - 饥饿，这样喂食是往上涨、100% 表示吃饱，更直观。
        HungerText.Text = $"{(int)Math.Round(100 - _state.Hunger)}%";
        MoodText.Text = $"{(int)Math.Round(_state.Mood)}%";
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;

        if (msg == WM_NCHITTEST)
        {
            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }

        return IntPtr.Zero;
    }
}

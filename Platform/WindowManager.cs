using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet.Platform;

/// <summary>
/// 跨显示器虚拟桌面与工作区（去掉任务栏）的边界计算。
/// Win32 返回的是物理像素，这里换算为 WPF 的 DIP，
/// 以保证多显示器 + 高 DPI 下坐标一致。
/// </summary>
public static class WindowManager
{
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO info);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    /// <summary>整个虚拟桌面（所有显示器合并）的 DIP 边界。</summary>
    public static Rect GetVirtualScreenDips(Visual visual)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(visual);
        double scaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;

        double left = GetSystemMetrics(SM_XVIRTUALSCREEN) / scaleX;
        double top = GetSystemMetrics(SM_YVIRTUALSCREEN) / scaleY;
        double width = GetSystemMetrics(SM_CXVIRTUALSCREEN) / scaleX;
        double height = GetSystemMetrics(SM_CYVIRTUALSCREEN) / scaleY;

        return new Rect(left, top, width, height);
    }

    /// <summary>窗口所在显示器的工作区（去掉任务栏）的 DIP 边界。</summary>
    public static Rect GetWorkAreaDips(Visual visual)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(visual);
        double scaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;

        if (visual is Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                IntPtr monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfoW(monitor, ref info))
                    {
                        return new Rect(
                            info.rcWork.Left / scaleX,
                            info.rcWork.Top / scaleY,
                            (info.rcWork.Right - info.rcWork.Left) / scaleX,
                            (info.rcWork.Bottom - info.rcWork.Top) / scaleY);
                    }
                }
            }
        }

        return SystemParameters.WorkArea;
    }
}

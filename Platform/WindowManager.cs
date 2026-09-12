using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet.Platform;

/// <summary>
/// 跨显示器虚拟桌面边界计算。Win32 返回的是物理像素，这里换算为 WPF 的 DIP，
/// 以保证多显示器 + 高 DPI 下坐标一致。
/// </summary>
public static class WindowManager
{
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

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
}

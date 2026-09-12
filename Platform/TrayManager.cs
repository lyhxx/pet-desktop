using System.Runtime.InteropServices;
using DesktopPet.UI;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopPet.Platform;

/// <summary>
/// 系统托盘。菜单只保留必要功能：显示/隐藏、设置、重新定位、退出。
/// 通知走应用内轻提示（<see cref="ToastWindow"/>），不用系统气泡。
/// </summary>
public sealed class TrayManager : IDisposable
{
    private Forms.NotifyIcon? _icon;

    public void Initialize()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏宠物", null, (_, _) => AppServices.Pet.ToggleVisible());
        menu.Items.Add("设置", null, (_, _) => AppServices.Pet.OpenSettings());
        menu.Items.Add("重新定位宠物", null, (_, _) => AppServices.Pet.RepositionToCenter());
        menu.Items.Add("检查更新", null, (_, _) => { _ = AppServices.App.CheckForUpdates(manual: true); });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => AppServices.App.ExitApp());

        _icon = new Forms.NotifyIcon
        {
            Icon = CreateIcon(),
            Visible = true,
            Text = "桌面宠物",
            ContextMenuStrip = menu
        };

        _icon.DoubleClick += (_, _) => AppServices.Pet.ToggleVisible();
    }

    public void NotifyUpdate(string version, string url)
        => ToastWindow.Show("发现新版本", $"桌面宠物 v{version} 已发布（当前 v{AppInfo.Version}），点击查看。", url);

    public void NotifyInfo(string title, string text) => ToastWindow.Show(title, text);

    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static Drawing.Icon CreateIcon()
    {
        // 托盘图标按当前 DPI 的小图标尺寸取帧（100%→16、150%→24、200%→32），
        // 固定用 16 会被系统拉伸而发虚。
        int width = GetSystemMetrics(SM_CXSMICON);
        int height = GetSystemMetrics(SM_CYSMICON);
        if (width <= 0) width = 16;
        if (height <= 0) height = 16;

        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(path))
        {
            try
            {
                return new Drawing.Icon(path, width, height);
            }
            catch
            {
                // 落到下面的运行时生成。
            }
        }

        return DrawFallbackIcon(width, height);
    }

    private static Drawing.Icon DrawFallbackIcon(int width, int height)
    {
        using var bitmap = new Drawing.Bitmap(width, height);
        using (Drawing.Graphics g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            float s = Math.Min(width, height) / 32f;

            using var body = new Drawing.SolidBrush(Drawing.Color.FromArgb(61, 70, 98));
            using var belly = new Drawing.SolidBrush(Drawing.Color.FromArgb(252, 245, 235));
            using var beak = new Drawing.SolidBrush(Drawing.Color.FromArgb(242, 169, 59));
            using var eye = new Drawing.SolidBrush(Drawing.Color.FromArgb(43, 48, 56));

            g.FillEllipse(body, 3 * s, 4 * s, 26 * s, 26 * s);
            g.FillEllipse(belly, 9 * s, 12 * s, 14 * s, 18 * s);
            g.FillEllipse(eye, 10 * s, 11 * s, 4 * s, 5 * s);
            g.FillEllipse(eye, 18 * s, 11 * s, 4 * s, 5 * s);
            g.FillPolygon(beak, new[]
            {
                new Drawing.Point((int)(14 * s), (int)(17 * s)),
                new Drawing.Point((int)(21 * s), (int)(19 * s)),
                new Drawing.Point((int)(14 * s), (int)(21 * s))
            });
        }

        IntPtr handle = bitmap.GetHicon();
        return Drawing.Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.Dispose();
        _icon = null;
    }
}

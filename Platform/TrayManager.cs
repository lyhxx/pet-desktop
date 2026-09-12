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
        => ToastWindow.Show("发现新版本", $"桌面宠物 v{version} 已发布，点击查看。", url);

    public void NotifyInfo(string title, string text) => ToastWindow.Show(title, text);

    private static Drawing.Icon CreateIcon()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (System.IO.File.Exists(path))
        {
            try
            {
                return new Drawing.Icon(path, 16, 16);
            }
            catch
            {
                // 落到下面的运行时生成。
            }
        }

        using var bitmap = new Drawing.Bitmap(32, 32);
        using (Drawing.Graphics g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            using var body = new Drawing.SolidBrush(Drawing.Color.FromArgb(61, 70, 98));
            using var belly = new Drawing.SolidBrush(Drawing.Color.FromArgb(252, 245, 235));
            using var beak = new Drawing.SolidBrush(Drawing.Color.FromArgb(242, 169, 59));
            using var eye = new Drawing.SolidBrush(Drawing.Color.FromArgb(43, 48, 56));

            g.FillEllipse(body, 3, 4, 26, 26);
            g.FillEllipse(belly, 9, 12, 14, 18);
            g.FillEllipse(eye, 10, 11, 4, 5);
            g.FillEllipse(eye, 18, 11, 4, 5);
            g.FillPolygon(beak, new[]
            {
                new Drawing.Point(14, 17), new Drawing.Point(21, 19), new Drawing.Point(14, 21)
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

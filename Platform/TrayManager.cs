using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopPet.Platform;

/// <summary>
/// 系统托盘。菜单只保留必要功能：显示/隐藏、设置、重新定位、退出。
/// </summary>
public sealed class TrayManager : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private string? _updateUrl;

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
        _icon.BalloonTipClicked += (_, _) => OpenUrl(_updateUrl);
    }

    public void NotifyUpdate(string version, string url)
    {
        _updateUrl = url;
        Notify("发现新版本", $"桌面宠物 v{version} 已发布，点此查看。", Forms.ToolTipIcon.Info);
    }

    public void NotifyInfo(string title, string text) => Notify(title, text, Forms.ToolTipIcon.Info);

    private void Notify(string title, string text, Forms.ToolTipIcon icon)
    {
        if (_icon is null) return;
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(6000);
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打开失败忽略。
        }
    }

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

            using var body = new Drawing.SolidBrush(Drawing.Color.FromArgb(246, 162, 75));
            using var ear = new Drawing.SolidBrush(Drawing.Color.FromArgb(232, 145, 58));
            using var eye = new Drawing.SolidBrush(Drawing.Color.FromArgb(59, 42, 26));

            g.FillPolygon(ear, new[]
            {
                new Drawing.Point(6, 12), new Drawing.Point(9, 1), new Drawing.Point(16, 10)
            });
            g.FillPolygon(ear, new[]
            {
                new Drawing.Point(26, 12), new Drawing.Point(23, 1), new Drawing.Point(16, 10)
            });
            g.FillEllipse(body, 3, 7, 26, 22);
            g.FillEllipse(eye, 10, 15, 4, 5);
            g.FillEllipse(eye, 18, 15, 4, 5);
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

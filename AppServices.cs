using DesktopPet.Core;
using DesktopPet.Platform;
using DesktopPet.UI;

namespace DesktopPet;

/// <summary>
/// 轻量服务定位器，用于托盘、设置窗口等跨组件访问。
/// 刻意保持简单，避免引入依赖注入容器。
/// </summary>
public static class AppServices
{
    public static App App { get; set; } = null!;
    public static PetWindow Pet { get; set; } = null!;
    public static PetSession Session { get; set; } = null!;
    public static AppSettings Settings { get; set; } = null!;
    public static SoundManager Sound { get; set; } = null!;

    /// <summary>请求一次立即存档（互动后、移动后等）。</summary>
    public static Action? RequestSave { get; set; }
}

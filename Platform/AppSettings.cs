namespace DesktopPet.Platform;

/// <summary>程序级配置（软件设置），与宠物状态分开保存。</summary>
public sealed class AppSettings
{
    public bool RunAtStartup { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public double Volume { get; set; } = 0.6;
    public bool PetVisible { get; set; } = true;

    /// <summary>宠物显示缩放：1.0 表示一个网格格（256 DIP）。0.5 = 128 DIP。</summary>
    public double PetScale { get; set; } = 0.5;
}

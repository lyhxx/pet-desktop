namespace DesktopPet.Core;

/// <summary>
/// 一次运行期间的会话数据：宠物状态、装扮、恢复位置。
/// 与 AppSettings（程序配置）分开，前者是"宠物"，后者是"软件"。
/// </summary>
public sealed class PetSession
{
    public PetState State { get; init; } = new();
    public OutfitState Outfit { get; init; } = new();

    public bool HasPosition { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

namespace DesktopPet.Core;

/// <summary>装扮选择。第一版为本地选择 + 保存，直接显示在宠物身上。</summary>
public sealed class OutfitState
{
    public const string None = "none";

    public string Hat { get; set; } = None;
    public string Glasses { get; set; } = None;
    public string Scarf { get; set; } = None;

    public OutfitState Clone() => new() { Hat = Hat, Glasses = Glasses, Scarf = Scarf };
}

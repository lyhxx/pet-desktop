namespace DesktopPet.Core;

/// <summary>一种食物对状态的影响。数值刻意保持简单。</summary>
public sealed record Food(string Name, double Hunger, double Mood);

public static class Foods
{
    public static readonly Food Fish = new("小鱼干", 28, 8);
    public static readonly Food Kibble = new("猫粮", 38, 4);
    public static readonly Food Milk = new("牛奶", 16, 10);

    public static IReadOnlyList<Food> All { get; } = new[] { Fish, Kibble, Milk };
}

namespace DesktopPet.Core;

/// <summary>
/// 宠物的基础养成状态。数值只用于影响行为，不做复杂经济系统。
/// 所有变化按真实经过时间计算，离线与在线使用同一套公式。
/// </summary>
public sealed class PetState
{
    public string Name { get; set; } = "小橘";

    /// <summary>饥饿：越高越饿。</summary>
    public double Hunger { get; set; } = 15;

    /// <summary>心情：越高越开心。</summary>
    public double Mood { get; set; } = 80;

    /// <summary>精力：越高越有精神。</summary>
    public double Energy { get; set; } = 90;

    /// <summary>健康：长期处于极端状态才会下降，永不死亡。</summary>
    public double Health { get; set; } = 100;

    /// <summary>由行为系统每帧同步，用于计算精力衰减/恢复方向。</summary>
    public bool Sleeping { get; set; }

    private const double HungerPerHour = 4.0;
    private const double EnergyPerHourAwake = 3.0;
    private const double EnergyPerHourSleep = 18.0;
    private const double MoodPerHourWhenHungry = 1.2;
    private const double MoodPerHourRecover = 0.6;

    public void ApplyElapsed(TimeSpan elapsed)
    {
        double hours = elapsed.TotalHours;
        if (hours <= 0) return;

        Hunger = Clamp(Hunger + HungerPerHour * hours);

        double energyDelta = Sleeping ? -EnergyPerHourSleep : EnergyPerHourAwake;
        Energy = Clamp(Energy + energyDelta * hours);

        if (Hunger > 70)
            Mood = Clamp(Mood - MoodPerHourWhenHungry * hours);
        else if (Hunger < 40 && !Sleeping)
            Mood = Clamp(Mood + MoodPerHourRecover * hours);

        if (Hunger > 90 || Energy < 5)
            Health = Clamp(Health - 1.5 * hours);
        else if (Hunger < 50 && Energy > 30)
            Health = Clamp(Health + 1.0 * hours);
    }

    /// <summary>喂食：降低饥饿、提升心情。</summary>
    public void ApplyFeed(Food food)
    {
        Hunger = Clamp(Hunger - food.Hunger);
        Mood = Clamp(Mood + food.Mood);
    }

    /// <summary>抚摸：提升心情。</summary>
    public void ApplyPetting() => Mood = Clamp(Mood + 8);

    /// <summary>
    /// 离线时间结算。曲线做封顶，宠物不会因为离开太久而"崩坏"，且永不死亡。
    /// 假设离开期间宠物大部分时间在休息：饥饿缓慢上升、精力恢复、心情随饥饿缓慢下降。
    /// </summary>
    public void ApplyOffline(TimeSpan elapsed)
    {
        double hours = Math.Min(elapsed.TotalHours, MaxOfflineHours);
        if (hours <= 0) return;

        Hunger = Clamp(Hunger + HungerPerHour * hours * 0.5);
        Energy = Clamp(Energy + EnergyPerHourSleep * hours * 0.4);

        if (Hunger > 60)
            Mood = Clamp(Mood - 0.8 * hours);

        Health = Clamp(Math.Max(Health, 20));
    }

    private const double MaxOfflineHours = 72;

    private static double Clamp(double v, double min = 0, double max = 100)
        => v < min ? min : (v > max ? max : v);
}

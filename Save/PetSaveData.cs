using DesktopPet.Core;
using DesktopPet.Platform;

namespace DesktopPet.Save;

/// <summary>本地 JSON 存档的数据结构。</summary>
public sealed class PetSaveData
{
    public string Name { get; set; } = "小橘";
    public double X { get; set; }
    public double Y { get; set; }
    public bool HasPosition { get; set; }

    public double Hunger { get; set; } = 15;
    public double Mood { get; set; } = 80;
    public double Energy { get; set; } = 90;
    public double Health { get; set; } = 100;

    public OutfitState Outfit { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
    public DateTimeOffset LastSaved { get; set; } = DateTimeOffset.Now;

    public PetSession ToSession() => new()
    {
        State = new PetState
        {
            Name = Name,
            Hunger = Hunger,
            Mood = Mood,
            Energy = Energy,
            Health = Health
        },
        Outfit = Outfit.Clone(),
        HasPosition = HasPosition,
        X = X,
        Y = Y
    };

    /// <summary>根据上次保存时间结算离线变化，并更新 LastSaved。</summary>
    public void ApplyOffline(DateTimeOffset now)
    {
        var state = new PetState
        {
            Name = Name,
            Hunger = Hunger,
            Mood = Mood,
            Energy = Energy,
            Health = Health
        };

        state.ApplyOffline(now - LastSaved);

        Hunger = state.Hunger;
        Mood = state.Mood;
        Energy = state.Energy;
        Health = state.Health;
        LastSaved = now;
    }

    public static PetSaveData From(PetSession session, AppSettings settings,
        double x, double y, bool hasPosition) => new()
    {
        Name = session.State.Name,
        X = x,
        Y = y,
        HasPosition = hasPosition,
        Hunger = session.State.Hunger,
        Mood = session.State.Mood,
        Energy = session.State.Energy,
        Health = session.State.Health,
        Outfit = session.Outfit.Clone(),
        Settings = settings,
        LastSaved = DateTimeOffset.Now
    };
}

namespace DesktopPet.Core;

/// <summary>
/// 行为 / 步态 → 动画剪辑名的集中映射，并定义图集缺失时的回退链。
/// 剪辑名与 Assets/Pet/animations.json 保持一致；
/// 后续新增图集时只需补 JSON，这里无需改动（缺失的图集会沿回退链降级到 Idle）。
/// </summary>
public static class AnimationClips
{
    // Idle 图集
    public const string Idle = "Idle";
    public const string Blink = "Blink";
    public const string LookAround = "LookAround";
    public const string Stretch = "Stretch";
    public const string Groom = "Groom";
    public const string Curious = "Curious";
    public const string Tail = "Tail";
    public const string Happy = "Happy";
    public const string Doze = "Doze";
    public const string Fidget = "Fidget";
    public const string Special = "Special";

    // Move 图集
    public const string Walk = "Walk";
    public const string WalkSmall = "WalkSmall";
    public const string Trot = "Trot";
    public const string MoveFast = "MoveFast";
    public const string MoveBack = "MoveBack";
    public const string Turn = "Turn";
    public const string SlowDown = "SlowDown";
    public const string Stop = "Stop";
    public const string StopAfterMove = "StopAfterMove";

    // 待制作图集（Action / EatSleep）
    public const string Sleep = "Sleep";
    public const string Eat = "Eat";
    public const string Petted = "Petted";
    public const string Sad = "Sad";
    public const string Angry = "Angry";

    public static string ForState(BehaviorState state) => state switch
    {
        BehaviorState.Idle => Idle,
        BehaviorState.Walk => Walk,
        BehaviorState.Turn => Turn,
        BehaviorState.Stop => StopAfterMove,
        BehaviorState.LookAround => LookAround,
        BehaviorState.Sit => Doze,
        BehaviorState.Sleep => Sleep,
        BehaviorState.Eat => Eat,
        BehaviorState.Happy => Happy,
        BehaviorState.Sad => Sad,
        BehaviorState.Angry => Angry,
        BehaviorState.Curious => Curious,
        BehaviorState.Interact => Petted,
        BehaviorState.Stretch => Stretch,
        BehaviorState.Groom => Groom,
        BehaviorState.Tail => Tail,
        BehaviorState.Fidget => Fidget,
        BehaviorState.Special => Special,
        _ => Idle
    };

    public static string ForGait(Gait gait) => gait switch
    {
        Gait.Small => WalkSmall,
        Gait.Trot => Trot,
        Gait.Fast => MoveFast,
        _ => Walk
    };

    /// <summary>首选剪辑所在图集缺失时的回退顺序，最后一定落到 Idle。</summary>
    public static IReadOnlyList<string> Chain(string clip)
    {
        var chain = new List<string> { clip };

        switch (clip)
        {
            case WalkSmall:
            case Trot:
            case MoveFast:
            case MoveBack:
            case Turn:
            case SlowDown:
            case Stop:
            case StopAfterMove:
                chain.Add(Walk);
                break;
            case Sleep:
                chain.Add(Doze);
                break;
        }

        if (!chain.Contains(Idle)) chain.Add(Idle);
        return chain;
    }
}

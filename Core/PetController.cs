namespace DesktopPet.Core;

/// <summary>
/// 把行为系统、状态模型和视图串起来的协调者。
/// 本身不依赖 WPF，所有界面操作都通过 <see cref="IPetView"/>。
/// </summary>
public sealed class PetController
{
    private readonly IPetView _view;
    private readonly PetBehavior _behavior;
    private readonly PetState _state;
    private readonly Random _rng = new();

    // 互动是一串连续的强制状态（如 被摸 → 开心 → 互动结束）。
    private readonly Queue<(BehaviorState State, double Seconds)> _pending = new();

    private double _pauseSeconds;
    private double _interactionRemaining;

    /// <summary>请求独立粒子层播放效果，与宠物状态机解耦。</summary>
    public event EventHandler<EffectKind>? EffectRequested;

    public PetController(IPetView view, PetBehavior? behavior = null, PetState? state = null)
    {
        _view = view;
        _behavior = behavior ?? new PetBehavior();
        _state = state ?? new PetState();

        _behavior.StateChanged += (_, s) => _view.PlayAnimation(ClipFor(s));
        _behavior.FacingChanged += (_, f) => _view.SetFacing(f);
    }

    public PetState State => _state;
    public PetBehavior Behavior => _behavior;

    /// <summary>暂停/恢复自主行为（悬停菜单打开时用）。</summary>
    public bool Autonomous
    {
        get => _behavior.Autonomous;
        set => _behavior.Autonomous = value;
    }

    /// <summary>进入场景：先打个招呼再回到自主行为。</summary>
    public void Start() => BeginInteraction(BehaviorState.Wave, 1.0);

    public void SetBounds(double minX, double maxX, double minY, double maxY)
        => _behavior.SetBounds(minX, maxX, minY, maxY);

    public void PlaceAt(double x, double y)
    {
        _behavior.PlaceAt(x, y);
        _view.MoveTo(_behavior.X, _behavior.Y);
    }

    /// <summary>喂食：吃东西 → 开心 → 互动结束。</summary>
    public void Feed(Food food)
    {
        // 已经很饱就不吃了，只开心一下。
        if (_state.Hunger <= 2)
        {
            EffectRequested?.Invoke(this, EffectKind.Heart);
            BeginInteraction(BehaviorState.Happy, 1.0, (BehaviorState.InteractEnd, 0.4));
            return;
        }

        _state.ApplyFeed(food);
        EffectRequested?.Invoke(this, EffectKind.Heart);
        BeginInteraction(BehaviorState.Eat, 1.8,
            (BehaviorState.Happy, 0.9),
            (BehaviorState.InteractEnd, 0.4));
    }

    /// <summary>抚摸：被摸 → 开心 / 害羞（饿了会生气）→ 互动结束。</summary>
    public void Pet()
    {
        _state.ApplyPetting();
        EffectRequested?.Invoke(this, EffectKind.Heart);

        BehaviorState mood = _state.Hunger > 75
            ? BehaviorState.Angry
            : _rng.NextDouble() < 0.35 ? BehaviorState.Shy : BehaviorState.Happy;

        BeginInteraction(BehaviorState.Interact, 1.0,
            (mood, 0.9),
            (BehaviorState.InteractEnd, 0.4));
    }

    public void Tick(double dt)
    {
        if (dt <= 0) return;

        _state.Sleeping = _behavior.State == BehaviorState.Sleep;
        _state.ApplyElapsed(TimeSpan.FromSeconds(dt));

        if (_interactionRemaining > 0)
        {
            _interactionRemaining -= dt;
            if (_interactionRemaining <= 0)
            {
                if (_pending.Count > 0)
                {
                    (BehaviorState next, double seconds) = _pending.Dequeue();
                    _behavior.OverrideState(next);
                    _interactionRemaining = seconds;
                }
                else
                {
                    _behavior.ReleaseOverride();
                }
            }

            _view.MoveTo(_behavior.X, _behavior.Y);
            return;
        }

        if (_pauseSeconds > 0)
        {
            _pauseSeconds -= dt;
            _view.MoveTo(_behavior.X, _behavior.Y);
            return;
        }

        _behavior.Tick(dt, _state);
        _view.MoveTo(_behavior.X, _behavior.Y);
    }

    /// <summary>用户拖动结束后同步位置，并短暂停顿，避免刚放下就自己走开。</summary>
    public void NotifyUserMoved(double x, double y)
    {
        ClearInteraction();
        _behavior.PlaceAt(x, y);
        _pauseSeconds = 1.2;
        _view.MoveTo(_behavior.X, _behavior.Y);
    }

    /// <summary>被点击：睡着/坐着时先吓一跳，否则播放"被点击"。</summary>
    public void NotifyClicked()
    {
        BehaviorState reaction = _behavior.State is BehaviorState.Sleep or BehaviorState.Sit
            ? BehaviorState.Surprised
            : BehaviorState.Clicked;

        EffectRequested?.Invoke(this,
            reaction == BehaviorState.Surprised ? EffectKind.Sweat : EffectKind.Star);

        BeginInteraction(reaction, 0.9);
    }

    /// <summary>被拎起（拖动开始）：受到惊吓。</summary>
    public void NotifyPickedUp()
    {
        EffectRequested?.Invoke(this, EffectKind.Sweat);
        BeginInteraction(BehaviorState.Startled, 0.5);
    }

    private void BeginInteraction(BehaviorState first, double seconds,
        params (BehaviorState State, double Seconds)[] followUps)
    {
        _pending.Clear();
        foreach (var step in followUps)
            _pending.Enqueue(step);

        _behavior.OverrideState(first);
        _interactionRemaining = seconds;
        _pauseSeconds = 0;
    }

    private void ClearInteraction()
    {
        _pending.Clear();
        _interactionRemaining = 0;
    }

    private string ClipFor(BehaviorState s)
        => s == BehaviorState.Walk
            ? AnimationClips.ForGait(_behavior.Gait)
            : AnimationClips.ForState(s);
}

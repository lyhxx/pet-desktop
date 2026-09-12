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

    private double _pauseSeconds;
    private double _interactionRemaining;
    private BehaviorState? _followUp;
    private double _followUpRemaining;

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

    /// <summary>进入场景，播放初始动画。</summary>
    public void Start() => _view.PlayAnimation(ClipFor(_behavior.State));

    public void SetBounds(double minX, double maxX, double minY, double maxY)
        => _behavior.SetBounds(minX, maxX, minY, maxY);

    public void PlaceAt(double x, double y)
    {
        _behavior.PlaceAt(x, y);
        _view.MoveTo(_behavior.X, _behavior.Y);
    }

    /// <summary>喂食：改变状态 + 播放吃东西，随后开心，并触发爱心效果。</summary>
    public void Feed(Food food)
    {
        // 已经很饱就不吃了，只开心一下，避免无意义地反复喂。
        if (_state.Hunger <= 2)
        {
            _behavior.OverrideState(BehaviorState.Happy);
            EffectRequested?.Invoke(this, EffectKind.Heart);
            _interactionRemaining = 1.0;
            _followUp = null;
            return;
        }

        _state.ApplyFeed(food);
        _behavior.OverrideState(BehaviorState.Eat);
        EffectRequested?.Invoke(this, EffectKind.Heart);
        _interactionRemaining = 2.4;
        _followUp = BehaviorState.Happy;
        _followUpRemaining = 1.4;
    }

    /// <summary>抚摸：提升心情，播放被摸，随后开心。</summary>
    public void Pet()
    {
        _state.ApplyPetting();
        _behavior.OverrideState(BehaviorState.Interact);
        EffectRequested?.Invoke(this, EffectKind.Heart);
        _interactionRemaining = 1.8;
        _followUp = BehaviorState.Happy;
        _followUpRemaining = 1.2;
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
                if (_followUp is { } next)
                {
                    _behavior.OverrideState(next);
                    _interactionRemaining = _followUpRemaining;
                    _followUp = null;
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
        _interactionRemaining = 0;
        _followUp = null;
        _behavior.PlaceAt(x, y);
        _pauseSeconds = 1.2;
        _view.MoveTo(_behavior.X, _behavior.Y);
    }

    public void NotifyClicked() => _pauseSeconds = 0.8;

    private string ClipFor(BehaviorState s)
        => s == BehaviorState.Walk
            ? AnimationClips.ForGait(_behavior.Gait)
            : AnimationClips.ForState(s);
}

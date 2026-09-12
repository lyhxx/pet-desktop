namespace DesktopPet.Core;

/// <summary>
/// 宠物行为系统。完全不依赖 WPF，坐标以"双脚锚点"为单位（DIP）。
/// 通过加权随机 + 需求影响来选择下一个行为，而不是固定时间轴循环。
/// 走动带步态（普通 / 小步 / 小跑 / 快走）、转身与停下过渡。
/// </summary>
public sealed class PetBehavior
{
    private const double TurnSeconds = 0.7;
    private const double StopMinSeconds = 0.3;
    private const double StopMaxSeconds = 0.5;
    private const double RestMinSeconds = 2.0;
    private const double RestMaxSeconds = 5.0;

    private readonly Random _rng;

    private double _minX, _maxX, _minY, _maxY;
    private double _targetX;
    private double _pendingTargetX;
    private double _timeLeft;
    private double _speed = 40.0;
    private double _walkCooldown;
    private double _restCooldown;
    private bool _overridden;

    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>false 时暂停自主行为（不随机走动/换动作），但仍可被互动覆盖。</summary>
    public bool Autonomous { get; set; } = true;

    public BehaviorState State { get; private set; } = BehaviorState.Idle;
    public int Facing { get; private set; } = 1;

    /// <summary>当前走动的步态，供视图选择对应动画剪辑。</summary>
    public Gait Gait { get; private set; } = Gait.Walk;

    public event EventHandler<BehaviorState>? StateChanged;
    public event EventHandler<int>? FacingChanged;

    public PetBehavior(Random? rng = null) => _rng = rng ?? new Random();

    public void SetBounds(double minX, double maxX, double minY, double maxY)
    {
        _minX = Math.Min(minX, maxX);
        _maxX = Math.Max(minX, maxX);
        _minY = Math.Min(minY, maxY);
        _maxY = Math.Max(minY, maxY);
        X = Clamp(X, _minX, _maxX);
        Y = Clamp(Y, _minY, _maxY);
    }

    public void PlaceAt(double x, double y)
    {
        X = Clamp(x, _minX, _maxX);
        Y = Clamp(y, _minY, _maxY);
        _overridden = false;
        SetState(BehaviorState.Idle, 0.6, 1.6);
    }

    /// <summary>强制进入某个行为（互动时使用），期间不参与随机选择。</summary>
    public void OverrideState(BehaviorState state)
    {
        _overridden = true;
        _timeLeft = double.MaxValue;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>结束强制状态，回到自主行为。</summary>
    public void ReleaseOverride()
    {
        _overridden = false;
        _timeLeft = 0.8 + _rng.NextDouble() * 1.2;
        State = BehaviorState.Idle;
        StateChanged?.Invoke(this, BehaviorState.Idle);
    }

    public void Tick(double dt, PetState state)
    {
        if (_overridden) return;

        if (_walkCooldown > 0)
            _walkCooldown = Math.Max(0, _walkCooldown - dt);
        if (_restCooldown > 0)
            _restCooldown = Math.Max(0, _restCooldown - dt);

        if (!Autonomous)
        {
            if (State is BehaviorState.Walk or BehaviorState.Turn or BehaviorState.Stop)
                SetState(BehaviorState.Idle, 1.0, 2.0);
            return;
        }

        // 过渡态：转身结束后开始走，停下结束后回到待机。
        switch (State)
        {
            case BehaviorState.Turn:
                _timeLeft -= dt;
                if (_timeLeft <= 0)
                    BeginWalk(_pendingTargetX);
                return;

            case BehaviorState.Walk:
                StepToward(dt);
                if (Math.Abs(_targetX - X) < 1.0)
                    SetState(BehaviorState.Stop, StopMinSeconds, StopMaxSeconds);
                return;

            case BehaviorState.Stop:
                _timeLeft -= dt;
                if (_timeLeft <= 0)
                    RestAfterAction();
                return;
        }

        _timeLeft -= dt;
        if (_timeLeft > 0) return;

        // 主动动作播完先回待机休息一段，避免一个动作接一个动作地连播。
        if (IsActive(State))
        {
            RestAfterAction();
            return;
        }

        ChooseNext(state);
    }

    /// <summary>结束一个主动动作：进入待机，并给一段休息冷却，减少动作切换频率。</summary>
    private void RestAfterAction()
    {
        _restCooldown = RestMinSeconds + _rng.NextDouble() * (RestMaxSeconds - RestMinSeconds);
        SetState(BehaviorState.Idle, 3.5, 7.0);
    }

    private static bool IsActive(BehaviorState state) => state switch
    {
        BehaviorState.LookAround or BehaviorState.Curious or BehaviorState.Tail
            or BehaviorState.Groom or BehaviorState.Fidget or BehaviorState.Stretch
            or BehaviorState.Special or BehaviorState.Walk or BehaviorState.Turn
            or BehaviorState.Stop => true,
        _ => false
    };

    private void StepToward(double dt)
    {
        double delta = _targetX - X;
        int dir = Math.Sign(delta);
        if (dir == 0) return;

        SetFacing(dir > 0 ? 1 : -1);

        double next = X + dir * _speed * dt;
        if ((dir > 0 && next >= _targetX) || (dir < 0 && next <= _targetX))
            next = _targetX;
        X = Clamp(next, _minX, _maxX);
    }

    private void ChooseNext(PetState state)
    {
        // 需求影响权重：精力越低越可能睡觉，精力充足时更容易出现各种小动作。
        double alertness = Math.Clamp(state.Energy / 100.0, 0, 1);
        bool resting = _restCooldown > 0;

        // 休息冷却期间几乎只待机/坐着，避免动作一个接一个连播。
        double sleepWeight = 2 + Math.Max(0, 55 - state.Energy) * 1.6;
        double walkWeight = (resting || _walkCooldown > 0) ? 0 : 6 + Math.Max(0, state.Energy - 40) * 0.15;
        double idleWeight = resting ? 90 : 72;
        double sitWeight = resting ? 22 : 10 + Math.Max(0, 40 - state.Energy) * 0.4;
        double micro = resting ? 0 : alertness;

        BehaviorState picked = PickWeighted(
            (BehaviorState.Idle, idleWeight),
            (BehaviorState.Walk, walkWeight),
            (BehaviorState.Sit, sitWeight),
            (BehaviorState.Sleep, sleepWeight),
            (BehaviorState.LookAround, 5 * micro),
            (BehaviorState.Curious, 4 * micro),
            (BehaviorState.Tail, 3 * micro),
            (BehaviorState.Groom, 3 * micro),
            (BehaviorState.Fidget, 3 * micro),
            (BehaviorState.Stretch, 3 * micro),
            (BehaviorState.Special, 2 * micro));

        switch (picked)
        {
            case BehaviorState.Walk:
                StartWalk();
                break;

            case BehaviorState.Sleep:
                SetState(BehaviorState.Sleep, 10, 20);
                break;

            case BehaviorState.Sit:
                SetState(BehaviorState.Sit, 5, 10);
                break;

            case BehaviorState.LookAround:
                SetState(BehaviorState.LookAround, 1.6, 3.0);
                break;

            case BehaviorState.Curious:
                SetState(BehaviorState.Curious, 1.8, 3.2);
                break;

            case BehaviorState.Tail:
                SetState(BehaviorState.Tail, 1.6, 2.8);
                break;

            case BehaviorState.Groom:
                SetState(BehaviorState.Groom, 1.8, 3.2);
                break;

            case BehaviorState.Fidget:
                SetState(BehaviorState.Fidget, 1.8, 3.0);
                break;

            case BehaviorState.Stretch:
                SetState(BehaviorState.Stretch, 1.8, 3.0);
                break;

            case BehaviorState.Special:
                SetState(BehaviorState.Special, 1.4, 2.4);
                break;

            default:
                SetState(BehaviorState.Idle, 4.0, 9.0);
                break;
        }
    }

    /// <summary>挑选一次走动：确定目标点、步态；方向相反时先转身。</summary>
    private void StartWalk()
    {
        double reach = 60 + _rng.NextDouble() * 180;
        double dir = _rng.NextDouble() < 0.5 ? -1 : 1;
        double target = Clamp(X + dir * reach, _minX, _maxX);

        double distance = Math.Abs(target - X);
        if (distance < 8)
        {
            SetState(BehaviorState.Idle, 2.0, 4.0);
            return;
        }

        Gait = PickGait(distance);
        _speed = SpeedFor(Gait);
        _walkCooldown = 18 + _rng.NextDouble() * 22;

        int facing = target >= X ? 1 : -1;
        if (facing != Facing)
        {
            // 先转身再走，避免瞬间镜像。
            _pendingTargetX = target;
            SetFacing(facing);
            SetState(BehaviorState.Turn, TurnSeconds, TurnSeconds);
            return;
        }

        BeginWalk(target);
    }

    private void BeginWalk(double target)
    {
        _targetX = target;
        double travel = Math.Abs(_targetX - X) / Math.Max(1, _speed);
        SetState(BehaviorState.Walk, travel + 0.2, travel + 0.2);
    }

    /// <summary>按距离与随机挑选步态：近距离偏小步，远距离偏快走/小跑。</summary>
    private Gait PickGait(double distance)
    {
        double roll = _rng.NextDouble();

        if (distance > 170)
            return roll < 0.5 ? Gait.Fast : Gait.Trot;

        if (distance < 90)
            return roll < 0.6 ? Gait.Small : Gait.Walk;

        if (roll < 0.5) return Gait.Walk;
        if (roll < 0.8) return Gait.Small;
        return Gait.Trot;
    }

    private static double SpeedFor(Gait gait) => gait switch
    {
        Gait.Small => 30,
        Gait.Trot => 62,
        Gait.Fast => 82,
        _ => 40
    };

    private BehaviorState PickWeighted(params (BehaviorState State, double Weight)[] choices)
    {
        double total = 0;
        foreach (var c in choices) total += Math.Max(0, c.Weight);
        if (total <= 0) return BehaviorState.Idle;

        double roll = _rng.NextDouble() * total;
        foreach (var c in choices)
        {
            roll -= Math.Max(0, c.Weight);
            if (roll <= 0) return c.State;
        }
        return choices[^1].State;
    }

    private void SetState(BehaviorState next, double minSeconds, double maxSeconds)
    {
        _timeLeft = minSeconds + _rng.NextDouble() * Math.Max(0, maxSeconds - minSeconds);
        if (next == State) return;
        State = next;
        StateChanged?.Invoke(this, next);
    }

    private void SetFacing(int facing)
    {
        facing = facing >= 0 ? 1 : -1;
        if (facing == Facing) return;
        Facing = facing;
        FacingChanged?.Invoke(this, facing);
    }

    private static double Clamp(double v, double min, double max)
    {
        if (min > max) (min, max) = (max, min);
        return v < min ? min : (v > max ? max : v);
    }
}

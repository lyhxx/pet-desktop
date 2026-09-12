namespace DesktopPet.Core;

/// <summary>
/// 宠物行为系统。完全不依赖 WPF，坐标以"双脚锚点"为单位（DIP）。
/// 通过加权随机 + 需求影响来选择下一个行为，而不是固定时间轴循环。
/// </summary>
public sealed class PetBehavior
{
    private readonly Random _rng;

    private double _minX, _maxX, _minY, _maxY;
    private double _targetX;
    private double _timeLeft;
    private double _speed = 40.0;
    private double _walkCooldown;
    private bool _overridden;

    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>false 时暂停自主行为（不随机走动/换动作），但仍可被互动覆盖。</summary>
    public bool Autonomous { get; set; } = true;

    public BehaviorState State { get; private set; } = BehaviorState.Idle;
    public int Facing { get; private set; } = 1;

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

        if (!Autonomous)
        {
            if (State == BehaviorState.Walk)
                SetState(BehaviorState.Idle, 1.0, 2.0);
            return;
        }

        if (State == BehaviorState.Walk)
        {
            StepToward(dt);
            if (Math.Abs(_targetX - X) < 1.0)
            {
                // 走完后安静待一会儿，避免一直来回走。
                SetState(BehaviorState.Idle, 3.5, 8.0);
                return;
            }
        }

        _timeLeft -= dt;
        if (_timeLeft <= 0)
            ChooseNext(state);
    }

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

        double sleepWeight = 2 + Math.Max(0, 55 - state.Energy) * 1.6;
        double walkWeight = _walkCooldown > 0 ? 0 : 10 + Math.Max(0, state.Energy - 40) * 0.2;
        double idleWeight = 42;
        double sitWeight = 12 + Math.Max(0, 40 - state.Energy) * 0.4;

        BehaviorState picked = PickWeighted(
            (BehaviorState.Idle, idleWeight),
            (BehaviorState.Walk, walkWeight),
            (BehaviorState.Sit, sitWeight),
            (BehaviorState.Sleep, sleepWeight),
            (BehaviorState.LookAround, 10 * alertness),
            (BehaviorState.Curious, 7 * alertness),
            (BehaviorState.Tail, 6 * alertness),
            (BehaviorState.Groom, 6 * alertness),
            (BehaviorState.Fidget, 5 * alertness),
            (BehaviorState.Stretch, 4 * alertness),
            (BehaviorState.Special, 3 * alertness));

        switch (picked)
        {
            case BehaviorState.Walk:
                double reach = 60 + _rng.NextDouble() * 180;
                double dir = _rng.NextDouble() < 0.5 ? -1 : 1;
                _targetX = Clamp(X + dir * reach, _minX, _maxX);
                _walkCooldown = 10 + _rng.NextDouble() * 14;
                double travel = Math.Abs(_targetX - X) / Math.Max(1, _speed);
                SetFacing(_targetX >= X ? 1 : -1);
                SetState(BehaviorState.Walk, travel + 0.2, travel + 0.2);
                break;

            case BehaviorState.Sleep:
                SetState(BehaviorState.Sleep, 8, 16);
                break;

            case BehaviorState.Sit:
                SetState(BehaviorState.Sit, 4, 9);
                break;

            case BehaviorState.LookAround:
                SetState(BehaviorState.LookAround, 1.2, 2.2);
                break;

            case BehaviorState.Curious:
                SetState(BehaviorState.Curious, 1.4, 2.4);
                break;

            case BehaviorState.Tail:
                SetState(BehaviorState.Tail, 1.2, 2.2);
                break;

            case BehaviorState.Groom:
                SetState(BehaviorState.Groom, 1.4, 2.4);
                break;

            case BehaviorState.Fidget:
                SetState(BehaviorState.Fidget, 1.4, 2.4);
                break;

            case BehaviorState.Stretch:
                SetState(BehaviorState.Stretch, 1.4, 2.4);
                break;

            case BehaviorState.Special:
                SetState(BehaviorState.Special, 1.0, 1.8);
                break;

            default:
                SetState(BehaviorState.Idle, 3.0, 8.0);
                break;
        }
    }

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

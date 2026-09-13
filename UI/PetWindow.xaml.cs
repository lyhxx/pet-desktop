using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Animation;
using DesktopPet.Core;
using DesktopPet.Platform;

namespace DesktopPet.UI;

/// <summary>
/// 桌面宠物窗口：无边框、透明、无任务栏按钮、可置顶、可拖动。
/// 窗口尺寸固定等于一个网格单元，宠物双脚锚点对齐到窗口内 (128, 236)。
/// </summary>
public partial class PetWindow : Window, IPetView
{
    private const int CellSize = 256;
    private const double BaseAnchorX = 128;
    private const double BaseAnchorY = 236;
    private const int DragThreshold = 4;
    private const double MaxFrameDelta = 0.25;
    private const double SleepEffectInterval = 3.0;
    private const double OneShotHoldSeconds = 0.25;

    /// <summary>
    /// 这些待机小动作剪辑比行为状态短，播完后自动回到循环的 Idle，
    /// 避免停在最后一帧看起来像"卡住"。互动表情与走路等由控制器接管，不在此列。
    /// </summary>
    private static readonly HashSet<string> AutoReturnClips = new(StringComparer.OrdinalIgnoreCase)
    {
        AnimationClips.LookAround, AnimationClips.Stretch, AnimationClips.Groom,
        AnimationClips.Tail, AnimationClips.Fidget, AnimationClips.Special,
        AnimationClips.Curious
    };

    private double _anchorX = BaseAnchorX;
    private double _anchorY = BaseAnchorY;

    private readonly PetSession _session;
    private readonly SoundManager _sound;
    private readonly PetAnimation _animation;
    private readonly PetController _controller;
    private readonly EffectLayer _effects;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private TimeSpan _lastTick;
    private double _sleepEffectTimer;
    private double _blinkCountdown = 3.0;
    private bool _blinking;
    private double _oneShotHold;

    private bool _pressed;
    private bool _dragging;
    private Point _pressScreen;
    private double _pressLeft;
    private double _pressTop;

    private StatusWindow? _statusWindow;
    private SettingsWindow? _settingsWindow;
    private OutfitWindow? _outfitWindow;

    private PetMenuWindow? _menuWindow;
    private DispatcherTimer? _hoverTimer;
    private DispatcherTimer? _closeTimer;
    private bool _menuOpen;
    private bool _menuOnRight = true;

    public PetWindow(PetSession session, SoundManager sound)
    {
        InitializeComponent();

        _session = session;
        _sound = sound;

        _effects = new EffectLayer(EffectCanvas);
        _animation = new PetAnimation(AnimationLibrary.LoadDefault());
        _animation.FrameChanged += OnFrameChanged;
        _controller = new PetController(this, state: session.State);
        _controller.EffectRequested += (_, kind) => _effects.Play(kind);

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTimerTick;

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseEnter += OnPetMouseEnter;
        MouseLeave += OnPetMouseLeave;

        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer!.Stop();
            ShowMenu();
        };

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer!.Stop();
            HideMenu();
        };

        ApplyOutfit();
        ApplyScale();
    }

    public PetController Controller => _controller;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySettings();

        // 活动范围用工作区（去掉任务栏），宠物站在任务栏上沿而不是屏幕最底部。
        Rect work = WindowManager.GetWorkAreaDips(this);
        ApplyBounds(work);

        // 只恢复水平位置；竖直方向每次启动都回到地面（任务栏上沿），
        // 避免上次拖到半空后一直悬在屏幕中间。
        double startX = _session.HasPosition ? _session.X : work.Left + work.Width / 2;

        _controller.PlaceAt(startX, work.Bottom);
        _controller.Start();

        _lastTick = _clock.Elapsed;
        _timer.Start();

        if (AppServices.Settings.PetVisible)
        {
            Opacity = 1;
        }
        else
        {
            _timer.Stop();
            Hide();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!AppServices.App.IsExiting)
        {
            e.Cancel = true;
            HidePet();
            return;
        }

        _timer.Stop();
        HideMenu();
        AppServices.RequestSave?.Invoke();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        TimeSpan now = _clock.Elapsed;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        if (dt <= 0) return;
        if (dt > MaxFrameDelta) dt = MaxFrameDelta;

        if (!_pressed)
            _controller.Tick(dt);

        _animation.Tick(dt);
        UpdateAmbientEffects(dt);
        UpdateBlink(dt);
        UpdateOneShot(dt);
    }

    /// <summary>
    /// 待机小动作（张望 / 理毛等）播完后回到循环的待机，
    /// 避免停在最后一帧看起来像"卡住"。
    /// </summary>
    private void UpdateOneShot(double dt)
    {
        if (_pressed ||
            _animation.CurrentClipName is not { } clip ||
            !AutoReturnClips.Contains(clip) ||
            !_animation.IsFinished)
        {
            _oneShotHold = 0;
            return;
        }

        _oneShotHold += dt;
        if (_oneShotHold < OneShotHoldSeconds) return;

        _oneShotHold = 0;
        PlayAnimation(AnimationClips.Idle);
    }

    /// <summary>待机时偶尔播放眨眼，让静止画面有生气（用 Idle 图集的 Blink 帧）。</summary>
    private void UpdateBlink(double dt)
    {
        bool canBlink = !_pressed && !_menuOpen
            && _controller.Behavior.State == BehaviorState.Idle;

        if (!canBlink)
        {
            _blinking = false;
            _blinkCountdown = NextBlinkDelay();
            return;
        }

        if (_blinking)
        {
            if (!_animation.IsFinished) return;
            _blinking = false;
            PlayAnimation(AnimationClips.Idle);
            _blinkCountdown = NextBlinkDelay();
            return;
        }

        _blinkCountdown -= dt;
        if (_blinkCountdown > 0) return;

        _blinkCountdown = NextBlinkDelay();
        if (PlayAnimation(AnimationClips.Blink) &&
            _animation.CurrentClipName == AnimationClips.Blink)
        {
            _blinking = true;
        }
    }

    private static double NextBlinkDelay() => 2.5 + Random.Shared.NextDouble() * 4.5;

    private void UpdateAmbientEffects(double dt)
    {
        if (_controller.Behavior.State == BehaviorState.Sleep)
        {
            _sleepEffectTimer += dt;
            if (_sleepEffectTimer >= SleepEffectInterval)
            {
                _sleepEffectTimer = 0;
                _effects.Play(EffectKind.Sleep);
            }
        }
        else
        {
            _sleepEffectTimer = 0;
        }
    }

    private void OnFrameChanged(object? sender, BitmapSource frame) => SpriteImage.Source = frame;

    // ---- IPetView ----

    public void MoveTo(double x, double y)
    {
        Left = x - _anchorX;
        Top = y - _anchorY;
    }

    public void SetFacing(int facing) => FacingTransform.ScaleX = facing >= 0 ? 1 : -1;

    public bool PlayAnimation(string clipName)
    {
        // 对应图集尚未制作时，沿回退链降级（如 Sleep → Doze → Idle），保持形象一致。
        foreach (string candidate in AnimationClips.Chain(clipName))
        {
            if (!_animation.Play(candidate)) continue;
            SpriteImage.Visibility = Visibility.Visible;
            Placeholder.Visibility = Visibility.Collapsed;
            return true;
        }

        SpriteImage.Visibility = Visibility.Collapsed;
        Placeholder.Visibility = Visibility.Visible;
        return false;
    }

    // ---- 对外操作（托盘 / 设置窗口） ----

    public void ApplySettings()
    {
        Topmost = AppServices.Settings.AlwaysOnTop;
        _sound.Enabled = AppServices.Settings.SoundEnabled;
        _sound.Volume = AppServices.Settings.Volume;
        ApplyScale();
    }

    /// <summary>按设置缩放显示尺寸，并保持双脚锚点原地不动。</summary>
    public void ApplyScale()
    {
        double scale = Math.Clamp(AppServices.Settings.PetScale, 0.25, 1.0);

        double feetX = Left + _anchorX;
        double feetY = Top + _anchorY;

        _anchorX = BaseAnchorX * scale;
        _anchorY = BaseAnchorY * scale;
        Width = CellSize * scale;
        Height = CellSize * scale;

        if (IsLoaded && double.IsFinite(feetX) && double.IsFinite(feetY))
        {
            Left = feetX - _anchorX;
            Top = feetY - _anchorY;
        }
    }

    public void ApplyOutfit()
    {
        OutfitState outfit = _session.Outfit;
        HatLayer.Visibility = outfit.Hat == "cap" ? Visibility.Visible : Visibility.Collapsed;
        GlassesLayer.Visibility = outfit.Glasses == "glasses" ? Visibility.Visible : Visibility.Collapsed;
        ScarfLayer.Visibility = outfit.Scarf == "scarf" ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ToggleVisible()
    {
        if (IsVisible) HidePet();
        else ShowPet();
    }

    public void ShowPet()
    {
        Opacity = 1;
        Show();
        _timer.Start();
        AppServices.Settings.PetVisible = true;
        AppServices.RequestSave?.Invoke();
    }

    public void HidePet()
    {
        HideMenu();
        Hide();
        _timer.Stop();
        AppServices.Settings.PetVisible = false;
        AppServices.RequestSave?.Invoke();
    }

    public void RepositionToCenter()
    {
        RefreshBounds();
        Rect work = WindowManager.GetWorkAreaDips(this);
        _controller.NotifyUserMoved(work.Left + work.Width / 2, work.Bottom);
        AppServices.RequestSave?.Invoke();
    }

    /// <summary>把活动范围限制在工作区（任务栏之上）。</summary>
    private void ApplyBounds(Rect work)
    {
        _controller.SetBounds(
            work.Left + _anchorX,
            work.Right - _anchorX,
            work.Top + _anchorY,
            work.Bottom);
    }

    /// <summary>按窗口当前所在显示器重新计算活动范围（拖到别的屏幕后调用）。</summary>
    private void RefreshBounds() => ApplyBounds(WindowManager.GetWorkAreaDips(this));

    public void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    public (double X, double Y) GetAnchorPosition() => (Left + _anchorX, Top + _anchorY);

    // ---- 菜单 ----

    // ---- 悬停菜单 ----

    private void OnPetMouseEnter(object sender, MouseEventArgs e)
    {
        if (_pressed) return;
        _closeTimer?.Stop();
        if (!_menuOpen)
            _hoverTimer?.Start();
    }

    private void OnPetMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverTimer?.Stop();
        if (_menuOpen)
            _closeTimer?.Start();
    }

    private void OnMenuPointerInside(bool inside)
    {
        if (inside)
            _closeTimer?.Stop();
        else if (_menuOpen)
            _closeTimer?.Start();
    }

    private void ShowMenu()
    {
        if (_menuOpen) return;

        if (_menuWindow is null)
        {
            _menuWindow = new PetMenuWindow();
            _menuWindow.FoodRequested += (_, food) => Feed(food);
            _menuWindow.PetRequested += (_, _) => Pet();
            _menuWindow.OutfitRequested += (_, _) => { HideMenu(); OpenOutfit(); };
            _menuWindow.SettingsRequested += (_, _) => { HideMenu(); OpenSettings(); };
            _menuWindow.HideRequested += (_, _) => { HideMenu(); HidePet(); };
            _menuWindow.PointerInsideChanged += (_, inside) => OnMenuPointerInside(inside);
        }

        _menuWindow.ResetMode();
        PositionMenu();
        ShowStatusOverlay();

        _menuWindow.Show();
        _menuWindow.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

        _controller.Autonomous = false;
        _menuOpen = true;
        _closeTimer?.Stop();
    }

    private void PositionMenu()
    {
        if (_menuWindow is null) return;

        Rect screen = WindowManager.GetVirtualScreenDips(this);
        double menuWidth = _menuWindow.Width;
        double menuHeight = _menuWindow.Height;

        // 按可见宠物身体的边缘定位，让弧线贴近宠物而不是贴窗口边。
        double catRight = Left + Width * 0.82;
        double catLeft = Left + Width * 0.18;

        bool rightSide = catRight + menuWidth - 6 <= screen.Right;
        double x = rightSide ? catRight - 6 : catLeft - menuWidth + 6;

        double y = Top + Height / 2 - menuHeight / 2;

        if (y + menuHeight > screen.Bottom)
            y = screen.Bottom - menuHeight - 4;
        if (y < screen.Top + 4)
            y = screen.Top + 4;
        if (x < screen.Left)
            x = screen.Left + 4;

        _menuOnRight = rightSide;
        _menuWindow.SetSide(rightSide);
        _menuWindow.Left = x;
        _menuWindow.Top = y;
    }

    /// <summary>宠物移动时让悬停 UI 跟随。</summary>
    private void RepositionHover()
    {
        if (!_menuOpen) return;
        PositionMenu();
        PositionStatusOverlay();
    }

    private void HideMenu()
    {
        _menuOpen = false;
        _controller.Autonomous = true;
        _hoverTimer?.Stop();
        _closeTimer?.Stop();
        _menuWindow?.Hide();
        _statusWindow?.Hide();
    }

    private void Feed(Food food)
    {
        _controller.Feed(food);
        _sound.PlayFeed();
        AppServices.RequestSave?.Invoke();
    }

    private void Pet()
    {
        _controller.Pet();
        _sound.PlayPet();
        AppServices.RequestSave?.Invoke();
    }

    public void OpenOutfit()
    {
        if (_outfitWindow is { IsVisible: true })
        {
            _outfitWindow.Activate();
            return;
        }

        _outfitWindow = new OutfitWindow();
        _outfitWindow.Closed += (_, _) => _outfitWindow = null;
        _outfitWindow.Show();
    }

    private void ShowStatusOverlay()
    {
        if (_statusWindow is null)
        {
            _statusWindow = new StatusWindow(_session.State);
            _statusWindow.Closed += (_, _) => _statusWindow = null;
        }

        if (!_statusWindow.IsVisible)
            _statusWindow.Show();

        PositionStatusOverlay();
    }

    private void PositionStatusOverlay()
    {
        if (_statusWindow is null || !_statusWindow.IsVisible) return;

        _statusWindow.UpdateLayout();

        Rect screen = WindowManager.GetVirtualScreenDips(this);
        double w = _statusWindow.ActualWidth > 0 ? _statusWindow.ActualWidth : _statusWindow.Width;
        double h = _statusWindow.ActualHeight > 0 ? _statusWindow.ActualHeight : _statusWindow.Height;

        // 放在弧菜单的对面，菜单就不会被文字挡住。
        double catRight = Left + Width * 0.82;
        double catLeft = Left + Width * 0.18;

        double x = _menuOnRight ? catLeft - w - 6 : catRight + 6;
        double y = Top + Height / 2 - h / 2;

        if (x < screen.Left)
            x = screen.Left + 4;
        if (x + w > screen.Right)
            x = screen.Right - w - 4;
        if (y < screen.Top)
            y = screen.Top + 4;

        _statusWindow.Left = x;
        _statusWindow.Top = y;
    }

    // ---- 拖动 ----

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _dragging = false;
        _pressScreen = PointToScreen(e.GetPosition(this));
        _pressLeft = Left;
        _pressTop = Top;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;

        // 用屏幕绝对坐标计算位移，避免窗口自身移动导致相对坐标变化而形成抖动。
        Point current = PointToScreen(e.GetPosition(this));
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double dx = (current.X - _pressScreen.X) / dpi.DpiScaleX;
        double dy = (current.Y - _pressScreen.Y) / dpi.DpiScaleY;

        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) > DragThreshold)
        {
            _dragging = true;
            _controller.NotifyPickedUp();
        }

        if (_dragging)
        {
            Left = _pressLeft + dx;
            Top = _pressTop + dy;
            RepositionHover();
        }

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;

        ReleaseMouseCapture();
        _pressed = false;

        if (_dragging)
        {
            // 可能拖到了另一块显示器，先按新显示器的工作区更新活动范围。
            RefreshBounds();
            _controller.NotifyUserMoved(Left + _anchorX, Top + _anchorY);
            AppServices.RequestSave?.Invoke();
        }
        else
        {
            _controller.NotifyClicked();
            ShowMenu();
        }

        e.Handled = true;
    }

    // ---- 透明区域不拦截鼠标 ----

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;

        if (msg == WM_NCHITTEST &&
            SpriteImage.Visibility == Visibility.Visible &&
            SpriteImage.Source is BitmapSource frame)
        {
            long lp = lParam.ToInt64();
            int screenX = (short)(lp & 0xFFFF);
            int screenY = (short)((lp >> 16) & 0xFFFF);

            Point client = PointFromScreen(new Point(screenX, screenY));
            if (!IsOpaqueAt(frame, client))
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }
        }

        return IntPtr.Zero;
    }

    private bool IsOpaqueAt(BitmapSource frame, Point clientDip)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return true;
        if (frame.Format.BitsPerPixel != 32) return true;

        int px = (int)(clientDip.X / ActualWidth * frame.PixelWidth);
        int py = (int)(clientDip.Y / ActualHeight * frame.PixelHeight);
        if (px < 0 || py < 0 || px >= frame.PixelWidth || py >= frame.PixelHeight)
            return false;

        try
        {
            var pixel = new byte[4];
            frame.CopyPixels(new Int32Rect(px, py, 1, 1), pixel, 4, 0);
            return pixel[3] > 16;
        }
        catch
        {
            return true;
        }
    }
}

using System.Windows.Media.Imaging;

namespace DesktopPet.Animation;

/// <summary>
/// 动画播放器。由单一游戏循环驱动（不自己开 Timer），
/// 按 clip 的帧号顺序和时间累加推进，通过 <see cref="FrameChanged"/> 输出当前帧。
/// </summary>
public sealed class PetAnimation
{
    private readonly AnimationLibrary _library;

    private AnimationClip? _clip;
    private SpriteSheet? _sheet;
    private double _elapsed;
    private int _index;

    public event EventHandler<BitmapSource>? FrameChanged;

    public PetAnimation(AnimationLibrary library) => _library = library;

    public bool Play(string clipName)
    {
        if (!_library.TryGetClip(clipName, out var clip))
            return false;

        if (_clip is not null && _clip.Name.Equals(clip.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            _sheet = _library.GetSheet(clip.SheetKey);
        }
        catch
        {
            return false;
        }

        _clip = clip;
        _index = 0;
        _elapsed = 0;
        Emit();
        return true;
    }

    public void Tick(double dt)
    {
        if (_clip is null || _sheet is null || _clip.Frames.Length == 0 || _clip.Fps <= 0)
            return;

        double frameDuration = 1.0 / _clip.Fps;
        _elapsed += dt;

        bool changed = false;
        while (_elapsed >= frameDuration)
        {
            _elapsed -= frameDuration;
            _index++;
            changed = true;

            if (_index >= _clip.Frames.Length)
            {
                if (_clip.Loop)
                {
                    _index = 0;
                }
                else
                {
                    _index = _clip.Frames.Length - 1;
                    break;
                }
            }
        }

        if (changed) Emit();
    }

    private void Emit()
    {
        if (_clip is null || _sheet is null || _clip.Frames.Length == 0) return;
        FrameChanged?.Invoke(this, _sheet.GetFrame(_clip.Frames[_index]));
    }
}

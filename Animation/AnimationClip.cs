namespace DesktopPet.Animation;

/// <summary>
/// 一段动画的播放定义：属于哪张图集、按什么帧号顺序、帧率、是否循环。
/// 帧号 1-based，播放顺序完全由这里决定，程序不自行判断。
/// </summary>
public sealed class AnimationClip
{
    public string Name { get; }
    public string SheetKey { get; }
    public int[] Frames { get; }
    public double Fps { get; }
    public bool Loop { get; }

    public AnimationClip(string name, string sheetKey, int[] frames, double fps, bool loop)
    {
        Name = name;
        SheetKey = sheetKey;
        Frames = frames;
        Fps = fps;
        Loop = loop;
    }
}

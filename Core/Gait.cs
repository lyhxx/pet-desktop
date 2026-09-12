namespace DesktopPet.Core;

/// <summary>走动时的步态，决定移动速度与所用动画剪辑。</summary>
public enum Gait
{
    /// <summary>普通行走。</summary>
    Walk,

    /// <summary>小步快走：步子小、速度慢。</summary>
    Small,

    /// <summary>小跑：速度较快。</summary>
    Trot,

    /// <summary>快速移动：冲刺。</summary>
    Fast
}

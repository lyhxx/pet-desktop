namespace DesktopPet.Core;

/// <summary>
/// 核心逻辑与 UI 的边界。PetController / PetBehavior 只通过它驱动界面，
/// 不直接依赖任何 WPF 控件。
/// </summary>
public interface IPetView
{
    /// <summary>把宠物双脚锚点移动到虚拟桌面坐标 (DIP)。</summary>
    void MoveTo(double x, double y);

    /// <summary>朝向：1 = 朝右，-1 = 朝左（由视图水平镜像）。</summary>
    void SetFacing(int facing);

    /// <summary>请求播放某个动画，返回是否成功（资源缺失时返回 false）。</summary>
    bool PlayAnimation(string clipName);
}

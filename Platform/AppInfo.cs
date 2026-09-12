using System.Reflection;

namespace DesktopPet.Platform;

/// <summary>程序元信息与更新源配置。</summary>
public static class AppInfo
{
    /// <summary>GitHub 用户名/组织（发布仓库）。留空则跳过检查更新。</summary>
    public const string GitHubOwner = "lyhxx";

    /// <summary>GitHub 仓库名。留空则跳过检查更新。</summary>
    public const string GitHubRepo = "pet-desktop";

    public static string Version
    {
        get
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(info))
            {
                int plus = info.IndexOf('+');
                return plus >= 0 ? info[..plus] : info;
            }
            return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public static bool IsUpdateCheckConfigured =>
        !string.IsNullOrWhiteSpace(GitHubOwner) && !string.IsNullOrWhiteSpace(GitHubRepo);
}

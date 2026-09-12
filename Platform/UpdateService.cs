using System.Net.Http;

namespace DesktopPet.Platform;

public sealed record UpdateInfo(string Version, string Url);

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateResult(UpdateStatus Status, UpdateInfo? Info = null)
{
    public static readonly UpdateResult UpToDate = new(UpdateStatus.UpToDate);
    public static readonly UpdateResult Failed = new(UpdateStatus.Failed);
    public static UpdateResult Available(UpdateInfo info) => new(UpdateStatus.UpdateAvailable, info);
}

/// <summary>
/// 检查更新。读取 GitHub 网页 <c>/releases/latest</c> 的 302 跳转拿最新 tag，
/// 不使用 GitHub REST API（未登录只有 60 次/小时，容易 403 导致检查静默失败）。
/// 未配置 <see cref="AppInfo.GitHubOwner"/> / <see cref="AppInfo.GitHubRepo"/> 时不检查。
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        // 关闭自动跳转，直接读 302 的 Location。
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopPet-Updater");
        return client;
    }

    public static async Task<UpdateResult> CheckAsync()
    {
        if (!AppInfo.IsUpdateCheckConfigured)
            return UpdateResult.Failed;

        try
        {
            (string version, string url)? latest = await GetLatestReleaseAsync();
            if (latest is not { } release || string.IsNullOrEmpty(release.version))
                return UpdateResult.Failed;

            return IsNewer(release.version, AppInfo.Version)
                ? UpdateResult.Available(new UpdateInfo(release.version, release.url))
                : UpdateResult.UpToDate;
        }
        catch
        {
            return UpdateResult.Failed;
        }
    }

    /// <summary>访问 /releases/latest，从 302 跳转地址解析 tag 与发布页 URL。</summary>
    private static async Task<(string version, string url)?> GetLatestReleaseAsync()
    {
        string requestUrl =
            $"https://github.com/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/releases/latest";

        using var response = await Http.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
        Uri? location = response.Headers.Location;
        if (location is null)
            return null;

        // 形如 https://github.com/owner/repo/releases/tag/v1.1.0
        string path = location.AbsolutePath;
        int index = path.LastIndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        string rawTag = Uri.UnescapeDataString(path[(index + "/tag/".Length)..]);
        string version = rawTag.TrimStart('v', 'V');
        if (version.Length == 0)
            return null;

        return (version, $"{location.Scheme}://{location.Host}{path}");
    }

    private static bool IsNewer(string latest, string current)
    {
        int[] a = Parse(latest);
        int[] b = Parse(current);

        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length ? a[i] : 0;
            int y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private static int[] Parse(string version) =>
        version.Split('.', StringSplitOptions.RemoveEmptyEntries)
               .Select(part =>
               {
                   string digits = new(part.TakeWhile(char.IsDigit).ToArray());
                   return int.TryParse(digits, out int n) ? n : 0;
               })
               .ToArray();
}

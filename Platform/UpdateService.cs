using System.Net.Http;
using System.Text.Json;

namespace DesktopPet.Platform;

public sealed record UpdateInfo(string Version, string Url);

/// <summary>
/// 检查更新：查询 GitHub Releases 最新版本并与当前版本比较。
/// 未配置 <see cref="AppInfo.GitHubOwner"/> / <see cref="AppInfo.GitHubRepo"/> 时不检查。
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopPet-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>有比当前更新的版本时返回信息，否则返回 null。</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        if (!AppInfo.IsUpdateCheckConfigured)
            return null;

        try
        {
            string url =
                $"https://api.github.com/repos/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/releases/latest";

            using var stream = await Http.GetStreamAsync(url);
            using var doc = await JsonDocument.ParseAsync(stream);
            JsonElement root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() ?? "" : "";
            string html = root.TryGetProperty("html_url", out JsonElement h) ? h.GetString() ?? "" : "";
            string latest = tag.TrimStart('v', 'V');

            if (string.IsNullOrEmpty(latest))
                return null;

            return IsNewer(latest, AppInfo.Version) ? new UpdateInfo(latest, html) : null;
        }
        catch
        {
            return null;
        }
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

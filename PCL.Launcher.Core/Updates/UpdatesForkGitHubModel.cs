using System.Text.Json;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>CN 分支只从自己的 GitHub Releases 获取更新，不回退到上游服务。</summary>
public sealed class UpdatesForkGitHubModel : IUpdateSource
{
    public const string Repository = "noxargenta/PCL-CE-CN";
    public const string ReleasesUrl = "https://github.com/" + Repository + "/releases/latest";
    public const string ApiUrl = "https://api.github.com/repos/" + Repository + "/releases?per_page=100";

    private readonly Func<string, string> _fetch;
    private readonly object _cacheLock = new();
    private string? _releases;

    public UpdatesForkGitHubModel() : this(url => Requester.FetchString(url, RequestParam.WithRetry)) { }

    public UpdatesForkGitHubModel(Func<string, string> fetch) => _fetch = fetch;

    public string SourceName { get; set; } = "PCL CE CN GitHub";
    public bool IsAvailable() => true;

    public bool RefreshCache()
    {
        lock (_cacheLock)
        {
            var releases = _fetch(ApiUrl);
            using var document = JsonDocument.Parse(releases);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GitHub 发行列表格式无效");
            _releases = releases;
            return true;
        }
    }

    private (VersionDataModel Version, string DownloadUrl) GetRelease(UpdateSourceChannel channel)
    {
        string json;
        lock (_cacheLock)
        {
            if (_releases is null) RefreshCache();
            json = _releases!;
        }
        using var document = JsonDocument.Parse(json);
        var candidates = new List<(SemVer Order, VersionDataModel Version, string Url)>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()) continue;
            if (channel != UpdateSourceChannel.Beta && release.GetProperty("prerelease").GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var version = tag.StartsWith('v') ? tag[1..] : tag;
            if (!SemVer.TryParse(version, out var order)) continue;
            var prefix = $"https://github.com/{Repository}/releases/download/{tag}/";
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("PCL_CE_CN_", StringComparison.Ordinal) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (!url.StartsWith(prefix, StringComparison.Ordinal) || asset.GetProperty("size").GetInt64() <= 0) continue;
                var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
                candidates.Add((order!, new VersionDataModel
                {
                    VersionName = version,
                    VersionCode = 0,
                    Source = SourceName,
                    Changelog = release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                    Sha256 = digest?.StartsWith("sha256:", StringComparison.Ordinal) == true ? digest[7..] : ""
                }, url));
                break;
            }
        }
        var latest = candidates.OrderByDescending(candidate => candidate.Order).FirstOrDefault();
        if (latest.Version is null) throw new InvalidDataException("本仓库尚无可用的 Windows x64 EXE 发行版");
        return (latest.Version, latest.Url);
    }

    public VersionDataModel GetLatestVersion(UpdateSourceChannel channel, UpdateArch arch) => GetRelease(channel).Version;

    public bool IsLatest(UpdateSourceChannel channel, UpdateArch arch, SemVer currentVersion, int currentVersionCode) =>
        currentVersion >= SemVer.Parse(GetLatestVersion(channel, arch).VersionName);

    // 不接收上游公告或其中携带的操作指令。
    public VersionAnnouncementDataModel GetAnnouncementList() => new() { Content = [] };

    public List<Loading.LoaderBase> GetDownloadLoader(UpdateSourceChannel channel, UpdateArch arch, string output) =>
    [
        new Loading.LoaderTask<int, List<DownloadFile>>("获取 CN 版下载地址", load =>
            load.output = [new DownloadFile([GetRelease(channel).DownloadUrl], output)]),
        new LoaderDownload("下载 CN 版更新", [])
    ];
}

using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils;

namespace PCL.Core.Test;

[TestClass]
public class ForkGitHubUpdateTest
{
    private static object Release(string version, bool prerelease = false, bool draft = false, string? url = null) => new
    {
        tag_name = "v" + version,
        prerelease,
        draft,
        body = "CN release notes",
        assets = new[]
        {
            new
            {
                name = "PCL_CE_CN_2.15.1.exe",
                size = 100,
                browser_download_url = url ?? $"https://github.com/noxargenta/PCL-CE-CN/releases/download/v{version}/PCL_CE_CN_2.15.1.exe",
                digest = "sha256:abc123"
            }
        }
    };

    [TestMethod]
    public void ChecksOnlyForkAndSelectsStableByVersion()
    {
        var calls = 0;
        var source = new UpdatesForkGitHubModel(url =>
        {
            calls++;
            Assert.AreEqual("https://api.github.com/repos/noxargenta/PCL-CE-CN/releases?per_page=100", url);
            return JsonSerializer.Serialize(new[]
            {
                Release("2.15.1-cn.1"), Release("2.16.0-beta.1", prerelease: true),
                Release("2.17.0", draft: true), Release("2.15.1-cn.2")
            });
        });
        var stable = source.GetLatestVersion(UpdateSourceChannel.Stable, UpdateArch.X64);
        Assert.AreEqual("2.15.1-cn.2", stable.VersionName);
        Assert.AreEqual("CN release notes", stable.Changelog);
        Assert.AreEqual("abc123", stable.Sha256);
        Assert.AreEqual("2.16.0-beta.1", source.GetLatestVersion(UpdateSourceChannel.Beta, UpdateArch.X64).VersionName);
        Assert.IsFalse(source.IsLatest(UpdateSourceChannel.Stable, UpdateArch.X64, SemVer.Parse("2.15.1-cn.1"), 999));
        Assert.IsTrue(source.IsLatest(UpdateSourceChannel.Stable, UpdateArch.X64, SemVer.Parse("2.15.1-cn.2"), 0));
        Assert.AreEqual(1, calls);
    }

    [DataTestMethod]
    [DataRow("https://github.com/PCL-Community/PCL-CE/releases/download/v2.15.1-cn.2/PCL_CE_CN_2.15.1.exe")]
    [DataRow("https://github.com/noxargenta/PCL-CE-CN.evil/releases/download/v2.15.1-cn.2/PCL_CE_CN_2.15.1.exe")]
    public void RejectsExternalAssetsWithoutUpstreamFallback(string url)
    {
        var calls = 0;
        var source = new UpdatesForkGitHubModel(_ =>
        {
            calls++;
            return JsonSerializer.Serialize(new[] { Release("2.15.1-cn.2", url: url) });
        });
        Assert.ThrowsExactly<InvalidDataException>(() => source.GetLatestVersion(UpdateSourceChannel.Stable, UpdateArch.X64));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void RefreshesCachedReleaseAndDoesNotFetchAnnouncements()
    {
        var version = "2.15.1-cn.1";
        var calls = 0;
        var source = new UpdatesForkGitHubModel(_ =>
        {
            calls++;
            return JsonSerializer.Serialize(new[] { Release(version) });
        });
        Assert.HasCount(0, source.GetAnnouncementList().Content);
        Assert.AreEqual(0, calls);
        Assert.AreEqual(version, source.GetLatestVersion(UpdateSourceChannel.Stable, UpdateArch.X64).VersionName);
        version = "2.15.1-cn.2";
        source.RefreshCache();
        Assert.AreEqual(version, source.GetLatestVersion(UpdateSourceChannel.Stable, UpdateArch.X64).VersionName);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void NetworkFailureDoesNotContactAnotherSource()
    {
        var calls = 0;
        var source = new UpdatesForkGitHubModel(_ =>
        {
            calls++;
            throw new IOException("offline");
        });
        Assert.ThrowsExactly<IOException>(() => source.GetLatestVersion(UpdateSourceChannel.Stable, UpdateArch.X64));
        Assert.AreEqual(1, calls);
    }
}

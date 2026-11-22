using System;
using System.Collections.Generic;
using PCL.Core.Platform;

namespace PCL.Core.App.Region;

public enum RegionClassification
{
    Unknown,
    Other,
    Mainland
}

public sealed record RegionSignals(
    string? GeoName,
    bool GeoNameAvailable,
    string? TimeZoneId,
    bool TimeZoneAvailable);

public sealed record RegionAssessment(RegionClassification Classification, string Reason)
{
    // PCL CE CN 保留地区识别，但所有地区均可使用下载、安装与文件补全功能。
    public bool IsRestricted => false;
}

public static class RegionPolicy
{
    private static readonly HashSet<string> _ChinaTimeZones =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "China Standard Time",
        "Asia/Shanghai",
        "PRC"
    ];

    public static RegionAssessment Evaluate(RegionSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        // 仅在“时区为中国大陆”且“设备地区为中国大陆”时识别为中国大陆。
        // 设备地区取系统“国家或地区”设置，而不是 OOBE 时写入的设备安装地区。
        // 地区分类仅用于识别，不限制功能。
        var chinaTimeZone = signals is { TimeZoneAvailable: true, TimeZoneId: not null }
            && _ChinaTimeZones.Contains(signals.TimeZoneId);
        var chinaRegion = signals.GeoNameAvailable && _IsChinaGeoName(signals.GeoName);

        if (chinaTimeZone && chinaRegion)
            return new RegionAssessment(
                RegionClassification.Mainland,
                "china-time-zone-and-region");

        if (!signals.TimeZoneAvailable || !signals.GeoNameAvailable)
            return new RegionAssessment(RegionClassification.Unknown, "signals-unavailable");

        return new RegionAssessment(
            RegionClassification.Other,
            "not-china-time-zone-and-region");
    }

    public static RegionSignals ReadWindowsSignals()
    {
        return ApplicationRuntime.Current.Platform.Region.ReadSignals();
    }

    private static bool _IsChinaGeoName(string? name)
    {
        return string.Equals(name, "CN", StringComparison.OrdinalIgnoreCase) || name == "156";
    }
}

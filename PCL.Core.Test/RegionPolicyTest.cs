using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Region;

namespace PCL.Core.Test;

[TestClass]
public class RegionPolicyTest
{
    [DataTestMethod]
    [DataRow("CN", "China Standard Time")]
    [DataRow("CN", "Asia/Shanghai")]
    [DataRow("CN", "PRC")]
    [DataRow("156", "China Standard Time")]
    [DataRow("156", "Asia/Shanghai")]
    [DataRow("cn", "asia/shanghai")]
    public void ChinaTimeZoneAndChinaRegionDoNotRestrict(string geo, string timeZone)
    {
        var result = RegionPolicy.Evaluate(Signals(geo, timeZone));
        Assert.AreEqual(RegionClassification.Mainland, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void ChinaRegionWithoutChinaTimeZoneDoesNotRestrict()
    {
        var result = RegionPolicy.Evaluate(Signals("CN", "Tokyo Standard Time"));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void ChinaTimeZoneWithoutChinaRegionDoesNotRestrict()
    {
        var result = RegionPolicy.Evaluate(Signals("US", "China Standard Time"));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void OtherRegionAndTimeZoneDoNotRestrict()
    {
        var result = RegionPolicy.Evaluate(Signals("JP", "Tokyo Standard Time"));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void MissingSignalsDoNotRestrict()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, null, false));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void UnavailableRegionDoesNotRestrictEvenWithChinaTimeZone()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, "China Standard Time", true));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    private static RegionSignals Signals(string? geo, string? timeZone) =>
        new(geo, true, timeZone, true);
}

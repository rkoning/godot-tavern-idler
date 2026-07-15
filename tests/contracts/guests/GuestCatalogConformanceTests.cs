using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Guests;

/// <summary>
/// CON-005 guest-sheet catalog conformance. Abstract — the content adapter (TKT-020) subclasses
/// with its real loader. Validation runs against a `ServiceDurationTicks` so the REQ-092 patience
/// band (10–30% of the service phase) is checkable. Each invalid case below violates exactly ONE
/// rule against an otherwise-valid baseline.
/// </summary>
public abstract class GuestCatalogConformanceTests
{
    protected abstract GuestCatalog LoadCatalog(string json, int serviceDurationTicks);

    private const int Sdt = 1000;   // patience band ⇒ [100, 300]

    private static string GoldenJson([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "guests.sample.json"));

    [Fact]
    public void Golden_catalog_loads_expected_types()
    {
        var cat = LoadCatalog(GoldenJson(), Sdt);
        Assert.Contains(cat.Types, t => t.Id == new GuestTypeId("dwarf") && !t.IsVip);
        Assert.Contains(cat.Types, t => t.Id == new GuestTypeId("food-critic") && t.IsVip && t.Vip!.Conditions.Count == 2);
    }

    // Single-quote template → double quotes via Replace, so the JSON stays readable.
    private static string Type(string id = "a", int patience = 200, int blockedWait = 150,
        string crowdPref = "neutral", string crowdMag = "0.3", int walletMin = 20, int walletMax = 60,
        string vip = "null")
        => ("{'id':'" + id + "','displayName':'A','isVip':false,'spriteId':'s','baseWeight':5,"
          + "'attractors':[],'crowding':{'preference':'" + crowdPref + "','magnitude':" + crowdMag + "},"
          + "'queuePatienceTicks':" + patience + ",'blockedWaitTicks':" + blockedWait + ","
          + "'agenda':[{'serviceId':'drink','menuItem':null}],"
          + "'walletMin':" + walletMin + ",'walletMax':" + walletMax + ",'traits':[],'vip':" + vip + "}")
          .Replace("'", "\"");

    private static string Cat(params string[] types) => "{\"guestTypes\":[" + string.Join(",", types) + "]}";

    public static IEnumerable<object[]> InvalidCatalogs()
    {
        yield return new object[] { "duplicate type id", Cat(Type("a"), Type("a")) };
        yield return new object[] { "empty type id", Cat(Type(id: "")) };
        yield return new object[] { "patience below REQ-092 band", Cat(Type(patience: 50)) };     // < 10% of 1000
        yield return new object[] { "patience above REQ-092 band", Cat(Type(patience: 500)) };    // > 30% of 1000
        yield return new object[] { "blockedWait below band", Cat(Type(blockedWait: 50)) };
        yield return new object[] { "crowding magnitude > 1", Cat(Type(crowdMag: "1.5")) };
        yield return new object[] { "unknown crowding preference", Cat(Type(crowdPref: "meh")) };
        yield return new object[] { "unknown VIP condition kind",
            Cat(Type(vip: "{'visitChancePerNight':0.1,'conditions':[{'kind':'bogus','id':null,'value':null}]}".Replace("'", "\""))) };
        yield return new object[] { "walletMin > walletMax", Cat(Type(walletMin: 60, walletMax: 20)) };
    }

    [Theory]
    [MemberData(nameof(InvalidCatalogs))]
    public void Invalid_catalog_is_rejected(string reason, string json)
    {
        _ = reason;
        Assert.ThrowsAny<Exception>(() => LoadCatalog(json, Sdt));
    }
}

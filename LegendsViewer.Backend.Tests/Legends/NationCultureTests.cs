using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class NationCultureTests
{
    [TestMethod]
    public void FoundingConquestAndReclamationAreSeparate()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "Kingdom", IsCiv = true };
        Site founded = new([], world) { Id = 10, Name = "New Town" };
        Site conquered = new([], world) { Id = 11, Name = "Old Castle" };
        Site reclaimed = new([], world) { Id = 12, Name = "Returned Home" };

        _ = new OwnerPeriod(founded, nation, 10, "founded");
        _ = new OwnerPeriod(conquered, nation, 20, "took over");
        _ = new OwnerPeriod(reclaimed, nation, 30, "reclaimed");

        var culture = NationCultureProfileDto.FromEntity(nation);
        var all = culture.Periods.Single(p => p.WindowYears == 0);
        var recent = culture.Periods.Single(p => p.WindowYears == 25);

        Assert.AreEqual(1, all.SettlementsFounded);
        Assert.AreEqual(1, all.Conquests);
        Assert.AreEqual(1, all.SitesReclaimed);
        Assert.AreEqual(0, recent.Conquests);
        Assert.AreEqual(0, recent.SettlementsFounded);
        Assert.AreEqual(0, recent.SitesReclaimed);
        Assert.AreEqual(3, culture.Evidence.Count);
    }

    [TestMethod]
    public void ShortLivedNationRatesUseItsRecordedAge()
    {
        using World world = new() { CurrentYear = 130 };
        Entity nation = new([], world) { Id = 1, Name = "New Nation", IsCiv = true };
        Site town = new([], world) { Id = 10, Name = "New Town" };
        _ = new OwnerPeriod(town, nation, 120, "founded");

        var recent = NationCultureProfileDto.FromEntity(nation).Periods.Single(p => p.WindowYears == 25);

        Assert.AreEqual(120, recent.StartYear);
        Assert.AreEqual(11, recent.ObservedYears);
        Assert.AreEqual(Math.Round(100d / 11, 2), recent.ExpansionPerCentury);
    }

    [TestMethod]
    public void EnemyAttacksDoNotCountAsOwnAggression()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "Defender", IsCiv = true };
        Entity enemy = new([], world) { Id = 2, Name = "Aggressor", IsCiv = true };

        nation.Events.Add(new AttackedSite([], world)
        {
            Year = 70, Attacker = enemy, Defender = nation
        });
        nation.Events.Add(new FieldBattle([], world)
        {
            Year = 80, Attacker = nation, Defender = enemy
        });
        nation.Wars.Add(new War([], world)
        {
            Id = 40, StartYear = 70, Attacker = enemy, Defender = nation
        });

        var recent = NationCultureProfileDto.FromEntity(nation).Periods.Single(p => p.WindowYears == 100);

        Assert.AreEqual(1, recent.DefensiveOperations);
        Assert.AreEqual(1, recent.OffensiveOperations);
        Assert.AreEqual(0, recent.WarsInitiated);
        Assert.AreEqual(1, recent.WarsDefended);
    }

    [TestMethod]
    public void MissingHistoricalEventsDoNotImplyPacifism()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "Unknown", IsCiv = true };

        var culture = NationCultureProfileDto.FromEntity(nation);

        Assert.AreEqual("Insufficient recorded activity", culture.Character);
        Assert.IsFalse(culture.IntrinsicValuesAvailable);
        Assert.AreEqual(0, culture.Periods[0].OffensiveOperations);
        Assert.AreEqual(0, culture.Evidence.Count);
    }
}

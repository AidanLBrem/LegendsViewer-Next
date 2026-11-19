using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
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
        Assert.AreEqual(all.SettlementExpansionPerCentury + all.ConquestPerCentury, all.ExpansionPerCentury);
        Assert.AreEqual(Math.Round(100d / all.ObservedYears, 2), all.SettlementExpansionPerCentury);
        Assert.AreEqual(Math.Round(100d / all.ObservedYears, 2), all.ConquestPerCentury);
        Assert.AreEqual(1, all.SitesReclaimed);
        Assert.AreEqual(0, recent.Conquests);
        Assert.AreEqual(0, recent.SettlementsFounded);
        Assert.AreEqual(0, recent.SitesReclaimed);
        Assert.AreEqual(0, recent.SettlementExpansionPerCentury);
        Assert.AreEqual(0, recent.ConquestPerCentury);
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
        Assert.AreEqual(recent.ExpansionPerCentury, recent.SettlementExpansionPerCentury);
        Assert.AreEqual(0, recent.ConquestPerCentury);
    }

    [TestMethod]
    public void RecordedCivilizationCreationAnchorsObservedDuration()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "Old Nation", IsCiv = true };
        Site town = new([], world) { Id = 10, Name = "Frontier Town" };
        nation.Events.Add(new EntityCreated([], world)
        {
            Id = 4, Entity = nation, Year = 10
        });
        _ = new OwnerPeriod(town, nation, 90, "founded");

        var all = NationCultureProfileDto.FromEntity(nation).Periods.Single(p => p.WindowYears == 0);

        Assert.AreEqual(10, all.StartYear);
        Assert.AreEqual(91, all.ObservedYears);
        Assert.AreEqual(Math.Round(100d / 91, 2), all.ExpansionPerCentury);
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
    public void WartimeSettlementLossesCountOnlyAttributedCapturesAndDestruction()
    {
        using World world = new() { CurrentYear = 150 };
        Entity defender = new([], world) { Id = 1, Name = "Defender", IsCiv = true };
        Entity attacker = new([], world) { Id = 2, Name = "Attacker", IsCiv = true };
        Entity thirdParty = new([], world) { Id = 3, Name = "Outsider", IsCiv = true };

        War war = new([], world)
        {
            Id = 40, Name = "Border War", StartYear = 80,
            Attacker = attacker, Defender = defender
        };
        defender.Wars.Add(war);
        attacker.Wars.Add(war);

        Site captured = new([], world) { Id = 101, Name = "Lost Keep" };
        Site razed = new([], world) { Id = 102, Name = "Burned Town" };
        Site plundered = new([], world) { Id = 103, Name = "Raided Market" };
        Site abandoned = new([], world) { Id = 104, Name = "Abandoned Hold" };
        Site outsideWar = new([], world) { Id = 105, Name = "Unrelated Town" };
        Site thirdPartyCapture = new([], world) { Id = 106, Name = "Outsider Capture" };

        _ = new OwnerPeriod(captured, defender, 20, "founded")
        {
            EndYear = 110, EndCause = "taken over", Ender = attacker
        };
        _ = new OwnerPeriod(razed, defender, 50, "founded")
        {
            EndYear = 135, EndCause = "destroyed", Ender = attacker
        };
        _ = new OwnerPeriod(abandoned, defender, 40, "founded")
        {
            EndYear = 145, EndCause = "abandoned"
        };
        _ = new OwnerPeriod(outsideWar, defender, 55, "founded")
        {
            EndYear = 146, EndCause = "taken over", Ender = attacker
        };

        SiteConquered Outcome(int id, Site site, int year,
            SiteConqueredType type, Entity winner)
        {
            return new SiteConquered([], world)
            {
                Id = id, Site = site, StartYear = year,
                Attacker = winner, Defender = defender, ConquerType = type
            };
        }

        war.EventCollections.Add(Outcome(1, captured, 110, SiteConqueredType.Conquest, attacker));
        war.EventCollections.Add(Outcome(2, captured, 110, SiteConqueredType.Conquest, attacker));
        war.EventCollections.Add(Outcome(3, razed, 135, SiteConqueredType.Destruction, attacker));
        war.EventCollections.Add(Outcome(4, plundered, 140, SiteConqueredType.Pillaging, attacker));
        war.EventCollections.Add(Outcome(5, thirdPartyCapture, 143, SiteConqueredType.Conquest, thirdParty));

        var defenderProfile = NationCultureProfileDto.FromEntity(defender);
        var all = defenderProfile.Periods.Single(p => p.WindowYears == 0);
        var last25 = defenderProfile.Periods.Single(p => p.WindowYears == 25);

        Assert.AreEqual(2, all.SitesLostInWar);
        Assert.AreEqual(1, last25.SitesLostInWar);
        Assert.AreEqual(Math.Round(200d / all.ObservedYears, 2), all.SitesLostInWarPerCentury);
        Assert.AreEqual(Math.Round(100d / last25.ObservedYears, 2), last25.SitesLostInWarPerCentury);
        Assert.AreEqual(2, defenderProfile.Evidence.Count(e => e.Category == "warLoss"));
        Assert.AreEqual(0, NationCultureProfileDto.FromEntity(attacker).Periods[0].SitesLostInWar);
    }

    [TestMethod]
    public void NonWarTerritorialLossesDoNotCountAsWartimeLosses()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "Realm", IsCiv = true };
        Site abandoned = new([], world) { Id = 10, Name = "Old Town" };
        _ = new OwnerPeriod(abandoned, nation, 10, "founded")
        {
            EndYear = 90, EndCause = "abandoned"
        };

        var profile = NationCultureProfileDto.FromEntity(nation);
        var all = profile.Periods.Single(p => p.WindowYears == 0);

        Assert.AreEqual(1, all.SitesLost);
        Assert.AreEqual(0, all.SitesLostInWar);
        Assert.AreEqual(0, all.SitesLostInWarPerCentury);
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

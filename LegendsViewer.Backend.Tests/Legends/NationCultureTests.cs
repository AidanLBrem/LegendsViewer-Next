using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class NationCultureTests
{
    [TestMethod]
    public void LawHistoryPreservesRepealsAndOnlyIncludesTheNamedGovernment()
    {
        using World world = new();
        Entity nation = new([], world) { Id = 1, IsCiv = true };
        Entity local = new([], world) { Id = 2, Parent = nation };
        nation.Events.Add(new EntityLaw([], world) { Id = 5, Year = 10, Entity = nation, Law = EntityLawType.Harsh, LawLaid = true });
        nation.Events.Add(new EntityLaw([], world) { Id = 9, Year = 10, Entity = nation, Law = EntityLawType.Harsh, LawLaid = false });
        nation.Events.Add(new EntityLaw([], world) { Id = 14, Year = 11, Entity = local, Law = EntityLawType.Harsh, LawLaid = true });

        var history = NationCultureProfileDto.FromEntity(nation).LawHistory;
        CollectionAssert.AreEqual(new[] { 9, 5 }, history.Select(change => change.Id).ToArray());
        Assert.IsFalse(history[0].Enacted);
        Assert.AreEqual("Harsh edicts", history[0].Law);
    }

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
    public void SurrenderedSiteCountsAsConquestWithoutAnyNationOwnershipPeriod()
    {
        using World world = new() { CurrentYear = 100 };
        Entity winner = new([], world) { Id = 1, Name = "Winning Realm", IsCiv = true };
        Entity loser = new([], world) { Id = 2, Name = "Losing Realm", IsCiv = true };
        Site site = new([], world) { Id = 1, Name = "Captured Village" };
        world.Entities.AddRange([winner, loser]);
        world.Sites.Add(site);

        // SiteSurrendered records a capture, but does not create an OwnerPeriod.
        // The former culture implementation therefore reported zero conquests.
        var surrender = new SiteSurrendered(
        [
            new Property { Name = "attacker_civ_id", Value = "1" },
            new Property { Name = "defender_civ_id", Value = "2" },
            new Property { Name = "site_civ_id", Value = "2" },
            new Property { Name = "site_id", Value = "1" }
        ], world)
        {
            Id = 10,
            Year = 60
        };
        world.Events.Add(surrender);

        Assert.AreEqual(0, winner.SiteHistory.Count);
        var profile = NationCultureProfileDto.FromEntity(winner);
        var all = profile.Periods.Single(p => p.WindowYears == 0);
        Assert.AreEqual(1, all.Conquests);
        Assert.AreEqual(0, all.SettlementsFounded);
        Assert.AreEqual(1, profile.Evidence.Count(e => e.Category == "conquest"));
        Assert.AreEqual(0, NationCultureProfileDto.FromEntity(loser).Periods[0].Conquests);
    }

    [TestMethod]
    public void ConquestCollectionAndOwnershipPeriodCountOnceAndExcludeNonConquests()
    {
        using World world = new() { CurrentYear = 100 };
        Entity winner = new([], world) { Id = 1, Name = "Winning Realm", IsCiv = true };
        Entity loser = new([], world) { Id = 2, Name = "Losing Realm", IsCiv = true };
        Entity localGovernment = new([], world) { Id = 3, Name = "Local Government" };
        localGovernment.SetParent(winner);
        Site captured = new([], world) { Id = 1, Name = "Old Castle" };
        Site raided = new([], world) { Id = 2, Name = "Trade Village" };
        Site internalSite = new([], world) { Id = 3, Name = "Internal Town" };

        _ = new OwnerPeriod(captured, winner, 60, "took over");
        world.EventCollections.AddRange(
        [
            new SiteConquered([], world)
            {
                Id = 11, Site = captured, StartYear = 60,
                Attacker = winner, Defender = loser, ConquerType = SiteConqueredType.Conquest
            },
            new SiteConquered([], world)
            {
                Id = 12, Site = raided, StartYear = 70,
                Attacker = winner, Defender = loser, ConquerType = SiteConqueredType.Pillaging
            },
            new SiteConquered([], world)
            {
                Id = 13, Site = raided, StartYear = 80,
                Attacker = winner, Defender = loser, ConquerType = SiteConqueredType.Invasion
            },
            new SiteConquered([], world)
            {
                Id = 14, Site = internalSite, StartYear = 90,
                Attacker = winner, Defender = localGovernment, ConquerType = SiteConqueredType.Conquest
            }
        ]);

        var profile = NationCultureProfileDto.FromEntity(winner);
        var all = profile.Periods.Single(p => p.WindowYears == 0);
        Assert.AreEqual(1, all.Conquests);
        Assert.AreEqual(1, profile.Evidence.Count(e => e.Category == "conquest"));
        Assert.AreEqual(0, profile.Periods.Single(p => p.WindowYears == 25).Conquests);
        Assert.AreEqual(1, profile.Timeline.Sum(p => p.Conquests));
    }

    [TestMethod]
    public void SiteOwnerHistoryIsRecoveredWhenLateParentLinkSkippedNationalHistory()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "New Overlords", IsCiv = true };
        Entity enemy = new([], world) { Id = 2, Name = "Previous Owners", IsCiv = true };
        Entity oldGovernment = new([], world) { Id = 3, Name = "Old Government" };
        Entity newGovernment = new([], world) { Id = 4, Name = "New Government" };
        Site site = new([], world) { Id = 1, Name = "Conquered Keep" };
        oldGovernment.SetParent(enemy);
        site.OwnerHistory.Add(new OwnerPeriod(site, oldGovernment, 10, "founded"));
        site.OwnerHistory.Add(new OwnerPeriod(site, newGovernment, 40, "took over"));
        newGovernment.SetParent(nation); // Parent linkage came after the ownership period.
        world.Sites.Add(site);

        Assert.AreEqual(0, nation.SiteHistory.Count);
        var culture = NationCultureProfileDto.FromEntity(nation);
        Assert.AreEqual(1, culture.Periods.Single(p => p.WindowYears == 0).Conquests);
        Assert.AreEqual(0, NationCultureProfileDto.FromEntity(enemy).Periods[0].Conquests);
    }

    [TestMethod]
    public void ConquestIsAttributedToCivilizationWhenSiteGovernmentHistoryWasNotPropagated()
    {
        using World world = new() { CurrentYear = 100 };
        Entity nation = new([], world) { Id = 1, Name = "Realm", IsCiv = true };
        Entity enemy = new([], world) { Id = 2, Name = "Enemy", IsCiv = true };
        Entity newGovernment = new([], world) { Id = 3, Name = "New Administration" };
        Site site = new([], world) { Id = 1, Name = "Fortress" };

        // A site ownership period may be created before the subsidiary's
        // parent link exists; AddOwnedSite does not retroactively propagate it.
        _ = new OwnerPeriod(site, newGovernment, 40, "took over");
        newGovernment.SetParent(nation);
        Assert.AreEqual(0, nation.SiteHistory.Count);
        world.EventCollections.Add(new SiteConquered([], world)
        {
            Id = 11, Site = site, StartYear = 40,
            Attacker = newGovernment, Defender = enemy,
            ConquerType = SiteConqueredType.Conquest
        });

        var culture = NationCultureProfileDto.FromEntity(nation);
        Assert.AreEqual(1, culture.Periods.Single(p => p.WindowYears == 0).Conquests);
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

using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class WarfareSiteOutcomesTests
{
    private static Property P(string name, string value) => new() { Name = name, Value = value };

    private static (World World, War War, Entity Attacker, Entity Defender) CreateWar()
    {
        World world = new();
        Entity attacker = new([], world) { Id = 1, Name = "Northern Realm", IsCiv = true };
        Entity defender = new([], world) { Id = 2, Name = "Southern Realm", IsCiv = true };
        world.Entities.AddRange([attacker, defender]);

        War war = new([
            P("id", "10"),
            P("name", "War of the Realms"),
            P("aggressor_ent_id", "1"),
            P("defender_ent_id", "2")
        ], world);
        world.EventCollections.Add(war);
        return (world, war, attacker, defender);
    }

    private static SiteConquered AddOutcome(
        World world, War war, int id, Entity actor, Entity victim,
        Site site, SiteConqueredType outcome)
    {
        var record = new SiteConquered([
            P("id", id.ToString()),
            P("attacking_enid", actor.Id.ToString()),
            P("defending_enid", victim.Id.ToString()),
            P("war_eventcol", war.Id.ToString())
        ], world)
        {
            Site = site,
            ConquerType = outcome
        };
        war.EventCollections.Add(record);
        return record;
    }

    [TestMethod]
    public void WarOutcomes_DistinguishConquestDestructionAndPillaging()
    {
        var setup = CreateWar();
        using World world = setup.World;
        War war = setup.War;
        var north = setup.Attacker;
        var south = setup.Defender;

        Site conquered = new([], world) { Id = 101, Name = "Conquered Town" };
        Site destroyed = new([], world) { Id = 102, Name = "Destroyed Fort" };
        Site plundered = new([], world) { Id = 103, Name = "Plundered Hamlet" };
        Site counterattack = new([], world) { Id = 104, Name = "Retaken City" };

        AddOutcome(world, war, 1, north, south, conquered, SiteConqueredType.Conquest);
        AddOutcome(world, war, 2, north, south, destroyed, SiteConqueredType.Destruction);
        AddOutcome(world, war, 3, north, south, plundered, SiteConqueredType.Pillaging);
        AddOutcome(world, war, 4, south, north, counterattack, SiteConqueredType.Conquest);
        // Multiple plunder records at the same site count as one unique site.
        AddOutcome(world, war, 5, north, south, plundered, SiteConqueredType.Pillaging);

        var outcomes = war.SiteOutcomes;
        Assert.AreEqual(1, outcomes.AttackerGained.Count);
        Assert.AreEqual(1, outcomes.AttackerLost.Count);
        Assert.AreEqual(1, outcomes.DefenderGained.Count);
        Assert.AreEqual(2, outcomes.DefenderLost.Count);
        Assert.AreEqual(1, outcomes.AttackerPlundered.Count);
        Assert.AreEqual(0, outcomes.DefenderPlundered.Count);
        Assert.IsTrue(outcomes.AttackerGained[0].Contains("Conquered Town"));
        Assert.IsTrue(outcomes.DefenderLost.Any(s => s.Contains("Destroyed Fort")));
        Assert.IsFalse(outcomes.AttackerGained.Any(s => s.Contains("Destroyed Fort")));

        // The paginated overview DTO must surface the same counts as the
        // detail page, rather than dropping them from the table response.
        WorldObjectDto overview = new(war);
        Assert.AreEqual(1, overview.AttackerSitesGainedCount);
        Assert.AreEqual(1, overview.AttackerSitesLostCount);
        Assert.AreEqual(1, overview.AttackerSitesPlunderedCount);
        Assert.AreEqual(1, overview.DefenderSitesGainedCount);
        Assert.AreEqual(2, overview.DefenderSitesLostCount);
        Assert.AreEqual(0, overview.DefenderSitesPlunderedCount);

        // Sorting is applied to the full domain collection before pagination.
        War quietWar = new([
            P("id", "11"), P("name", "Peaceful War"),
            P("aggressor_ent_id", "1"), P("defender_ent_id", "2")
        ], world);
        var ranked = new[] { quietWar, war }
            .SortByProperty("attackerSitesGainedCount", "desc").ToList();
        Assert.AreSame(war, ranked[0]);
        Assert.AreSame(quietWar, ranked[1]);
    }

    [TestMethod]
    public void BattleOutcomes_UseDirectConquestLinkAndDoNotCountUnrelatedWarEvents()
    {
        var setup = CreateWar();
        using World world = setup.World;
        War war = setup.War;

        Site captured = new([], world) { Id = 200, Name = "Captured Citadel" };
        Site elsewhere = new([], world) { Id = 201, Name = "Elsewhere" };
        SiteConquered associated = AddOutcome(
            world, war, 201, setup.Attacker, setup.Defender, captured, SiteConqueredType.Conquest);
        AddOutcome(world, war, 202, setup.Attacker, setup.Defender, elsewhere, SiteConqueredType.Pillaging);

        Battle battle = new([P("id", "20"), P("war_eventcol", "10")], world)
        {
            Conquering = associated
        };
        associated.Battle = battle;
        war.EventCollections.Add(battle);

        var outcomes = battle.SiteOutcomes;
        Assert.AreEqual(1, outcomes.AttackerGained.Count);
        Assert.AreEqual(1, outcomes.DefenderLost.Count);
        Assert.AreEqual(0, outcomes.AttackerPlundered.Count);
        Assert.AreEqual(0, outcomes.DefenderGained.Count);

        WorldObjectDto overview = new(battle);
        Assert.AreEqual(1, overview.AttackerSitesGainedCount);
        Assert.AreEqual(1, overview.DefenderSitesLostCount);
        Assert.AreEqual(0, overview.AttackerSitesPlunderedCount);

        Battle quietBattle = new([P("id", "21"), P("war_eventcol", "10")], world);
        var ranked = new[] { quietBattle, battle }
            .SortByProperty("defenderSitesLostCount", "desc").ToList();
        Assert.AreSame(battle, ranked[0]);
        Assert.AreSame(quietBattle, ranked[1]);
    }

    [TestMethod]
    public void WarOutcomes_FindDirectAndNestedTerritorialEventsWithoutConquestCollections()
    {
        var setup = CreateWar();
        using World world = setup.World;
        War war = setup.War;

        Site captured = new([], world) { Id = 501, Name = "Taken Town" };
        Site razed = new([], world) { Id = 502, Name = "Burned Fortress" };
        Site surrendered = new([], world) { Id = 503, Name = "Surrendered City" };
        Site plundered = new([], world) { Id = 504, Name = "Pillaged Hamlet" };
        Site abandoned = new([], world) { Id = 505, Name = "Abandoned Hold" };
        Site reclaimed = new([], world) { Id = 506, Name = "Reclaimed Keep" };

        // These are linked to the war, but none is a SiteConquered collection.
        war.Events.Add(new SiteTakenOver([], world)
        {
            Site = captured, Attacker = setup.Attacker, Defender = setup.Defender
        });
        war.Events.Add(new DestroyedSite([], world)
        {
            Site = razed, Attacker = setup.Attacker, Defender = setup.Defender
        });
        war.Events.Add(new SiteSurrendered([], world)
        {
            Site = surrendered, Attacker = setup.Attacker, Defender = setup.Defender
        });
        war.Events.Add(new PlunderedSite([], world)
        {
            Site = plundered, Attacker = setup.Attacker, Defender = setup.Defender
        });
        war.Events.Add(new SiteAbandoned([], world)
        {
            Site = abandoned, Civ = setup.Defender
        });

        Battle nestedBattle = new([P("id", "560")], world)
        {
            Attacker = setup.Defender, Defender = setup.Attacker
        };
        nestedBattle.Events.Add(new ReclaimSite([], world)
        {
            Site = reclaimed, Civ = setup.Defender
        });
        war.EventCollections.Add(nestedBattle);

        WarfareSiteOutcomesDto outcomes = war.SiteOutcomes;
        Assert.AreEqual(2, outcomes.AttackerGained.Count);
        Assert.AreEqual(1, outcomes.DefenderGained.Count);
        Assert.AreEqual(4, outcomes.DefenderLost.Count);
        Assert.AreEqual(1, outcomes.AttackerPlundered.Count);
        Assert.IsTrue(outcomes.AttackerGained.Any(link => link.Contains("Taken Town")));
        Assert.IsTrue(outcomes.AttackerGained.Any(link => link.Contains("Surrendered City")));
        Assert.IsTrue(outcomes.DefenderLost.Any(link => link.Contains("Abandoned Hold")));
        Assert.IsFalse(outcomes.AttackerGained.Any(link => link.Contains("Burned Fortress")));

        WorldObjectDto overview = new(war);
        Assert.AreEqual(2, overview.AttackerSitesGainedCount);
        Assert.AreEqual(4, overview.DefenderSitesLostCount);

        // The battle sees only its own reclamation, not unrelated war events.
        // Battle roles reverse relative to the war: its attacker is the
        // war's defender, so battle-side labels must follow the battle.
        Assert.AreEqual(1, nestedBattle.SiteOutcomes.AttackerGained.Count);
        Assert.AreEqual(0, nestedBattle.SiteOutcomes.DefenderGained.Count);
    }

    [TestMethod]
    public void WarOutcomes_ResolveSiteFromChildEventWhenCollectionSiteIsMissing()
    {
        var setup = CreateWar();
        using World world = setup.World;
        Site town = new([], world) { Id = 601, Name = "Hidden Collection Site" };

        SiteConquered conquest = new([P("id", "620")], world)
        {
            Attacker = setup.Attacker,
            Defender = setup.Defender,
            ConquerType = SiteConqueredType.Conquest,
            Site = null
        };
        // The event gives the site ID, but is not a proof of victory by itself.
        conquest.Events.Add(new AttackedSite([], world)
        {
            Site = town, Attacker = setup.Attacker, Defender = setup.Defender
        });
        setup.War.EventCollections.Add(conquest);

        var outcomes = setup.War.SiteOutcomes;
        Assert.AreEqual(1, outcomes.AttackerGained.Count);
        Assert.AreEqual(1, outcomes.DefenderLost.Count);
        Assert.IsTrue(outcomes.AttackerGained.Single().Contains("Hidden Collection Site"));
    }

    [TestMethod]
    public void WarOutcomes_IncludeWorldLinkedRecordsAbsentFromParentLists()
    {
        var setup = CreateWar();
        using World world = setup.World;
        Site town = new([], world) { Id = 701, Name = "Parent Linked Town" };
        Site city = new([], world) { Id = 702, Name = "Parent Linked City" };

        // The collection and event have war parent references but are not in
        // war.EventCollections or war.Events.
        var conquest = new([P("id", "703")], world)
        {
            ParentCollection = setup.War,
            Site = town,
            Attacker = setup.Attacker,
            Defender = setup.Defender,
            ConquerType = SiteConqueredType.Conquest
        };
        world.EventCollections.Add(conquest);
        var takeover = new SiteTakenOver([], world)
        {
            ParentCollection = setup.War,
            Site = city,
            Attacker = setup.Attacker,
            Defender = setup.Defender
        };
        world.Events.Add(takeover);

        var outcomes = setup.War.SiteOutcomes;
        Assert.AreEqual(2, outcomes.AttackerGained.Count);
        Assert.AreEqual(2, outcomes.DefenderLost.Count);
    }

    [TestMethod]
    public void WarOutcomes_DeduplicateSiteWhenCollectionAndEventDescribeSameCapture()
    {
        var setup = CreateWar();
        using World world = setup.World;
        Site town = new([], world) { Id = 801, Name = "Twice Described Town" };
        var conquest = AddOutcome(world, setup.War, 820,
            setup.Attacker, setup.Defender, town, SiteConqueredType.Conquest);
        conquest.Events.Add(new SiteTakenOver([], world)
        {
            Site = town, Attacker = setup.Attacker, Defender = setup.Defender
        });
        setup.War.Events.Add(new SiteTakenOver([], world)
        {
            Site = town, Attacker = setup.Attacker, Defender = setup.Defender
        });

        var outcomes = setup.War.SiteOutcomes;
        Assert.AreEqual(1, outcomes.AttackerGained.Count);
        Assert.AreEqual(1, outcomes.DefenderLost.Count);
        Assert.AreEqual(0, outcomes.Unattributed.Count);
    }

    [TestMethod]
    public void WarOutcomes_PreserveUnattributedSiteActionsWithoutClaimingWarVictories()
    {
        var setup = CreateWar();
        using World world = setup.World;
        Entity mercenaries = new([], world) { Id = 900, Name = "Neutral Mercenaries" };
        Site taken = new([], world) { Id = 901, Name = "Third Party Victory" };
        var eventRecord = new SiteTakenOver([], world)
        {
            Site = taken, Attacker = mercenaries, Defender = setup.Defender
        };
        setup.War.Events.Add(eventRecord);

        var outcomes = setup.War.SiteOutcomes;
        Assert.AreEqual(0, outcomes.AttackerGained.Count);
        Assert.AreEqual(0, outcomes.DefenderLost.Count);
        Assert.AreEqual(1, outcomes.Unattributed.Count);
        Assert.IsTrue(outcomes.Unattributed.Single().Contains("Third Party Victory"));
    }

    [TestMethod]
    public void CivilWarSides_SharingParentCivilization_UseExactSideIdentity()
    {
        using World world = new();
        Entity parent = new([], world) { Id = 1, Name = "Empire", IsCiv = true };
        Entity rebels = new([], world) { Id = 2, Name = "Rebels", Parent = parent };
        Entity loyalists = new([], world) { Id = 3, Name = "Loyalists", Parent = parent };
        Site town = new([], world) { Id = 4, Name = "Disputed Town" };
        world.Entities.AddRange([parent, rebels, loyalists]);

        SiteConquered capture = new([P("id", "5")], world)
        {
            Attacker = rebels,
            Defender = loyalists,
            Site = town,
            ConquerType = SiteConqueredType.Conquest
        };

        WarfareSiteOutcomesDto outcomes = WarfareSiteOutcomesDto.FromConquerings(
            [capture], rebels, loyalists);
        Assert.AreEqual(1, outcomes.AttackerGained.Count);
        Assert.AreEqual(1, outcomes.DefenderLost.Count);
    }

    [TestMethod]
    public void ThirdPartyConquest_IsNotMisattributedToWarSide()
    {
        var setup = CreateWar();
        using World world = setup.World;
        Entity thirdParty = new([], world) { Id = 3, Name = "Unaligned Mercenaries" };
        world.Entities.Add(thirdParty);
        Site town = new([], world) { Id = 400, Name = "Mercenary Capture" };
        SiteConquered capture = new([P("id", "401")], world)
        {
            Attacker = thirdParty,
            Defender = setup.Defender,
            Site = town,
            ConquerType = SiteConqueredType.Conquest
        };

        var outcomes = WarfareSiteOutcomesDto.FromConquerings(
            [capture], setup.Attacker, setup.Defender);
        Assert.AreEqual(0, outcomes.AttackerGained.Count);
        Assert.AreEqual(0, outcomes.DefenderLost.Count);
    }

    [TestMethod]
    public void UnattributedConquest_IsNotSilentlyCreditedToEitherSide()
    {
        var setup = CreateWar();
        using World world = setup.World;
        Site site = new([], world) { Id = 301, Name = "Unattributed Site" };
        SiteConquered conquest = new([P("id", "400")], world)
        {
            Site = site,
            ConquerType = SiteConqueredType.Conquest
        };

        WarfareSiteOutcomesDto outcomes = WarfareSiteOutcomesDto.FromConquerings(
            [conquest], setup.Attacker, setup.Defender);
        Assert.AreEqual(0, outcomes.AttackerGained.Count);
        Assert.AreEqual(0, outcomes.DefenderLost.Count);
        Assert.AreEqual(0, outcomes.DefenderGained.Count);
        Assert.AreEqual(0, outcomes.AttackerLost.Count);
    }
}

using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
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

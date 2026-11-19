using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class BattleCasualtyTests
{
    private static Property P(string name, string value) => new() { Name = name, Value = value };

    private static War CreateWar(World world)
    {
        world.Entities.Add(new Entity([], world) { Id = 1, Name = "North", IsCiv = true });
        world.Entities.Add(new Entity([], world) { Id = 2, Name = "South", IsCiv = true });

        var war = new War([
            P("id", "10"),
            P("name", "War of North and South"),
            P("aggressor_ent_id", "1"),
            P("defender_ent_id", "2")
        ], world);
        world.EventCollections.Add(war);
        return war;
    }

    [TestMethod]
    public void BattleWithLargeSquads_UsesReportedCountsWithoutCappingAtOneHundred()
    {
        using var world = new World();
        var war = CreateWar(world);
        var battle = new Battle([
            P("id", "11"),
            P("war_eventcol", "10"),
            P("outcome", "attacker won"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "250"),
            P("attacking_squad_deaths", "120"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "40"),
            P("defending_squad_deaths", "8")
        ], world);
        war.EventCollections.Add(battle);

        Assert.AreEqual(250, battle.AttackerCount);
        Assert.AreEqual(40, battle.DefenderCount);
        Assert.AreEqual(120, battle.AttackerDeathCount);
        Assert.AreEqual(8, battle.DefenderDeathCount);
        Assert.AreEqual(130, battle.AttackersRemainingCount);
        Assert.AreEqual(32, battle.DefendersRemainingCount);
        Assert.AreEqual(128, battle.DeathCount);
        Assert.AreEqual(128, battle.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(128, war.DeathCount);
        Assert.AreEqual(war.AttackerDeathCount + war.DefenderDeathCount, war.DeathCount);
        Assert.AreEqual(128, war.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(1, war.AttackerBattleVictories.Count);
        Assert.AreEqual(0, war.DefenderBattleVictories.Count);
    }

    [TestMethod]
    public void MissingOptionalSquadFields_DoNotCrashAndDoNotLoseReportedDeaths()
    {
        using var world = new World();
        var war = CreateWar(world);
        var battle = new Battle([
            P("id", "12"),
            P("war_eventcol", "10"),
            P("outcome", "defender won"),
            P("attacking_squad_race", "GOBLIN"),
            P("attacking_squad_deaths", "7"),
            P("defending_squad_race", "HUMAN"),
            P("defending_squad_number", "20"),
            P("defending_squad_deaths", "3")
        ], world);
        war.EventCollections.Add(battle);

        Assert.AreEqual(7, battle.AttackerCount);
        Assert.AreEqual(7, battle.AttackerDeathCount);
        Assert.AreEqual(0, battle.AttackersRemainingCount);
        Assert.AreEqual(-1, battle.AttackerSquads.Single().Site);
        Assert.AreEqual(-1, battle.AttackerSquads.Single().Population);
        Assert.AreEqual(10, battle.DeathCount);
        Assert.AreEqual(10, battle.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(10, war.DeathCount);
        Assert.AreEqual(0, war.AttackerBattleVictories.Count);
        Assert.AreEqual(1, war.DefenderBattleVictories.Count);
    }

    [TestMethod]
    public void UnknownBattleOutcome_IsNotRecordedAsVictoryForEitherSide()
    {
        using var world = new World();
        var war = CreateWar(world);
        var battle = new Battle([
            P("id", "13"),
            P("war_eventcol", "10"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "30"),
            P("attacking_squad_deaths", "5")
        ], world);
        war.EventCollections.Add(battle);

        Assert.IsNull(battle.Victor);
        Assert.AreEqual(0, war.AttackerBattleVictories.Count);
        Assert.AreEqual(0, war.DefenderBattleVictories.Count);
        Assert.AreEqual(0, war.AttackerVictories.Count);
        Assert.AreEqual(0, war.DefenderVictories.Count);
        Assert.AreEqual(5, war.DeathCount);
    }

    [TestMethod]
    public void NegativeSquadValues_NeverProduceNegativeCasualtiesOrSurvivors()
    {
        using var world = new World();
        var battle = new Battle([
            P("id", "14"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "-5"),
            P("attacking_squad_deaths", "-3"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "1"),
            P("defending_squad_deaths", "3")
        ], world);

        Assert.AreEqual(0, battle.AttackerCount);
        Assert.AreEqual(0, battle.AttackerDeathCount);
        Assert.AreEqual(3, battle.DefenderCount);
        Assert.AreEqual(3, battle.DefenderDeathCount);
        Assert.AreEqual(0, battle.DefendersRemainingCount);
        Assert.AreEqual(3, battle.DeathsByRace.Datasets.Single().Data.Sum());
    }

    [TestMethod]
    public void FieldBattle_WithAbsentOptionalPartiesOrRegion_DoesNotThrow()
    {
        using var world = new World();
        var fieldBattle = new FieldBattle([P("id", "20")], world);
        Assert.IsTrue(fieldBattle.Print(false).Contains("an unknown region"));
    }
}

using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Extensions;
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
        Assert.AreEqual(250, battle.AttackerInitialArmyByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(40, battle.DefenderInitialArmyByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(250, battle.AttackerTroopCount);
        Assert.AreEqual(40, battle.DefenderTroopCount);
        Assert.AreEqual(120, battle.AttackerDeathCount);
        Assert.AreEqual(8, battle.DefenderDeathCount);
        Assert.AreEqual(130, battle.AttackersRemainingCount);
        Assert.AreEqual(32, battle.DefendersRemainingCount);
        Assert.AreEqual(128, battle.DeathCount);
        Assert.AreEqual(128, battle.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(120, battle.AttackerDeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(8, battle.DefenderDeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(battle.DeathCount,
            battle.AttackerDeathsByRace.Datasets.Single().Data.Sum() +
            battle.DefenderDeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(128, war.DeathCount);
        Assert.AreEqual(war.AttackerDeathCount + war.DefenderDeathCount, war.DeathCount);
        Assert.AreEqual(128, war.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(1, war.AttackerBattleVictories.Count);
        Assert.AreEqual(0, war.DefenderBattleVictories.Count);
    }

    [TestMethod]
    public void OverviewSortsBattleCasualtiesNumericallyBeforePagination()
    {
        using var world = new World();
        CreateWar(world);
        var lowerLosses = new Battle([
            P("id", "101"),
            P("war_eventcol", "10"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "30"),
            P("attacking_squad_deaths", "5")
        ], world);
        var higherLosses = new Battle([
            P("id", "102"),
            P("war_eventcol", "10"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "200"),
            P("attacking_squad_deaths", "100"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "100"),
            P("defending_squad_deaths", "20")
        ], world);

        var result = new[] { lowerLosses, higherLosses }
            .SortByProperty("deathCount", "desc")
            .Skip(0).Take(1).Single();
        Assert.AreSame(higherLosses, result);
        Assert.AreSame(lowerLosses,
            new[] { higherLosses, lowerLosses }.SortByProperty("attackerDeathCount", "asc").First());

        WorldObjectDto overview = new(higherLosses);
        Assert.AreEqual(120, overview.DeathCount);
        Assert.AreEqual(100, overview.AttackerDeathCount);
        Assert.AreEqual(20, overview.DefenderDeathCount);
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
        Assert.AreEqual(7, battle.AttackerInitialArmyByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(20, battle.DefenderInitialArmyByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(7, battle.AttackerDeathCount);
        Assert.AreEqual(0, battle.AttackersRemainingCount);
        Assert.AreEqual(-1, battle.AttackerSquads.Single().Site);
        Assert.AreEqual(-1, battle.AttackerSquads.Single().Population);
        Assert.AreEqual(10, battle.DeathCount);
        Assert.AreEqual(10, battle.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(7, battle.AttackerDeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(3, battle.DefenderDeathsByRace.Datasets.Single().Data.Sum());
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
    public void InitialArmyCharts_AggregateMultipleSquadsByRaceWithoutMixingSides()
    {
        using var world = new World();
        Battle battle = new([
            P("id", "101"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "50"),
            P("attacking_squad_deaths", "20"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "30"),
            P("attacking_squad_deaths", "10"),
            P("attacking_squad_race", "GOBLIN"),
            P("attacking_squad_number", "25"),
            P("attacking_squad_deaths", "5"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "70"),
            P("defending_squad_deaths", "7")
        ], world);

        Assert.AreEqual(105, battle.AttackerCount);
        Assert.AreEqual(70, battle.DefenderCount);
        Assert.AreEqual(2, battle.AttackerInitialArmyByRace.Labels.Count);
        Assert.AreEqual(105, battle.AttackerInitialArmyByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(80, battle.AttackerInitialArmyByRace.Datasets.Single().Data.Max());
        Assert.AreEqual(70, battle.DefenderInitialArmyByRace.Datasets.Single().Data.Single());
        Assert.AreEqual(35, battle.AttackerDeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(7, battle.DefenderDeathsByRace.Datasets.Single().Data.Sum());
    }

    [TestMethod]
    public void AnimatedSquads_AreSeparateFromLivingCreaturesInBattleAndWarCharts()
    {
        using var world = new World();
        War war = CreateWar(world);
        Battle battle = new([
            P("id", "150"),
            P("war_eventcol", "10"),
            P("attacking_squad_race", "DWARF"),
            P("attacking_squad_number", "30"),
            P("attacking_squad_deaths", "5"),
            P("attacking_squad_race", "DWARF"),
            P("attacking_squad_animated", "true"),
            P("attacking_squad_number", "40"),
            P("attacking_squad_deaths", "12"),
            P("attacking_squad_race", "GOBLIN"),
            P("attacking_squad_number", "10"),
            P("attacking_squad_animated", "true"),
            P("attacking_squad_deaths", "3"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "20"),
            P("defending_squad_animated", "true"),
            P("defending_squad_deaths", "7"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "10"),
            P("defending_squad_deaths", "2")
        ], world);
        war.EventCollections.Add(battle);

        string dwarf = world.GetCreatureInfo("DWARF").NamePlural;
        string goblin = world.GetCreatureInfo("GOBLIN").NamePlural;

        Assert.IsFalse(battle.AttackerSquads[0].Animated);
        Assert.IsTrue(battle.AttackerSquads[1].Animated);
        Assert.IsTrue(battle.AttackerSquads[2].Animated);
        Assert.IsTrue(battle.DefenderSquads[0].Animated);
        Assert.IsFalse(battle.DefenderSquads[1].Animated);
        Assert.IsTrue(battle.AttackingSquadAnimated);
        Assert.IsTrue(battle.DefendingSquadAnimated);

        static int ChartValue(ChartDataDto chart, string label)
        {
            int index = chart.Labels.IndexOf(label);
            Assert.IsTrue(index >= 0, $"Missing chart category: {label}");
            return chart.Datasets.Single().Data[index];
        }

        Assert.AreEqual(30, ChartValue(battle.AttackerInitialArmyByRace, dwarf));
        Assert.AreEqual(40, ChartValue(battle.AttackerInitialArmyByRace, $"Undead {dwarf}"));
        Assert.AreEqual(10, ChartValue(battle.AttackerInitialArmyByRace, $"Undead {goblin}"));
        Assert.AreEqual(20, ChartValue(battle.DefenderInitialArmyByRace, $"Undead {dwarf}"));
        Assert.AreEqual(10, ChartValue(battle.DefenderInitialArmyByRace, dwarf));
        Assert.AreEqual(5, ChartValue(battle.AttackerDeathsByRace, dwarf));
        Assert.AreEqual(12, ChartValue(battle.AttackerDeathsByRace, $"Undead {dwarf}"));
        Assert.AreEqual(3, ChartValue(battle.AttackerDeathsByRace, $"Undead {goblin}"));
        Assert.AreEqual(7, ChartValue(battle.DefenderDeathsByRace, $"Undead {dwarf}"));
        Assert.AreEqual(2, ChartValue(battle.DefenderDeathsByRace, dwarf));

        Assert.AreEqual(80, battle.AttackerCount);
        Assert.AreEqual(30, battle.DefenderCount);
        Assert.AreEqual(29, battle.DeathCount);
        Assert.AreEqual(29, battle.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(110, battle.AttackerInitialArmyByRace.Datasets.Single().Data.Sum() +
            battle.DefenderInitialArmyByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(29, war.DeathsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(80, war.AttackerDeploymentsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(30, war.DefenderDeploymentsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(40, ChartValue(war.AttackerDeploymentsByRace, $"Undead {dwarf}"));
        Assert.AreEqual(19, ChartValue(war.DeathsByRace, $"Undead {dwarf}"));
    }

    [TestMethod]
    public void UndeadNotableFigures_AreNotMergedIntoLivingRaceCounts()
    {
        using var world = new World();
        var race = world.GetCreatureInfo("DWARF");
        world.HistoricalFigures.Add(new HistoricalFigure([], world)
        {
            Id = 40,
            Name = "Living Dwarf",
            Race = race
        });
        world.HistoricalFigures.Add(new HistoricalFigure([], world)
        {
            Id = 41,
            Name = "Undead Dwarf",
            Race = race,
            Zombie = true
        });

        Battle battle = new([
            P("id", "151"),
            P("attacking_hfid", "40"),
            P("attacking_hfid", "41")
        ], world);

        Assert.AreEqual(2, battle.AttackerCount);
        Assert.AreEqual(2, battle.AttackerInitialArmyByRace.Labels.Count);
        Assert.IsTrue(battle.AttackerInitialArmyByRace.Labels.Contains(race.NamePlural));
        Assert.IsTrue(battle.AttackerInitialArmyByRace.Labels.Contains($"Undead {race.NamePlural}"));
        Assert.AreEqual(2, battle.AttackerInitialArmyByRace.Datasets.Single().Data.Sum());
    }

    [TestMethod]
    public void WarDeploymentTotals_MatchRaceChartsAndPreserveWarSidesAcrossBattleRoleReversals()
    {
        using var world = new World();
        War war = CreateWar(world);
        Battle first = new([
            P("id", "102"),
            P("war_eventcol", "10"),
            P("attacking_squad_race", "HUMAN"),
            P("attacking_squad_number", "90"),
            P("defending_squad_race", "DWARF"),
            P("defending_squad_number", "50")
        ], world);

        Battle counterattack = new([
            P("id", "103"),
            P("war_eventcol", "10"),
            P("attacking_squad_race", "DWARF"),
            P("attacking_squad_number", "25"),
            P("defending_squad_race", "HUMAN"),
            P("defending_squad_number", "40")
        ], world);
        // The war's defending faction launches the second battle.
        counterattack.Attacker = war.Defender;
        counterattack.Defender = war.Attacker;
        war.EventCollections.Add(first);
        war.EventCollections.Add(counterattack);

        Assert.AreEqual(130, war.AttackerBattleDeployments);
        Assert.AreEqual(75, war.DefenderBattleDeployments);
        Assert.AreEqual(130, war.AttackerDeploymentsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(75, war.DefenderDeploymentsByRace.Datasets.Single().Data.Sum());
        Assert.AreEqual(130, new LegendsViewer.Backend.Contracts.WorldObjectDto(war).AttackerTroopCount);
        Assert.AreEqual(75, new LegendsViewer.Backend.Contracts.WorldObjectDto(war).DefenderTroopCount);
    }

    [TestMethod]
    public void FieldBattle_WithAbsentOptionalPartiesOrRegion_DoesNotThrow()
    {
        using var world = new World();
        var fieldBattle = new FieldBattle([P("id", "20")], world);
        Assert.IsTrue(fieldBattle.Print(false).Contains("an unknown region"));
    }
}

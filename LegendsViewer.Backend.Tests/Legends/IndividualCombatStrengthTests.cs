using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class IndividualCombatStrengthTests
{
    [DataTestMethod]
    [DataRow(0, 40L)]
    [DataRow(499, 40L)]
    [DataRow(500, 41L)]
    [DataRow(18000, 76L)]
    [DataRow(19999, 79L)]
    [DataRow(20000, 80L)]
    [DataRow(29000, 80L)]
    [DataRow(-100, 40L)]
    public void NativeFactorUsesTotalIpWithIntegerStepsAndCap(int ip, long expected) =>
        Assert.AreEqual(expected, IndividualCombatStrength.SkillFactor(ip));

    [TestMethod]
    public void SkillGroupsExcludeNonCombatAndUseWideSums()
    {
        var points = IndividualCombatStrength.Points([
            new("axe", int.MaxValue), new("AXE", 100), new("shield", int.MaxValue),
            new("situational_awareness", 500), new("dodging", 18000), new("mining", 99999),
            new("melee_combat", 99999), new("throw", 99999), new("armor", -10)]);
        Assert.AreEqual(2L * int.MaxValue, points.Material);
        Assert.AreEqual(18500L, points.Defensive);
        Assert.AreEqual(80L, IndividualCombatStrength.SkillFactor(points.Material));
    }

    [TestMethod]
    public void EstimatedTrainingIncrementKeepsUntrainedBaseAndDistinguishesLegendary()
    {
        Assert.AreEqual(120d, IndividualCombatStrength.Estimate(120, []));
        Assert.AreEqual(120d, IndividualCombatStrength.Estimate(120, [new("axe", 0)]));
        Assert.AreEqual(129d, IndividualCombatStrength.Estimate(120, [new("axe", 18000)]));
        Assert.AreEqual(245d, IndividualCombatStrength.Estimate(120, [new("axe", 18000), new("dodging", 18000)]));
        Assert.AreEqual(260d, IndividualCombatStrength.Estimate(120, [new("axe", 29000), new("dodging", 29000)]));
    }

    [TestMethod]
    public void BattleAndCommanderUseIndividualsOnceAndPreserveAnonymousSquads()
    {
        using World world = new();
        var dwarf = new CreatureInfo("DWARF") { NativeCombatRating = 120, AdultBodySize = 60000 };
        var warrior = new HistoricalFigure([], world) { Id = 1, Race = dwarf, BirthYear = 0, DeathYear = 40, Skills = [new("axe", 18000), new("dodging", 18000)] };
        var battle = new Battle([], world) { Outcome = BattleOutcome.AttackerWon, StartYear = 40 };
        battle.NotableAttackers.AddRange([warrior, warrior]);
        battle.AttackerSquads.Add(new(dwarf, 10, 0, 0, 0));
        battle.DefenderSquads.Add(new(dwarf, 20, 0, 0, 0));
        Assert.AreEqual(1445d, battle.AttackerArmyStrength);
        Assert.AreEqual(2400d, battle.DefenderArmyStrength);
        Assert.AreEqual(11, battle.AttackerCount);
        Assert.AreEqual("native-skill-estimate", battle.ArmyStrengthBasis);
        var commander = new CommanderRecord(2, "Commander");
        commander.AddBattle(battle, true);
        Assert.AreEqual(1445d, commander.Battles.Single().OwnEffectiveStrength);
        Assert.AreEqual("native-skill-estimate", commander.Battles.Single().OddsBasis);
        var untrained = new CommanderRecord(3, "Untrained");
        warrior.Skills.Clear();
        untrained.AddBattle(battle, true);
        Assert.IsTrue(commander.CommandScore < untrained.CommandScore);
        Assert.AreEqual("native", battle.ArmyStrengthBasis);
    }

    [TestMethod]
    public void BodySizeFallbackRetainsSkillsButUnknownAndTransformedKeepExistingProxy()
    {
        using World world = new();
        var dwarf = new CreatureInfo("DWARF") { AdultBodySize = 60000 };
        var warrior = new HistoricalFigure([], world) { Race = dwarf, BirthYear = 0, DeathYear = 40, Skills = [new("dodging", 20000)] };
        var battle = new Battle([], world) { StartYear = 40 };
        battle.NotableAttackers.Add(warrior);
        battle.DefenderSquads.Add(new(dwarf, 1, 0, 0, 0));
        Assert.AreEqual(2400d, battle.AttackerArmyStrength);
        Assert.AreEqual("body-size-skill-estimate", battle.ArmyStrengthBasis);
        warrior.Animated = true;
        Assert.AreEqual(1200d, battle.AttackerArmyStrength);
        Assert.AreEqual("body-size-undead", battle.ArmyStrengthBasis);
        warrior.Animated = false;
        warrior.PreviousRace = "ELF";
        Assert.AreEqual(1200d, battle.AttackerArmyStrength);
        warrior.PreviousRace = "";
        warrior.Race = new CreatureInfo("UNKNOWN_TEST_CREATURE");
        Assert.AreEqual(1200d, battle.AttackerArmyStrength);
        Assert.AreEqual(1, battle.AttackerStrengthEstimate.PlaceholderCount);
    }

    [TestMethod]
    public void HistoricalPointsScaleBeforeNativeFactorCap()
    {
        using World world = new();
        var dwarf = new CreatureInfo("DWARF") { NativeCombatRating = 120 };
        var warrior = new HistoricalFigure([], world)
        {
            Race = dwarf, BirthYear = 100, DeathYear = 152,
            Skills = [new("axe", 18000), new("dodging", 40000)]
        };
        Assert.AreEqual(0L, IndividualCombatStrength.HistoricalPoints(warrior, 112, 200).Total);
        var early = IndividualCombatStrength.HistoricalPoints(warrior, 122, 200);
        Assert.AreEqual(4500L, early.Material);
        Assert.AreEqual(10000L, early.Defensive);
        Assert.AreEqual(183d, IndividualCombatStrength.Estimate(120, early));
        Assert.AreEqual(0L, IndividualCombatStrength.HistoricalPoints(warrior, 153, 200).Total);
        warrior.DeathYear = -1;
        Assert.AreEqual(4500L, IndividualCombatStrength.HistoricalPoints(warrior, 122, 152).Material);
        warrior.BirthYear = -1;
        Assert.AreEqual(0L, IndividualCombatStrength.HistoricalPoints(warrior, 122, 152).Total);
    }

    [TestMethod]
    public void NotableParticipantsIncludeBothSidesAndNonCombatantsWithoutDuplicates()
    {
        using World world = new();
        var race = new CreatureInfo("DWARF");
        var warrior = new HistoricalFigure([], world)
        {
            Id = 1, Race = race, BirthYear = 0, DeathYear = 52,
            Skills = [new("axe", 18000), new("mining", 29000)]
        };
        var defender = new HistoricalFigure([], world) { Id = 2, Race = race };
        var civilian = new HistoricalFigure([], world) { Id = 3, Race = race };
        var battle = new Battle([], world) { StartYear = 32 };
        battle.NotableAttackers.AddRange([warrior, warrior]);
        battle.AttackerSupportMercenaryHfs.Add(warrior);
        battle.NotableDefenders.Add(defender);
        battle.NonCombatants.Add(civilian);
        var participants = battle.NotableParticipants;
        Assert.AreEqual(3, participants.Count);
        StringAssert.Contains(participants[0].Subtitle!, "Estimated at battle:");
        StringAssert.Contains(participants[0].Subtitle!, "Professional");
        Assert.IsFalse(participants[0].Subtitle!.Contains("Mining", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(participants[1].Subtitle!, "Defender");
        StringAssert.Contains(participants[2].Subtitle!, "Non-combatant");
    }

    [TestMethod]
    public void HallOfFameRanksIndividualsAndExperienceWithStableTies()
    {
        using World world = new();
        var race = new CreatureInfo("DWARF") { NativeCombatRating = 120 };
        world.HistoricalFigures.AddRange([
            new([], world) { Id = 1, Name = "Untrained", Race = race },
            new([], world) { Id = 3, Name = "Legendary", Race = race, Skills = [new("axe", 18000)] },
            new([], world) { Id = 2, Name = "Legendary tie", Race = race, Skills = [new("axe", 18000)] },
            new([], world) { Id = 4, Name = "Craftsman", Race = race, Skills = [new("carpentry", 100000)] }
        ]);
        var cards = WorldRecordsCalculator.Calculate(world).Categories.Single(c => c.Id == "warfare").Cards;
        var strength = cards.Single(c => c.Id == "individual_combat");
        Assert.AreEqual(2, strength.Entries[0].Id);
        Assert.AreEqual(129d, strength.Entries[0].NumericValue);
        var proficiency = cards.Single(c => c.Id == "combat_proficiency");
        Assert.AreEqual(2, proficiency.Entries.Count);
        Assert.AreEqual(18000d, proficiency.Entries[0].NumericValue);
        Assert.AreEqual(120d, cards.Single(c => c.Id == "native_combat").Entries[0].NumericValue);
    }
}

using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class CommanderRecordTests
{
    [TestMethod]
    public void CreditsEachCommandOnceAndKeepsCommandersWithMissingOutcomes()
    {
        using World world = new();
        HistoricalFigure first = new([], world) { Id = 1, Name = "First" };
        HistoricalFigure second = new([], world) { Id = 2, Name = "Second" };
        HistoricalFigure third = new([], world) { Id = 3, Name = "Third" };
        HistoricalFigure fighter = new([], world) { Id = 4, Name = "Fighter" };
        Battle battle = new([], world)
        {
            Id = 20, Outcome = BattleOutcome.DefenderWon, AttackerDeathCount = 12, DefenderDeathCount = 3
        };
        battle.NotableAttackers.Add(fighter);
        Battle unknown = new([], world) { Id = 21, DefenderDeathCount = 5 };
        world.Events.AddRange([
            new FieldBattle([], world) { ParentCollection = battle, AttackerGeneral = first, DefenderGeneral = second },
            new AttackedSite([], world) { ParentCollection = battle, DefenderGeneral = second },
            new FieldBattle([], world) { ParentCollection = unknown, AttackerGeneral = first },
            new AttackedSite([], world) { AttackerGeneral = third }
        ]);

        var records = new CommanderController(world).Get().ToDictionary(r => r.Id);
        Assert.AreEqual(3, records.Count);
        var defense = records[second.Id].Battles.Single();
        Assert.AreEqual(20, defense.Id);
        Assert.AreEqual("Victory", defense.Outcome);
        Assert.AreEqual(12, defense.OwnKills);
        Assert.AreEqual(3, defense.EnemyKills);
        Assert.AreEqual(1, defense.EnemyTroops);
        Assert.AreEqual("Unknown", records[first.Id].Battles.Single(b => b.Id == 21).Outcome);
        Assert.AreEqual(0, records[third.Id].Battles.Count);
        Assert.AreEqual(1, records[first.Id].Losses);
        Assert.AreEqual(0d, records[first.Id].WinPercentage!.Value);
        Assert.AreEqual(8L, records[first.Id].EnemiesKilled);
        Assert.AreEqual(1, records[second.Id].Victories);
        Assert.AreEqual(100d, records[second.Id].WinPercentage!.Value);
        Assert.AreEqual(12L, records[second.Id].EnemiesKilled);
        Assert.IsNull(records[third.Id].WinPercentage);
    }

    [TestMethod]
    public void RatesWinsAgainstStartingNumbersAndSeparatesMatchups()
    {
        using World world = new();
        Battle battle = new([], world) { Outcome = BattleOutcome.AttackerWon, DefenderDeathCount = 8 };
        battle.AttackerSquads.Add(new(null!, 10, 0, 0, 0));
        battle.DefenderSquads.Add(new(null!, 20, 8, 0, 0));
        CommanderRecord attacker = new(1, "Attacker");
        CommanderRecord defender = new(2, "Defender");

        attacker.AddBattle(battle, true);
        defender.AddBattle(battle, false);
        Assert.AreEqual(1, attacker.Outnumbered.Wins);
        Assert.AreEqual(1, defender.Favored.Battles);
        Assert.AreEqual(0, defender.Favored.Wins);
        double expectedScore = 100 * Math.Sqrt(0.3) * (2d / 3) / (Math.Sqrt(0.3) + 3) / 11;
        Assert.AreEqual(expectedScore, attacker.CommandScore!.Value, 0.00001);
        Assert.AreEqual(-expectedScore, defender.CommandScore!.Value, 0.00001);
        Assert.AreEqual(0L, attacker.OwnDeaths);
        Assert.AreEqual(8L, defender.OwnDeaths);

        battle.DefenderSquads[0].Numbers = 11;
        battle.Outcome = BattleOutcome.Unknown;
        attacker.AddBattle(battle, true);
        Assert.AreEqual(1, attacker.NearPeer.Battles);
        Assert.AreEqual(0, attacker.NearPeer.Wins);
        Assert.AreEqual(1, attacker.RatedBattles);

        battle.AttackerSquads.Clear();
        battle.Outcome = BattleOutcome.AttackerWon;
        attacker.AddBattle(battle, true);
        Assert.AreEqual(1, attacker.RatedBattles);
        Assert.AreEqual(2, attacker.Victories);
    }

    [TestMethod]
    public void NativeRatingsReverseNumericalOddsAndRequireCompleteCoverage()
    {
        using World world = new();
        var strong = new LegendsViewer.Backend.Legends.Various.CreatureInfo("strong") { NativeCombatRating = 10 };
        var weak = new LegendsViewer.Backend.Legends.Various.CreatureInfo("weak") { NativeCombatRating = 2 };
        Battle battle = new([], world) { Outcome = BattleOutcome.AttackerWon };
        battle.AttackerSquads.Add(new(strong, 10, 0, 0, 0));
        battle.DefenderSquads.Add(new(weak, 25, 0, 0, 0));
        CommanderRecord attacker = new(1, "Attacker");
        CommanderRecord defender = new(2, "Defender");
        attacker.AddBattle(battle, true);
        defender.AddBattle(battle, false);
        Assert.AreEqual(100d, attacker.Battles[0].OwnEffectiveStrength!.Value);
        Assert.AreEqual(50d, attacker.Battles[0].EnemyEffectiveStrength!.Value);
        Assert.AreEqual(1, attacker.Favored.Wins);
        Assert.AreEqual(1, defender.Outnumbered.Battles);
        double weight = Math.Sqrt(0.35);
        Assert.AreEqual(100 * weight / 3 / (weight + 3) / 11, attacker.CommandScore!.Value, 0.00001);
        Assert.AreEqual(-attacker.CommandScore.Value, defender.CommandScore!.Value, 0.00001);

        weak.NativeCombatRating = null;
        CommanderRecord fallback = new(3, "Fallback");
        fallback.AddBattle(battle, true);
        Assert.AreEqual(12000d, fallback.Battles[0].OwnEffectiveStrength!.Value);
        Assert.AreEqual(30000d, fallback.Battles[0].EnemyEffectiveStrength!.Value);
        Assert.AreEqual("body-size-placeholders", fallback.Battles[0].OddsBasis);
        Assert.AreEqual(1, fallback.Outnumbered.Wins);
    }

    [TestMethod]
    public void ExistingExportsUseVanillaSizesWithoutMixingRatingSources()
    {
        using World world = new();
        var dwarf = new LegendsViewer.Backend.Legends.Various.CreatureInfo("DWARF") { NativeCombatRating = 17 };
        var troll = new LegendsViewer.Backend.Legends.Various.CreatureInfo("troll");
        Battle battle = new([], world) { Outcome = BattleOutcome.AttackerWon };
        battle.AttackerSquads.Add(new(dwarf, 100, 0, 0, 0));
        battle.DefenderSquads.Add(new(troll, 50, 0, 0, 0));
        CommanderRecord attacker = new(1, "Attacker");
        attacker.AddBattle(battle, true);
        Assert.AreEqual("body-size", attacker.Battles[0].OddsBasis);
        Assert.AreEqual(120000d, attacker.Battles[0].OwnEffectiveStrength!.Value);
        Assert.AreEqual(250000d, attacker.Battles[0].EnemyEffectiveStrength!.Value);
        Assert.AreEqual(1, attacker.Outnumbered.Wins);

        troll.NativeCombatRating = 20;
        attacker.AddBattle(battle, true);
        Assert.AreEqual("native", attacker.Battles[1].OddsBasis);
        Assert.AreEqual(1700d, attacker.Battles[1].OwnEffectiveStrength!.Value);

        troll.NativeCombatRating = null;
        troll.AdultBodySize = 60000;
        attacker.AddBattle(battle, true);
        Assert.AreEqual(60000d, attacker.Battles[2].EnemyEffectiveStrength!.Value);
        Assert.AreEqual(1, attacker.Favored.Wins);

        battle.DefenderSquads[0].Race = new("unknown_modded_race");
        attacker.AddBattle(battle, true);
        Assert.AreEqual("body-size-placeholders", attacker.Battles[3].OddsBasis);
        Assert.AreEqual(120000d, attacker.Battles[3].OwnEffectiveStrength!.Value);
        Assert.AreEqual(60000d, attacker.Battles[3].EnemyEffectiveStrength!.Value);
        Assert.AreEqual(0d, attacker.Battles[3].EnemyStrengthEstimate!.Coverage!.Value);
    }

    [TestMethod]
    public void BattleStrengthFieldsAgreeWithCommanderAndOverview()
    {
        using World world = new();
        var dwarf = new LegendsViewer.Backend.Legends.Various.CreatureInfo("dwarf");
        var troll = new LegendsViewer.Backend.Legends.Various.CreatureInfo("troll");
        Battle battle = new([], world) { Outcome = BattleOutcome.DefenderWon };
        battle.AttackerSquads.Add(new(dwarf, 100, 0, 0, 0));
        battle.DefenderSquads.Add(new(troll, 50, 0, 0, 0));
        Assert.AreEqual(120000d, battle.AttackerArmyStrength);
        Assert.AreEqual(250000d, battle.DefenderArmyStrength);
        Assert.AreEqual("body-size", battle.ArmyStrengthBasis);
        Assert.AreEqual(0.48d, battle.ArmyStrengthRatio!.Value, 0.00001);
        var row = new LegendsViewer.Backend.Contracts.WorldObjectDto(battle);
        Assert.AreEqual(battle.AttackerArmyStrength, row.AttackerArmyStrength!.Value);
        Assert.AreEqual(battle.DefenderArmyStrength, row.DefenderArmyStrength!.Value);
        Assert.AreEqual(battle.ArmyStrengthRatio, row.ArmyStrengthRatio);
        Assert.AreEqual(battle.ArmyStrengthBasis, row.ArmyStrengthBasis);
        CommanderRecord defender = new(1, "Defender");
        defender.AddBattle(battle, false);
        Assert.AreEqual(battle.DefenderArmyStrength, defender.Battles[0].OwnEffectiveStrength!.Value);
        Assert.AreEqual(battle.AttackerArmyStrength, defender.Battles[0].EnemyEffectiveStrength!.Value);

        battle.DefenderSquads[0].Animated = true;
        dwarf.NativeCombatRating = 10;
        troll.NativeCombatRating = 20;
        Assert.AreEqual("body-size-undead", battle.ArmyStrengthBasis);
        Assert.AreEqual(120000d, battle.AttackerArmyStrength);
        Assert.AreEqual(250000d, battle.DefenderArmyStrength);
        Assert.AreEqual(0.48d, battle.ArmyStrengthRatio!.Value, 0.00001);

        battle.DefenderSquads[0].Race = new("unknown_animated_race");
        Assert.AreEqual("body-size-undead-placeholders", battle.ArmyStrengthBasis);
        Assert.AreEqual(120000d, battle.AttackerArmyStrength);
        Assert.AreEqual(60000d, battle.DefenderArmyStrength);
        Assert.AreEqual(2d, battle.ArmyStrengthRatio!.Value);
        battle.DefenderSquads.Clear();
        Assert.IsNull(battle.ArmyStrengthRatio);
    }

    [TestMethod]
    public void MixedWarBeastsAgainstAnimatedArmyUseSizeOdds()
    {
        using World world = new();
        Battle battle = new([], world) { Outcome = BattleOutcome.DefenderWon };
        void Add(List<Battle.Squad> squads, string race, int count, bool animated = false)
        {
            squads.Add(new(new LegendsViewer.Backend.Legends.Various.CreatureInfo(race), count, 0, 0, 0, animated));
        }

        Add(battle.DefenderSquads, "ELF", 30);
        Add(battle.DefenderSquads, "DWARF", 22);
        Add(battle.DefenderSquads, "GOBLIN", 12);
        Add(battle.DefenderSquads, "HUMAN", 4);
        foreach (string race in new[] { "ANDREWSARCHUS", "CAMEL_2_HUMP", "JAGUAR", "LEOPARD",
            "BEAR_GRIZZLY", "BIRD_SWALLOW_CAVE_GIANT", "OLM_GIANT", "CROCODILE_CAVE", "JABBERER" })
            Add(battle.DefenderSquads, race, 5);
        Add(battle.AttackerSquads, "DWARF", 591, true);
        Add(battle.AttackerSquads, "HUMAN", 220, true);
        Add(battle.AttackerSquads, "GOBLIN", 172, true);
        Add(battle.AttackerSquads, "ELF", 21, true);

        Assert.AreEqual(113, battle.DefenderCount);
        Assert.AreEqual(1004, battle.AttackerCount);
        Assert.AreEqual("body-size-undead", battle.ArmyStrengthBasis);
        Assert.AreEqual(799900d, battle.DefenderArmyStrength);
        Assert.AreEqual(1248800d, battle.AttackerArmyStrength);
        CommanderRecord commander = new(1, "Defender");
        commander.AddBattle(battle, false);
        Assert.AreEqual(1, commander.NearPeer.Wins);
        Assert.AreEqual("body-size-undead", commander.Battles[0].OddsBasis);
    }

    [TestMethod]
    public void UnknownCombatantsDoNotEraseKnownRatings()
    {
        using World world = new();
        var dwarf = new LegendsViewer.Backend.Legends.Various.CreatureInfo("dwarf");
        var troll = new LegendsViewer.Backend.Legends.Various.CreatureInfo("troll");
        var unknown = new LegendsViewer.Backend.Legends.Various.CreatureInfo("custom_beast");
        Battle battle = new([], world) { Outcome = BattleOutcome.AttackerWon };
        battle.AttackerSquads.Add(new(dwarf, 100, 0, 0, 0));
        battle.AttackerSquads.Add(new(troll, 20, 0, 0, 0));
        battle.AttackerSquads.Add(new(unknown, 10, 0, 0, 0));
        battle.DefenderSquads.Add(new(dwarf, 100, 0, 0, 0, true));
        Assert.AreEqual(232000d, battle.AttackerArmyStrength);
        Assert.AreEqual(120000d, battle.DefenderArmyStrength);
        Assert.AreEqual("body-size-undead-placeholders", battle.ArmyStrengthBasis);
        Assert.AreEqual(120d / 130 * 100, battle.AttackerRatingCoverage!.Value, 0.00001);
        Assert.AreEqual(100d, battle.DefenderRatingCoverage!.Value);
        Assert.AreEqual("custom_beast", battle.AttackerStrengthEstimate.Placeholders.Single().Race);
        Assert.AreEqual(10, battle.AttackerStrengthEstimate.PlaceholderCount);
        CommanderRecord defender = new(1, "Defender");
        defender.AddBattle(battle, false);
        Assert.AreEqual(10, defender.Battles[0].EnemyStrengthEstimate!.PlaceholderCount);
        unknown.AdultBodySize = 100000;
        Assert.AreEqual(240000d, battle.AttackerArmyStrength);
        Assert.AreEqual(100d, battle.AttackerRatingCoverage!.Value);
        Assert.AreEqual("body-size-undead", battle.ArmyStrengthBasis);
    }

    [TestMethod]
    public void PrehistoricAndMissingVanillaRacesUseExportedIdentifiers()
    {
        var sizes = new Dictionary<string, int>
        {
            ["BOBCAT"] = 8000,
            ["CENOZOIC_ANDREWSARCHUS"] = 850000,
            ["CENOZOIC_DEINOTHERIUM"] = 11793000,
            ["CENOZOIC_KELENKEN"] = 100000,
            ["CENOZOIC_MAMMOTH_PYGMY"] = 1055000,
            ["CENOZOIC_MAMMOTH_WOOLLY"] = 6045000,
            ["CENOZOIC_MEGACEROPS"] = 3000000,
            ["CENOZOIC_MEGALANIA"] = 450000,
            ["CENOZOIC_PARACERATHERIUM"] = 18000000,
            ["CENOZOIC_RHINOCEROS_WOOLLY"] = 2650000,
            ["CENOZOIC_SMILODON"] = 328000,
            ["CENOZOIC_THYLACINE"] = 19000,
            ["CRETACEOUS_CARNOTAURUS"] = 1700000,
            ["CRETACEOUS_QUETZALCOATLUS"] = 225000,
            ["CRETACEOUS_QUETZALCOATLUS_MAN"] = 147500,
            ["CRETACEOUS_THERIZINOSAURUS"] = 5000000,
            ["ELK_BIRD"] = 100000,
            ["GIANT_OPOSSUM"] = 221040,
            ["JURASSIC_AFROVENATOR"] = 950000,
            ["JURASSIC_KENTROSAURUS"] = 1600000,
            ["PERMIAN_ANTEOSAURUS"] = 600000,
            ["RUTHERER"] = 3000000,
            ["VORACIOUS_CAVE_CRAWLER"] = 900000,
        };
        foreach (var (id, size) in sizes)
        {
            var race = new LegendsViewer.Backend.Legends.Various.CreatureInfo(id.ToLowerInvariant());
            Assert.AreEqual(size / 50, race.EstimatedCombatRating!.Value, id);
        }
    }

    [TestMethod]
    public void TransformedFiguresRetainKnownRaceSize()
    {
        using World world = new();
        var human = new LegendsViewer.Backend.Legends.Various.CreatureInfo("human") { NativeCombatRating = 10 };
        var dwarf = new LegendsViewer.Backend.Legends.Various.CreatureInfo("dwarf") { NativeCombatRating = 10 };
        Battle battle = new([], world);
        battle.NotableAttackers.Add(new HistoricalFigure([], world) { Id = 1, Race = human, PreviousRace = "elf" });
        battle.NotableDefenders.Add(new HistoricalFigure([], world) { Id = 2, Race = dwarf });
        Assert.AreEqual("body-size", battle.ArmyStrengthBasis);
        Assert.AreEqual(1400d, battle.AttackerArmyStrength);
        Assert.AreEqual(1200d, battle.DefenderArmyStrength);
        Assert.AreEqual(100d, battle.AttackerRatingCoverage!.Value);
        battle.NotableAttackers[0].Race = new("night_creature_14");
        Assert.AreEqual("body-size-placeholders", battle.ArmyStrengthBasis);
        Assert.AreEqual(0d, battle.AttackerRatingCoverage!.Value);
    }

    [TestMethod]
    public void LargerBattlesCarryMoreWeightForWinsAndLosses()
    {
        using World world = new();
        Battle battle = new([], world) { Outcome = BattleOutcome.AttackerWon };
        battle.AttackerSquads.Add(new(null!, 200, 0, 0, 0));
        battle.DefenderSquads.Add(new(null!, 200, 0, 0, 0));
        CommanderRecord attacker = new(1, "Attacker");
        CommanderRecord defender = new(2, "Defender");
        attacker.AddBattle(battle, true);
        defender.AddBattle(battle, false);

        battle.AttackerSquads[0].Numbers = 50;
        battle.DefenderSquads[0].Numbers = 50;
        battle.Outcome = BattleOutcome.DefenderWon;
        attacker.AddBattle(battle, true);
        defender.AddBattle(battle, false);

        // Both went 1-1 at even odds. The 400-troop battle counts twice as much.
        Assert.AreEqual(100d / 72, attacker.CommandScore!.Value, 0.00001);
        Assert.AreEqual(-100d / 72, defender.CommandScore!.Value, 0.00001);
        Assert.AreEqual(2, attacker.RatedBattles);
    }

    [TestMethod]
    public void ExperienceRewardsProvenCommandersEvenAgainstPerfectShortRecords()
    {
        using World world = new();
        Battle battle = new([], world) { Outcome = BattleOutcome.AttackerWon };
        battle.AttackerSquads.Add(new(null!, 500, 0, 0, 0));
        battle.DefenderSquads.Add(new(null!, 500, 0, 0, 0));
        CommanderRecord novice = new(1, "Novice");
        CommanderRecord veteran = new(2, "Veteran");
        for (int i = 0; i < 3; i++) novice.AddBattle(battle, true);
        for (int i = 0; i < 30; i++)
        {
            battle.Outcome = i < 24 ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon;
            veteran.AddBattle(battle, true);
        }
        Assert.IsTrue(veteran.CommandScore!.Value > novice.CommandScore!.Value);
        Assert.AreEqual(100 * 1.5 * Math.Sqrt(10) / (3 * Math.Sqrt(10) + 3) * 3 / 13, novice.CommandScore.Value, 0.00001);
    }
}

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
        Assert.IsNull(fallback.Battles[0].OwnEffectiveStrength);
        Assert.IsNull(fallback.Battles[0].EnemyEffectiveStrength);
        Assert.AreEqual(1, fallback.Outnumbered.Wins);
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

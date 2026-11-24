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
        Assert.AreEqual(1, records[first.Id].Losses);
        Assert.AreEqual(0d, records[first.Id].WinPercentage!.Value);
        Assert.AreEqual(8L, records[first.Id].EnemiesKilled);
        Assert.AreEqual(1, records[second.Id].Victories);
        Assert.AreEqual(100d, records[second.Id].WinPercentage!.Value);
        Assert.AreEqual(12L, records[second.Id].EnemiesKilled);
        Assert.IsNull(records[third.Id].WinPercentage);
    }
}

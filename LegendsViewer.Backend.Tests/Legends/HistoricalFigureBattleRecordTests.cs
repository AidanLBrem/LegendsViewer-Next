using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class HistoricalFigureBattleRecordTests
{
    [TestMethod]
    public void CountsRecordedSidesOnlyOnceAndSkipsUnknownOrNoncombatants()
    {
        using World world = new();
        HistoricalFigure figure = new([], world) { Id = 1, Name = "Captain" };
        HistoricalFigure other = new([], world) { Id = 2, Name = "Bystander" };

        Battle win = new([], world) { Id = 100, Outcome = BattleOutcome.AttackerWon };
        win.NotableAttackers.Add(figure);

        Battle loss = new([], world) { Id = 101, Outcome = BattleOutcome.AttackerWon };
        loss.NotableDefenders.Add(figure);

        Battle defendingWin = new([], world) { Id = 102, Outcome = BattleOutcome.DefenderWon };
        defendingWin.NotableDefenders.Add(figure);

        Battle unknown = new([], world) { Id = 103, Outcome = BattleOutcome.Unknown };
        unknown.NotableAttackers.Add(figure);

        Battle ambiguous = new([], world) { Id = 104, Outcome = BattleOutcome.DefenderWon };
        ambiguous.NotableAttackers.Add(figure);
        ambiguous.NotableDefenders.Add(figure);

        Battle noncombatant = new([], world) { Id = 105, Outcome = BattleOutcome.DefenderWon };
        noncombatant.NonCombatants.Add(figure);

        figure.Battles.AddRange([win, win, loss, defendingWin, unknown, ambiguous, noncombatant]);
        other.Battles.Add(noncombatant);

        Assert.AreEqual(2, figure.BattlesWon);
        Assert.AreEqual(1, figure.BattlesLost);
        Assert.AreEqual(0, other.BattlesWon);
        Assert.AreEqual(0, other.BattlesLost);

        var row = new WorldObjectDto(figure);
        Assert.AreEqual(2, row.BattlesWon);
        Assert.AreEqual(1, row.BattlesLost);
    }

    [TestMethod]
    public void CommanderRequiresAnExplicitGeneralEvent()
    {
        using World world = new();
        HistoricalFigure commander = new([], world) { Id = 1, Name = "Commander" };
        HistoricalFigure fighter = new([], world) { Id = 2, Name = "Fighter" };

        Battle battle = new([], world) { Id = 99, Outcome = BattleOutcome.AttackerWon };
        battle.NotableAttackers.Add(fighter);
        fighter.Battles.Add(battle);
        Assert.IsFalse(fighter.IsCommander);
        Assert.AreEqual(1, fighter.BattlesWon);

        commander.Events.Add(new FieldBattle([], world)
        {
            AttackerGeneral = commander
        });
        Assert.IsTrue(commander.IsCommander);
        Assert.AreEqual(0, commander.BattlesWon);

        var positive = new WorldObjectFilterDto
        {
            Filters = [new FilterRuleDto { PropertyName = "IsCommander", Operator = FilterOperator.Equals, Value = "true" }]
        };
        var negative = new WorldObjectFilterDto
        {
            Filters = [new FilterRuleDto { PropertyName = "IsCommander", Operator = FilterOperator.Equals, Value = "false" }]
        };

        Assert.IsTrue(commander.MatchesFilterCriteria(positive));
        Assert.IsFalse(fighter.MatchesFilterCriteria(positive));
        Assert.IsTrue(fighter.MatchesFilterCriteria(negative));
    }

    [TestMethod]
    public void HallOfFameListsWinningAndLosingFiguresSeparately()
    {
        using World world = new();
        HistoricalFigure winner = new([], world) { Id = 1, Name = "Winner" };
        HistoricalFigure loser = new([], world) { Id = 2, Name = "Loser" };
        world.HistoricalFigures.AddRange([winner, loser]);

        Battle battle = new([], world) { Id = 101, Outcome = BattleOutcome.AttackerWon };
        battle.NotableAttackers.Add(winner);
        battle.NotableDefenders.Add(loser);
        winner.Battles.Add(battle);
        loser.Battles.Add(battle);

        var warfare = WorldRecordsCalculator.Calculate(world).Categories
            .Single(category => category.Id == "warfare");
        var wins = warfare.Cards.Single(card => card.Id == "battle_victories");
        var losses = warfare.Cards.Single(card => card.Id == "battle_defeats");

        Assert.AreEqual(winner.Id, wins.Entries.Single().Id);
        Assert.AreEqual(loser.Id, losses.Entries.Single().Id);
        Assert.AreEqual(1d, wins.Entries.Single().NumericValue);
        Assert.AreEqual(1d, losses.Entries.Single().NumericValue);
    }
}

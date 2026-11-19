using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends.EventCollections;

[TestClass]
public class DuelTests
{
    private Mock<IWorld> _mockWorld = null!;
    private HistoricalFigure _attacker = null!;
    private HistoricalFigure _defender = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorld = new Mock<IWorld>();
        _mockWorld.Setup(w => w.ParsingErrors).Returns(new ParsingErrors());
        
        _attacker = new HistoricalFigure { Id = 1, Name = "Fighter A", Icon = "person" };
        _defender = new HistoricalFigure { Id = 2, Name = "Fighter B", Icon = "person" };
        
        _mockWorld.Setup(w => w.GetHistoricalFigure(1)).Returns(_attacker);
        _mockWorld.Setup(w => w.GetHistoricalFigure(2)).Returns(_defender);
    }

    [TestMethod]
    public void Constructor_WithValidProperties_ParsesCorrectly()
    {
        var props = new List<Property>
        {
            new Property { Name = "ordinal", Value = "1" },
            new Property { Name = "attacking_hfid", Value = "1" },
            new Property { Name = "defending_hfid", Value = "2" }
        };

        var duel = new Duel(props, _mockWorld.Object);

        Assert.IsNotNull(duel);
        Assert.AreEqual(1, duel.Ordinal);
    }

    [TestMethod]
    public void Constructor_SetsDefaultName()
    {
        var props = new List<Property>
        {
            new Property { Name = "ordinal", Value = "1" }
        };

        var duel = new Duel(props, _mockWorld.Object);

        Assert.IsTrue(duel.Name.Contains("duel"));
    }

    [TestMethod]
    public void DuelDeaths_SelectLivingOrUndeadSquadOfSameRace()
    {
        using var world = new World();
        var dwarf = world.GetCreatureInfo("DWARF");
        var livingAttacker = new HistoricalFigure([], world) { Id = 1, Race = dwarf, Name = "Living attacker" };
        var undeadAttacker = new HistoricalFigure([], world) { Id = 2, Race = dwarf, Name = "Undead attacker", Skeleton = true };
        var livingDefender = new HistoricalFigure([], world) { Id = 3, Race = dwarf, Name = "Living defender" };
        var undeadDefender = new HistoricalFigure([], world) { Id = 4, Race = dwarf, Name = "Undead defender", Skeleton = true };
        world.HistoricalFigures.AddRange([livingAttacker, undeadAttacker, livingDefender, undeadDefender]);

        var battle = new Battle([
            new Property { Name = "id", Value = "10" },
            new Property { Name = "start_year", Value = "100" },
            new Property { Name = "attacking_hfid", Value = "1" },
            new Property { Name = "attacking_hfid", Value = "2" },
            new Property { Name = "defending_hfid", Value = "3" },
            new Property { Name = "defending_hfid", Value = "4" }
        ], world);
        world.EventCollections.Add(battle);

        Assert.AreEqual(2, battle.Attackers.Count);
        Assert.AreEqual(2, battle.Defenders.Count);

        var attackerDeath = new HfDied([
            new Property { Name = "id", Value = "101" },
            new Property { Name = "year", Value = "100" },
            new Property { Name = "hfid", Value = "1" }
        ], world);
        var defenderDeath = new HfDied([
            new Property { Name = "id", Value = "102" },
            new Property { Name = "year", Value = "100" },
            new Property { Name = "hfid", Value = "4" }
        ], world);
        world.Events.Add(attackerDeath);
        world.Events.Add(defenderDeath);

        _ = new Duel([
            new Property { Name = "id", Value = "11" },
            new Property { Name = "parent_eventcol", Value = "10" },
            new Property { Name = "event", Value = "101" }
        ], world);
        _ = new Duel([
            new Property { Name = "id", Value = "12" },
            new Property { Name = "parent_eventcol", Value = "10" },
            new Property { Name = "event", Value = "102" }
        ], world);

        Assert.AreEqual(1, battle.AttackerDeathCount);
        Assert.AreEqual(1, battle.DefenderDeathCount);
        Assert.AreEqual(1, battle.Attackers.Single(s => !s.Animated).Deaths);
        Assert.AreEqual(0, battle.Attackers.Single(s => s.Animated).Deaths);
        Assert.AreEqual(0, battle.Defenders.Single(s => !s.Animated).Deaths);
        Assert.AreEqual(1, battle.Defenders.Single(s => s.Animated).Deaths);
    }

    [TestMethod]
    public void ToLink_WithLink_ReturnsHtml()
    {
        var props = new List<Property>
        {
            new Property { Name = "ordinal", Value = "1" }
        };

        var duel = new Duel(props, _mockWorld.Object);

        var result = duel.ToLink(link: true);

        Assert.IsTrue(result.Contains("duel") || result.Contains("the"));
    }

    [TestMethod]
    public void ToString_ReturnsFormattedString()
    {
        var props = new List<Property>
        {
            new Property { Name = "ordinal", Value = "1" }
        };

        var duel = new Duel(props, _mockWorld.Object);

        var result = duel.ToString();

        Assert.IsTrue(result.Contains("duel"));
    }
}

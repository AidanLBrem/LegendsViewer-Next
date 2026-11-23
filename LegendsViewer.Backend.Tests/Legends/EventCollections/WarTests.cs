using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends.EventCollections;

[TestClass]
public class WarTests
{
    private Mock<IWorld> _mockWorld = null!;
    private Entity _attacker = null!;
    private Entity _defender = null!;
    private Entity _parentEntity = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorld = new Mock<IWorld>();
        _mockWorld.Setup(w => w.ParsingErrors).Returns(new ParsingErrors());
        _mockWorld.Setup(w => w.Events).Returns(new List<WorldEvent>());
        
        _parentEntity = new Entity([], _mockWorld.Object) { Id = 0, Name = "Parent", Icon = "civilization" };
        
        _attacker = new Entity([], _mockWorld.Object) { Id = 1, Name = "Dwarves", Icon = "dwarves", Parent = _parentEntity };
        _defender = new Entity([], _mockWorld.Object) { Id = 2, Name = "Goblins", Icon = "goblins", Parent = _parentEntity };
        
        _mockWorld.Setup(w => w.GetEntity(1)).Returns(_attacker);
        _mockWorld.Setup(w => w.GetEntity(2)).Returns(_defender);
    }

    [TestMethod]
    public void LeadUp_OrdersOpposingIncidentsAndRequiresWarLinkAtStart()
    {
        var war = new War([], _mockWorld.Object) { StartYear = 20, StartSeconds72 = 100 };
        war.RegisterParticipant(_attacker, true);
        war.RegisterParticipant(_defender, false);
        SiteDispute Incident(int id, int year, int tick, Entity opponent) =>
            new([], _mockWorld.Object) { Id = id, Year = year, Seconds72 = tick,
                Entity1 = opponent, Entity2 = _attacker };

        var earlier = Incident(1, 10, 0, _defender);
        var opening = Incident(2, 20, 100, _defender);
        opening.ParentCollection = war;
        _attacker.Events.AddRange([opening, earlier, earlier, Incident(3, 9, 0, _defender),
            Incident(4, 20, 101, _defender), Incident(5, 20, 100, _defender),
            Incident(6, 19, 0, _parentEntity)]);

        CollectionAssert.AreEqual(new[] { "Dispute", "War opening · Dispute" },
            war.LeadUp.Select(row => row.Title).ToArray());
    }

    [TestMethod]
    public void Constructor_WithValidProperties_ParsesCorrectly()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "War of the Mountains" },
            new Property { Name = "aggressor_ent_id", Value = "1" },
            new Property { Name = "defender_ent_id", Value = "2" }
        };

        var war = new War(props, _mockWorld.Object);

        Assert.IsNotNull(war);
        Assert.AreEqual("War of the Mountains", war.Name);
    }

    [TestMethod]
    public void Constructor_SetsDefaultIcon()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "War" }
        };

        var war = new War(props, _mockWorld.Object);

        Assert.IsTrue(war.Icon.Contains("sword"));
    }

    [TestMethod]
    public void ToLink_WithLink_ReturnsHtml()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "Great War" }
        };

        var war = new War(props, _mockWorld.Object);

        var result = war.ToLink(link: true);

        Assert.IsTrue(result.Contains("war") || result.Contains("anchor"));
    }

    [TestMethod]
    public void ToString_ReturnsName()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "War of Testing" }
        };

        var war = new War(props, _mockWorld.Object);

        var result = war.ToString();

        Assert.AreEqual("War of Testing", result);
    }

    [TestMethod]
    public void GenerateComplexSubType_SetsSubtype()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "War" },
            new Property { Name = "aggressor_ent_id", Value = "1" },
            new Property { Name = "defender_ent_id", Value = "2" }
        };

        var war = new War(props, _mockWorld.Object);
        war.GenerateComplexSubType();

        Assert.IsFalse(string.IsNullOrEmpty(war.Subtype));
    }
}

using LegendsViewer.Backend.Legends.WorldObjects;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using System.Reflection;
using System.Text.Json.Serialization;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends.WorldObjects;

[TestClass]
public class EntityTests
{
    private Mock<IWorld> _mockWorld = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorld = new Mock<IWorld>();
        _mockWorld.Setup(w => w.ParsingErrors).Returns(new ParsingErrors());
    }

    [TestMethod]
    public void NationPopulation_AggregatesResidentsFromSubordinateSiteOwners()
    {
        var nation = new Entity([], _mockWorld.Object);
        var subordinate = new Entity([], _mockWorld.Object) { Parent = nation };
        CreatureInfo dwarves = new("DWARF");
        CreatureInfo humans = new("HUMAN");

        subordinate.AddPopulations([
            new Population(_mockWorld.Object, dwarves, 120),
            new Population(_mockWorld.Object, humans, 30)
        ]);
        subordinate.AddPopulations([
            new Population(_mockWorld.Object, dwarves, 80)
        ]);

        Assert.AreEqual(200, nation.Populations.Single(p => p.Race == dwarves).Count);
        Assert.AreEqual(30, nation.Populations.Single(p => p.Race == humans).Count);
        Assert.AreEqual(230, nation.Populations.Sum(p => p.Count));
        Assert.AreEqual(230, subordinate.Populations.Sum(p => p.Count));
    }

    [TestMethod]
    public void NationPopulation_DoesNotSerializeAnExpandedStringForEveryResident()
    {
        var property = typeof(Entity).GetProperty(nameof(Entity.PopulationsAsList));
        Assert.IsNotNull(property);
        Assert.IsTrue(property.IsDefined(typeof(JsonIgnoreAttribute), inherit: true));
    }

    [TestMethod]
    public void Constructor_WithNameProperty_ParsesCorrectly()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "Mountainhome" }
        };

        var entity = new Entity(props, _mockWorld.Object);

        Assert.IsNotNull(entity);
        Assert.AreEqual("Mountainhome", entity.Name);
    }

    [TestMethod]
    public void Constructor_WithoutName_SetsDefaultName()
    {
        var props = new List<Property>();

        var entity = new Entity(props, _mockWorld.Object);

        Assert.IsNotNull(entity.Name);
    }

    [TestMethod]
    public void GetTitle_ReturnsType()
    {
        var props = new List<Property>
        {
            new Property { Name = "name", Value = "Test Entity" }
        };

        var entity = new Entity(props, _mockWorld.Object);

        var result = entity.GetTitle();

        Assert.IsNotNull(result);
    }
}

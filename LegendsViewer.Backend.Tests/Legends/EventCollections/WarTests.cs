using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
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
    public void LeadUp_IncludesDefenderOnlyEventsAndFiftyYearHistory()
    {
        var war = new War([], _mockWorld.Object) { StartYear = 100, StartSeconds72 = 100 };
        war.RegisterParticipant(_attacker, true);
        war.RegisterParticipant(_defender, false);

        var defenderOnly = new SiteDispute([], _mockWorld.Object)
        {
            Id = 100, Year = 51, Seconds72 = 0, Entity1 = _defender, Entity2 = _attacker
        };
        var tooOld = new SiteDispute([], _mockWorld.Object)
        {
            Id = 101, Year = 49, Seconds72 = 0, Entity1 = _defender, Entity2 = _attacker
        };
        _defender.Events.AddRange([tooOld, defenderOnly]);

        Assert.AreEqual(1, war.LeadUp.Count);
        Assert.AreEqual("Site dispute", war.LeadUp.Single().Title);
        Assert.AreEqual(50, war.LeadUpYears);
        Assert.IsTrue(war.LeadUpDiagnostics.Any(d => d.Title == "Displayed incidents"));
    }

    [TestMethod]
    public void LeadUp_FollowsHistoricalLocalGovernmentOwnership()
    {
        _attacker.IsCiv = true;
        _defender.IsCiv = true;
        var local = new Entity([], _mockWorld.Object)
        {
            Id = 23, Name = "Local government", Parent = _attacker
        };
        var site = new Site([], _mockWorld.Object) { Id = 24, Name = "Outpost" };
        var owned = new OwnerPeriod(site, local, 10, "founded") { EndYear = 20 };
        _attacker.SiteHistory.Add(owned);
        _mockWorld.Setup(w => w.Entities).Returns([_attacker, _defender, local]);

        var war = new War([], _mockWorld.Object) { StartYear = 30, StartSeconds72 = 100 };
        war.RegisterParticipant(_attacker, true);
        war.RegisterParticipant(_defender, false);
        local.Events.AddRange([
            new SiteDispute([], _mockWorld.Object)
                { Id = 200, Year = 15, Seconds72 = 0, Entity1 = local, Entity2 = _defender },
            new SiteDispute([], _mockWorld.Object)
                { Id = 201, Year = 25, Seconds72 = 0, Entity1 = local, Entity2 = _defender }
        ]);

        Assert.AreEqual(1, war.LeadUp.Count, "The post-transfer dispute must not be attributed to the former owner.");
        Assert.IsTrue(war.LeadUpDiagnostics.Any(d => d.Title == "Unresolved faction links" &&
            d.Subtitle!.Contains("1 classified")));
    }

    [TestMethod]
    public void LeadUp_ProvidesPriorWarContextWithoutClaimingItsCause()
    {
        var prior = new War([], _mockWorld.Object)
        {
            Id = 40, Name = "Old conflict", StartYear = 5, EndYear = 15,
            Attacker = _attacker, Defender = _defender
        };
        _mockWorld.Setup(w => w.Wars).Returns([prior]);
        var war = new War([], _mockWorld.Object)
        {
            Id = 41, StartYear = 90, StartSeconds72 = 100
        };
        war.RegisterParticipant(_attacker, true);
        war.RegisterParticipant(_defender, false);

        Assert.AreEqual(0, war.LeadUp.Count);
        Assert.IsTrue(war.LeadUpContext.Any(c => c.Title!.Contains("previous war")));
        Assert.IsTrue(war.LeadUpContext.Any(c => c.Subtitle!.Contains("Old conflict")));
    }

    [TestMethod]
    public void LeadUp_AuditsUnattributableAndForeignOpeningEvents()
    {
        var war = new War([], _mockWorld.Object)
        {
            Id = 80, StartYear = 20, StartSeconds72 = 100
        };
        war.RegisterParticipant(_attacker, true);
        war.RegisterParticipant(_defender, false);

        var foreignOpening = new SiteDispute([], _mockWorld.Object)
            { Id = 81, Year = 20, Seconds72 = 100, Entity1 = _attacker, Entity2 = _defender };
        var noFaction = new PeaceRejected([], _mockWorld.Object)
            { Id = 82, Year = 19, Seconds72 = 0 };
        _attacker.Events.AddRange([foreignOpening, noFaction]);

        Assert.AreEqual(0, war.LeadUp.Count);
        Assert.IsTrue(war.LeadUpDiagnostics.Any(d =>
            d.Title == "Excluded at war start" && d.Subtitle!.Contains("1 events")));
        Assert.IsTrue(war.LeadUpDiagnostics.Any(d =>
            d.Title == "Unresolved faction links" && d.Subtitle!.Contains("1 classified")));
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

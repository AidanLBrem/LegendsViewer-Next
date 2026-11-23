using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldLinks;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class DarkForcesTests
{
    private readonly List<HistoricalFigure> _figures = [];
    private readonly List<Entity> _entities = [];
    private readonly List<Site> _sites = [];
    private Mock<IWorld> _world = null!;

    [TestInitialize]
    public void Initialize()
    {
        _figures.Clear();
        _entities.Clear();
        _sites.Clear();
        _world = new Mock<IWorld>();
        _world.Setup(w => w.CurrentYear).Returns(50);
        _world.Setup(w => w.HistoricalFigures).Returns(_figures);
        _world.Setup(w => w.Entities).Returns(_entities);
        _world.Setup(w => w.Sites).Returns(_sites);
        _world.Setup(w => w.ParsingErrors).Returns(new ParsingErrors());
        _world.Setup(w => w.Events).Returns(new List<WorldEvent>());
    }

    private HistoricalFigure Necromancer(int id, string name)
    {
        var figure = new HistoricalFigure([], _world.Object) { Id = id, Name = name };
        figure.ActiveInteractions.Add("SECRET_OF_LIFE_AND_DEATH");
        _figures.Add(figure);
        _world.Setup(w => w.GetHistoricalFigure(id)).Returns(figure);
        return figure;
    }

    private Entity Faction(int id, string name, bool isCivilization = false)
    {
        var entity = new Entity([], _world.Object) { Id = id, Name = name, IsCiv = isCivilization };
        _entities.Add(entity);
        _world.Setup(w => w.GetEntity(id)).Returns(entity);
        return entity;
    }

    private Site Settlement(int id)
    {
        var site = new Site([], _world.Object) { Id = id, Name = "Site " + id };
        _sites.Add(site);
        _world.Setup(w => w.GetSite(id)).Returns(site);
        return site;
    }

    [TestMethod]
    public void Territory_UsesMaximumSimultaneousSettlements_NotAllTimeTotal()
    {
        var leader = Necromancer(1, "Vex");
        var faction = Faction(10, "The Ashen Tower");
        leader.StartPositionAssignment(faction, 0, 0, "Leader");

        var first = Settlement(100);
        first.OwnerHistory.Add(new OwnerPeriod(first, faction, 0, "founded") { EndYear = 10 });
        first.OwnerHistory.Add(new OwnerPeriod(first, faction, 15, "reclaimed") { EndYear = 26 });
        var second = Settlement(101);
        second.OwnerHistory.Add(new OwnerPeriod(second, faction, 5, "captured") { EndYear = 20 });
        var third = Settlement(102);
        third.OwnerHistory.Add(new OwnerPeriod(third, faction, 30, "captured") { EndYear = 40 });

        var result = DarkForcesAnalysis.Calculate(_world.Object).Single();
        Assert.AreEqual(2, result.PeakSettlements);
        Assert.AreEqual(5, result.PeakYear);
        Assert.AreEqual(0, result.CurrentSettlements);
        Assert.AreEqual(0, result.Invasions);
    }

    [TestMethod]
    public void Territory_DoesNotDoubleCountOverlappingSiteGovernmentPeriods()
    {
        var leader = Necromancer(1, "Vex");
        var faction = Faction(10, "The Ashen Tower");
        leader.StartPositionAssignment(faction, 0, 0, "Leader");
        var government = Faction(11, "Tower Government");
        government.Parent = faction;

        var site = Settlement(100);
        site.OwnerHistory.Add(new OwnerPeriod(site, faction, 10, "founded"));
        site.OwnerHistory.Add(new OwnerPeriod(site, government, 11, "recorded owner"));

        var result = DarkForcesAnalysis.Calculate(_world.Object).Single();
        Assert.AreEqual(1, result.PeakSettlements);
        Assert.AreEqual(1, result.CurrentSettlements);
    }

    [TestMethod]
    public void OrdinaryCivilizationMembership_DoesNotGiveItsLandToNecromancer()
    {
        var figure = Necromancer(1, "Vex");
        var civilization = Faction(10, "The Mortal Kingdom", true);
        var site = Settlement(100);
        site.OwnerHistory.Add(new OwnerPeriod(site, civilization, 0, "founded"));
        figure.RelatedEntities.Add(new EntityLink([
            new Property { Name = "entity_id", Value = "10" },
            new Property { Name = "link_type", Value = "member" }
        ], _world.Object));

        var result = DarkForcesAnalysis.Calculate(_world.Object).Single();
        Assert.AreEqual(0, result.PeakSettlements);
        Assert.AreEqual(0, result.Factions.Count);
    }

    [TestMethod]
    public void LocalLeadership_DoesNotInheritParentCivilizationsTerritory()
    {
        var leader = Necromancer(1, "Vex");
        var kingdom = Faction(10, "The Kingdom", true);
        var local = Faction(11, "Tower Government");
        local.Parent = kingdom;
        leader.StartPositionAssignment(local, 0, 0, "Local ruler");

        var kingdomSite = Settlement(100);
        kingdomSite.OwnerHistory.Add(new OwnerPeriod(kingdomSite, kingdom, 1, "founded"));
        var localSite = Settlement(101);
        localSite.OwnerHistory.Add(new OwnerPeriod(localSite, local, 1, "founded"));

        var result = DarkForcesAnalysis.Calculate(_world.Object).Single();
        Assert.AreEqual(1, result.PeakSettlements);
        Assert.AreEqual(1, result.CurrentSettlements);
    }

    [TestMethod]
    public void Chronicle_SeparatesPersonalAssaultFromFactionInvasion()
    {
        var figure = Necromancer(1, "Vex");
        var faction = Faction(10, "The Ashen Tower");
        var enemy = Faction(20, "Freehold", true);
        var site = Settlement(100);
        figure.StartPositionAssignment(faction, 10, 0, "Leader");

        figure.Events.Add(new HfLearnsSecret([], _world.Object)
            { Id = 1, Year = 5, Student = figure, SecretText = "the secrets of life and death" });
        figure.Events.Add(new HfAttackedSite([
            new Property { Name = "attacker_hfid", Value = "1" },
            new Property { Name = "defender_civ_id", Value = "20" },
            new Property { Name = "site_civ_id", Value = "20" },
            new Property { Name = "site_id", Value = "100" }
        ], _world.Object) { Id = 2, Year = 20 });
        faction.Events.Add(new AttackedSite([], _world.Object)
            { Id = 3, Year = 25, Attacker = faction, Defender = enemy, Site = site });

        var result = DarkForcesAnalysis.Calculate(_world.Object).Single();
        Assert.AreEqual(20, result.FirstInvasionYear);
        Assert.AreEqual(2, result.Invasions);
        Assert.IsTrue(result.Timeline.Any(e => e.IsPrelude && e.Kind == "Learned a dark secret"));
        Assert.IsTrue(result.Timeline.Any(e => e.Kind == "Attacked a settlement" && e.Attribution == "Personal"));
        Assert.IsTrue(result.Timeline.Any(e => e.Kind == "Faction invaded" && e.Attribution == "Associated faction"));
    }

    [TestMethod]
    public void Sorting_UsesPowerOrTerritoryBeforePagination()
    {
        var raider = Necromancer(1, "Raider");
        var landholder = Necromancer(2, "Landholder");
        var enemy = Faction(20, "Freehold", true);
        var raiderTower = Faction(10, "The Raiders");
        var landholderTower = Faction(11, "The Towers");
        raider.StartPositionAssignment(raiderTower, 0, 0, "Leader");
        landholder.StartPositionAssignment(landholderTower, 0, 0, "Leader");

        for (int i = 0; i < 12; i++)
        {
            raiderTower.Events.Add(new AttackedSite([], _world.Object)
            {
                Id = i + 1, Year = i + 10, Attacker = raiderTower,
                Defender = enemy, Site = Settlement(i + 100)
            });
        }
        for (int i = 0; i < 3; i++)
        {
            var site = Settlement(i + 200);
            site.OwnerHistory.Add(new OwnerPeriod(site, landholderTower, 1, "founded"));
        }
        var controller = new DarkForcesController(_world.Object);
        var powerful = (DarkForcesPageDto)((OkObjectResult)controller.Get(sort: "power", pageSize: 1).Result!).Value!;
        var extensive = (DarkForcesPageDto)((OkObjectResult)controller.Get(sort: "territory", pageSize: 1).Result!).Value!;
        Assert.AreEqual(1, powerful.Items.Single().FigureId);
        Assert.AreEqual(2, extensive.Items.Single().FigureId);
        Assert.AreEqual(2, powerful.TotalCount);
    }

    [TestMethod]
    public void FallenFilter_IncludesDocumentedDeath_AndTerritorialCollapse()
    {
        var deceased = Necromancer(1, "Dead mage");
        deceased.DeathYear = 25;
        var survivor = Necromancer(2, "Survivor");
        var fallen = Necromancer(3, "Lost realm");
        var faction = Faction(10, "Dark realm");
        fallen.StartPositionAssignment(faction, 0, 0, "Leader");
        var site = Settlement(100);
        site.OwnerHistory.Add(new OwnerPeriod(site, faction, 5, "founded")
            { EndYear = 20, EndCause = "taken over" });

        var result = DarkForcesAnalysis.Calculate(_world.Object);
        Assert.AreEqual("Deceased", result.Single(n => n.FigureId == 1).Status);
        Assert.AreEqual("Realm lost", result.Single(n => n.FigureId == 3).Status);
        Assert.IsFalse(result.Single(n => n.FigureId == 2).Defeated);

        var controller = new DarkForcesController(_world.Object);
        var filtered = (DarkForcesPageDto)((OkObjectResult)controller.Get(status: "fallen").Result!).Value!;
        Assert.AreEqual(2, filtered.TotalFilteredCount);
        CollectionAssert.AreEquivalent(new[] { 1, 3 }, filtered.Items.Select(n => n.FigureId).ToArray());
    }
}

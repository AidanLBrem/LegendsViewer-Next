using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Enums;
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
    public void IndependentNecromancerGroup_SubholdingLeaderDoesNotInheritTheParentRealmOrArmy()
    {
        var groupRuler = Necromancer(1, "Ag");
        var subholdingRuler = Necromancer(2, "Sumost");
        var parentGroup = Faction(10, "The Black Conclave");
        var subholding = Faction(11, "The Ash Tower");
        var otherSubholding = Faction(12, "The Black Fortress");
        subholding.Parent = parentGroup;
        otherSubholding.Parent = parentGroup;
        var enemy = Faction(20, "Human Kingdom", true);

        groupRuler.StartPositionAssignment(parentGroup, 0, 0, "Leader");
        subholdingRuler.StartPositionAssignment(subholding, 10, 0, "Tower ruler");
        subholdingRuler.RelatedEntities.Add(new EntityLink([], _world.Object)
            { Entity = parentGroup, Type = EntityLinkType.Member });
        subholdingRuler.RelatedEntities.Add(new EntityLink([], _world.Object)
            { Entity = subholding, Type = EntityLinkType.Position, PositionId = 0 });

        var parentSite = Settlement(100);
        parentSite.OwnerHistory.Add(new OwnerPeriod(parentSite, parentGroup, 5, "founded"));
        var towerSite = Settlement(101);
        towerSite.OwnerHistory.Add(new OwnerPeriod(towerSite, subholding, 12, "founded"));
        var otherSite = Settlement(102);
        otherSite.OwnerHistory.Add(new OwnerPeriod(otherSite, otherSubholding, 14, "founded"));

        parentGroup.Events.Add(new AttackedSite([], _world.Object)
            { Id = 201, Year = 25, Attacker = parentGroup, Defender = enemy, Site = parentSite });
        subholding.Events.Add(new AttackedSite([], _world.Object)
            { Id = 202, Year = 26, Attacker = subholding, Defender = enemy, Site = towerSite });
        otherSubholding.Events.Add(new AttackedSite([], _world.Object)
            { Id = 203, Year = 27, Attacker = otherSubholding, Defender = enemy, Site = otherSite });

        var figures = DarkForcesAnalysis.Calculate(_world.Object);
        var main = figures.Single(f => f.FigureId == 1);
        var local = figures.Single(f => f.FigureId == 2);

        Assert.AreEqual(3, main.PeakSettlements);
        Assert.AreEqual(3, main.CurrentSettlements);
        Assert.AreEqual(3, main.Invasions);
        Assert.AreEqual(parentGroup.Id, main.Factions.Single().Id);
        Assert.AreEqual("Group leader / founder", main.Groups.Single().Relationship);

        Assert.AreEqual(1, local.PeakSettlements);
        Assert.AreEqual(1, local.CurrentSettlements);
        Assert.AreEqual(1, local.Invasions);
        Assert.AreEqual(subholding.Id, local.Factions.Single().Id);
        Assert.AreEqual(parentGroup.Id, local.Groups.Single().Id);
        Assert.AreEqual("Subholding leader / founder", local.Groups.Single().Relationship);
        Assert.AreEqual(subholding.Id, local.Groups.Single().HoldingId);

        var controller = new DarkForcesController(_world.Object);
        var byGroup = (DarkForcesPageDto)((OkObjectResult)
            controller.Get(search: "Black Conclave").Result!).Value!;
        Assert.AreEqual(2, byGroup.TotalFilteredCount);
    }

    [TestMethod]
    public void CurrentLeaderAssignment_RecognizesOnlyCurrentYearWithoutBackdatingReign()
    {
        var figure = Necromancer(1, "Vex");
        var other = Necromancer(2, "Deputy");
        var tower = Faction(10, "The Ashen Tower");
        tower.EntityPositionAssignments.Add(new EntityPositionAssignment([], _world.Object)
            { PositionId = 0, HistoricalFigure = figure });
        tower.EntityPositionAssignments.Add(new EntityPositionAssignment([], _world.Object)
            { PositionId = 1, HistoricalFigure = other });

        var former = Settlement(100);
        former.OwnerHistory.Add(new OwnerPeriod(former, tower, 10, "founded")
            { EndYear = 30 });
        var current = Settlement(101);
        current.OwnerHistory.Add(new OwnerPeriod(current, tower, 5, "founded"));

        var result = DarkForcesAnalysis.Calculate(_world.Object);
        var ruler = result.Single(n => n.FigureId == 1);
        var deputy = result.Single(n => n.FigureId == 2);
        Assert.AreEqual(1, ruler.PeakSettlements);
        Assert.AreEqual(50, ruler.PeakYear);
        Assert.AreEqual(1, ruler.CurrentSettlements);
        Assert.AreEqual("Current leader (Legends+; start unknown)", ruler.Factions.Single().Evidence);
        Assert.AreEqual(0, deputy.PeakSettlements);
        Assert.AreEqual(0, deputy.Factions.Count);
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
    public void Apprentices_CountUniqueRecordedStudents_AndRetainFirstLesson()
    {
        var mentor = Necromancer(1, "The Black Tutor");
        var studentA = new HistoricalFigure([], _world.Object) { Id = 10, Name = "First Student" };
        var studentB = new HistoricalFigure([], _world.Object) { Id = 11, Name = "Second Student" };
        _figures.AddRange([studentA, studentB]);

        // Repeated lessons, self-teaching, unknown students, and undated/future
        // events must not inflate the number of apprentices.
        mentor.Events.AddRange([
            new HfLearnsSecret([], _world.Object)
                { Id = 101, Year = 12, Teacher = mentor, Student = studentA },
            new HfLearnsSecret([], _world.Object)
                { Id = 102, Year = 16, Teacher = mentor, Student = studentA },
            new HfLearnsSecret([], _world.Object)
                { Id = 103, Year = 14, Teacher = mentor, Student = studentB },
            new HfLearnsSecret([], _world.Object)
                { Id = 104, Year = 17, Teacher = mentor, Student = mentor },
            new HfLearnsSecret([], _world.Object)
                { Id = 105, Year = 18, Teacher = mentor, Student = null },
            new HfLearnsSecret([], _world.Object)
                { Id = 106, Year = 55, Teacher = mentor, Student = new HistoricalFigure { Id = 12 } }
        ]);

        var result = DarkForcesAnalysis.Calculate(_world.Object).Single();
        Assert.AreEqual(2, result.ApprenticeCount);
        CollectionAssert.AreEquivalent(new[] { 10, 11 },
            result.Apprentices.Select(a => a.FigureId).ToArray());
        Assert.AreEqual(12, result.Apprentices.Single(a => a.FigureId == 10).FirstLearnedYear);
        Assert.AreEqual("Second Student", result.Apprentices.Single(a => a.FigureId == 11).Name);
    }

    [TestMethod]
    public void Apprentices_AreSearchableByNameOrCount_AndSortableBeforePagination()
    {
        var renowned = Necromancer(1, "Renowned Tutor");
        var minor = Necromancer(2, "Minor Tutor");
        var unaffiliated = Necromancer(3, "Unaffiliated");
        var students = new[]
        {
            new HistoricalFigure([], _world.Object) { Id = 10, Name = "Ashen Apprentice" },
            new HistoricalFigure([], _world.Object) { Id = 11, Name = "Ruby Apprentice" },
            new HistoricalFigure([], _world.Object) { Id = 12, Name = "Silent Apprentice" }
        };
        _figures.AddRange(students);
        renowned.Events.AddRange([
            new HfLearnsSecret([], _world.Object)
                { Id = 1, Year = 2, Teacher = renowned, Student = students[0] },
            new HfLearnsSecret([], _world.Object)
                { Id = 2, Year = 3, Teacher = renowned, Student = students[1] }
        ]);
        minor.Events.Add(new HfLearnsSecret([], _world.Object)
            { Id = 3, Year = 4, Teacher = minor, Student = students[2] });

        var controller = new DarkForcesController(_world.Object);
        DarkForcesPageDto Page(ActionResult<DarkForcesPageDto> response) =>
            (DarkForcesPageDto)((OkObjectResult)response.Result!).Value!;

        var sorted = Page(controller.Get(sort: "apprentices", pageSize: 1));
        Assert.AreEqual(1, sorted.Items.Single().FigureId);
        Assert.AreEqual(2, sorted.Items.Single().ApprenticeCount);
        Assert.AreEqual(0, sorted.Items.Single().Apprentices.Count,
            "List endpoint omits apprentice detail records; detail endpoint retains them.");

        var least = Page(controller.Get(sort: "apprentices", direction: "asc", pageSize: 1));
        Assert.AreEqual(3, least.Items.Single().FigureId);
        Assert.AreEqual(1, Page(controller.Get(search: "Ruby Apprentice")).TotalFilteredCount);
        Assert.AreEqual(1, Page(controller.Get(minApprentices: 2)).TotalFilteredCount);
        Assert.AreEqual(1, Page(controller.Get(minApprentices: 1, maxApprentices: 1)).TotalFilteredCount);
        Assert.AreEqual(1, Page(controller.Get(maxApprentices: 0)).TotalFilteredCount);

        Assert.IsInstanceOfType(controller.Get(minApprentices: -1).Result, typeof(BadRequestObjectResult));
        Assert.IsInstanceOfType(controller.Get(minApprentices: 3, maxApprentices: 1).Result,
            typeof(BadRequestObjectResult));

        var detail = (DarkForceDto)((OkObjectResult)controller.GetFigure(1).Result!).Value!;
        Assert.AreEqual(2, detail.Apprentices.Count);
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
        deceased.DeathCause = DeathCause.PutToRest;
        var survivor = Necromancer(2, "Survivor");
        var oldAge = Necromancer(4, "Retired mage");
        oldAge.DeathYear = 45;
        oldAge.DeathCause = DeathCause.OldAge;
        var fallen = Necromancer(3, "Lost realm");
        var faction = Faction(10, "Dark realm");
        fallen.StartPositionAssignment(faction, 0, 0, "Leader");
        var site = Settlement(100);
        site.OwnerHistory.Add(new OwnerPeriod(site, faction, 5, "founded")
            { EndYear = 20, EndCause = "taken over" });

        var result = DarkForcesAnalysis.Calculate(_world.Object);
        Assert.AreEqual("Killed / put to rest", result.Single(n => n.FigureId == 1).Status);
        Assert.AreEqual("Deceased (cause not defeat)", result.Single(n => n.FigureId == 4).Status);
        Assert.IsFalse(result.Single(n => n.FigureId == 4).Defeated);
        Assert.AreEqual("Realm lost", result.Single(n => n.FigureId == 3).Status);
        Assert.IsFalse(result.Single(n => n.FigureId == 2).Defeated);

        var controller = new DarkForcesController(_world.Object);
        var filtered = (DarkForcesPageDto)((OkObjectResult)controller.Get(status: "fallen").Result!).Value!;
        Assert.AreEqual(2, filtered.TotalFilteredCount);
        CollectionAssert.AreEquivalent(new[] { 1, 3 }, filtered.Items.Select(n => n.FigureId).ToArray());
    }
}

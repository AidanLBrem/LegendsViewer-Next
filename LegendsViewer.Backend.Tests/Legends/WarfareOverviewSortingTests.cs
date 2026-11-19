using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.DataAccess.Repositories.Interfaces;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class WarfareOverviewSortingTests
{
    private static Property P(string name, string value) => new() { Name = name, Value = value };

    private static (Entity Attacker, Entity Defender) CreateSides(World world)
    {
        Entity attacker = new([], world) { Id = 1, Name = "North", IsCiv = true };
        Entity defender = new([], world) { Id = 2, Name = "South", IsCiv = true };
        world.Entities.AddRange([attacker, defender]);
        return (attacker, defender);
    }

    private static void AddGains(
        World world, EventCollection collection, Entity attacker, Entity defender,
        int count, int idBase)
    {
        for (int i = 0; i < count; i++)
        {
            Site site = new([], world)
            {
                Id = idBase + i,
                Name = $"Captured Site {idBase + i}"
            };
            SiteConquered conquest = new([P("id", (idBase + i).ToString())], world)
            {
                Attacker = attacker,
                Defender = defender,
                Site = site,
                ConquerType = SiteConqueredType.Conquest
            };
            collection.EventCollections.Add(conquest);
        }
    }

    [TestMethod]
    public void WarOverview_SortsByAttackerGainsBeforePaginating()
    {
        using World world = new();
        (Entity attacker, Entity defender) = CreateSides(world);
        War few = new([P("id", "10"), P("name", "Few")], world)
        {
            Attacker = attacker, Defender = defender,
            DeathCount = 10, AttackerDeathCount = 4, DefenderDeathCount = 6
        };
        War many = new([P("id", "11"), P("name", "Many")], world)
        {
            Attacker = attacker, Defender = defender,
            DeathCount = 40, AttackerDeathCount = 15, DefenderDeathCount = 25
        };
        War medium = new([P("id", "12"), P("name", "Medium")], world)
        {
            Attacker = attacker, Defender = defender,
            DeathCount = 30, AttackerDeathCount = 10, DefenderDeathCount = 20
        };
        AddGains(world, few, attacker, defender, 1, 100);
        AddGains(world, many, attacker, defender, 3, 200);
        AddGains(world, medium, attacker, defender, 2, 300);

        List<War> wars = [few, medium, many]; // Deliberately not sorted
        Mock<IWorldObjectRepository<War>> repository = new();
        repository.Setup(r => r.GetAllElements()).Returns(wars);
        repository.Setup(r => r.GetCount()).Returns(wars.Count);

        WarController controller = new(repository.Object);
        var firstResult = controller.Get(
            pageNumber: 1, pageSize: 1,
            sortKey: "attackerSitesGained", sortOrder: "desc", filter: null);
        var first = (firstResult.Result as OkObjectResult)?.Value
            as PaginatedResponse<WorldObjectDto>;
        Assert.IsNotNull(first);
        Assert.AreEqual(3, first.TotalFilteredCount);
        Assert.AreEqual(many.Id, first.Items.Single().Id);
        Assert.AreEqual(3, first.Items.Single().AttackerSitesGained);
        Assert.AreEqual(25, first.Items.Single().DefenderDeathCount);

        var secondResult = controller.Get(
            pageNumber: 2, pageSize: 1,
            sortKey: "attackerSitesGained", sortOrder: "desc", filter: null);
        var second = (secondResult.Result as OkObjectResult)?.Value
            as PaginatedResponse<WorldObjectDto>;
        Assert.IsNotNull(second);
        Assert.AreEqual(medium.Id, second.Items.Single().Id);
        Assert.AreEqual(2, second.Items.Single().DefenderSitesLost);
    }

    [TestMethod]
    public void BattleOverview_SortsByDefenderDeathsBeforePaginating()
    {
        using World world = new();
        (Entity attacker, Entity defender) = CreateSides(world);

        Battle small = new([P("id", "20"), P("name", "Small")], world)
        {
            Attacker = attacker, Defender = defender,
            AttackerDeathCount = 1, DefenderDeathCount = 5
        };
        Battle large = new([P("id", "21"), P("name", "Large")], world)
        {
            Attacker = attacker, Defender = defender,
            AttackerDeathCount = 3, DefenderDeathCount = 40
        };
        Battle medium = new([P("id", "22"), P("name", "Medium")], world)
        {
            Attacker = attacker, Defender = defender,
            AttackerDeathCount = 2, DefenderDeathCount = 12
        };
        AddGains(world, large, attacker, defender, 2, 400);

        List<Battle> battles = [small, medium, large];
        Mock<IWorldObjectRepository<Battle>> repository = new();
        repository.Setup(r => r.GetAllElements()).Returns(battles);
        repository.Setup(r => r.GetCount()).Returns(battles.Count);

        BattleController controller = new(repository.Object);
        var result = controller.Get(
            pageNumber: 1, pageSize: 1,
            sortKey: "defenderDeathCount", sortOrder: "desc", filter: null);
        var page = (result.Result as OkObjectResult)?.Value
            as PaginatedResponse<WorldObjectDto>;

        Assert.IsNotNull(page);
        Assert.AreEqual(large.Id, page.Items.Single().Id);
        Assert.AreEqual(43, page.Items.Single().DeathCount);
        Assert.AreEqual(40, page.Items.Single().DefenderDeathCount);
        Assert.AreEqual(2, page.Items.Single().AttackerSitesGained);
    }
}

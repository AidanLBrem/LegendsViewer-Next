using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.DataAccess.Repositories.Interfaces;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class EntityPopulationSortingTests
{
    [TestMethod]
    public void NationPopulationSort_IsNumericBeforePagination_AndUnknownLast()
    {
        using World world = new();
        var dwarves = new CreatureInfo("DWARF");
        var elves = new CreatureInfo("ELF");
        List<Entity> entities =
        [
            new Entity([], world) { Id = 1, Name = "Large", Populations = [new Population(world, dwarves, 100)] },
            new Entity([], world) { Id = 2, Name = "Smaller", Populations = [new Population(world, dwarves, 20)],
                EntityPopulation = new EntityPopulation([], world) { Race = dwarves, Count = 30 } },
            new Entity([], world) { Id = 3, Name = "Unknown" },
            new Entity([], world) { Id = 4, Name = "Mixed", Populations = [new Population(world, dwarves, 5)],
                EntityPopulation = new EntityPopulation([], world) { Race = elves, Count = 80 } }
        ];

        Assert.AreEqual(100, entities[0].PopulationCount);
        Assert.AreEqual(30, entities[1].PopulationCount); // Overlapping race: max, not sum
        Assert.IsNull(entities[2].PopulationCount);
        Assert.AreEqual(85, entities[3].PopulationCount); // Different races: sum

        Mock<IWorldObjectRepository<Entity>> repository = new();
        repository.Setup(r => r.GetAllElements()).Returns(entities);
        repository.Setup(r => r.GetCount()).Returns(entities.Count);
        EntityController controller = new(repository.Object);

        PaginatedResponse<WorldObjectDto> Page(int page, string order) =>
            (PaginatedResponse<WorldObjectDto>)((OkObjectResult)
                controller.Get(pageNumber: page, pageSize: 1,
                    sortKey: "populationCount", sortOrder: order).Result!).Value!;

        Assert.AreEqual(2, Page(1, "asc").Items.Single().Id);
        Assert.AreEqual(30, Page(1, "asc").Items.Single().PopulationCount);
        Assert.AreEqual(4, Page(2, "asc").Items.Single().Id);
        Assert.AreEqual(1, Page(3, "asc").Items.Single().Id);
        Assert.AreEqual(3, Page(4, "asc").Items.Single().Id);

        Assert.AreEqual(1, Page(1, "desc").Items.Single().Id);
        Assert.AreEqual(4, Page(2, "desc").Items.Single().Id);
        Assert.AreEqual(2, Page(3, "desc").Items.Single().Id);
        Assert.AreEqual(3, Page(4, "desc").Items.Single().Id);
    }
}

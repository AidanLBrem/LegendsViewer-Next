using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.DataAccess.Repositories.Interfaces;
using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class OverviewHtmlSortingTests
{
    [TestMethod]
    public void FactionsAndGroups_HtmlColumnSortsByEntityNameBeforePagination()
    {
        using World world = new();
        List<Entity> entities =
        [
            new Entity([], world) { Id = 3, Name = "Zebra Group" },
            new Entity([], world) { Id = 1, Name = "Alpha Group" },
            new Entity([], world) { Id = 2, Name = "Middle Group" }
        ];
        Mock<IWorldObjectRepository<Entity>> repository = new();
        repository.Setup(r => r.GetAllElements()).Returns(entities);
        repository.Setup(r => r.GetCount()).Returns(entities.Count);
        EntityController controller = new(repository.Object);

        var ascending = (PaginatedResponse<WorldObjectDto>)((OkObjectResult)
            controller.Get(pageNumber: 1, pageSize: 1,
                sortKey: "html", sortOrder: "asc").Result!).Value!;
        Assert.AreEqual(1, ascending.Items.Single().Id);
        Assert.AreEqual("Alpha Group", ascending.Items.Single().Name);

        var second = (PaginatedResponse<WorldObjectDto>)((OkObjectResult)
            controller.Get(pageNumber: 2, pageSize: 1,
                sortKey: "html", sortOrder: "asc").Result!).Value!;
        Assert.AreEqual(2, second.Items.Single().Id);

        var descending = (PaginatedResponse<WorldObjectDto>)((OkObjectResult)
            controller.Get(pageNumber: 1, pageSize: 1,
                sortKey: "HTML", sortOrder: "desc").Result!).Value!;
        Assert.AreEqual(3, descending.Items.Single().Id);
    }

    [TestMethod]
    public void HtmlSortWorksForOtherWorldObjectOverviewsAndPreservesNormalSorts()
    {
        using World world = new();
        List<War> wars =
        [
            new War([], world) { Id = 5, Name = "Winter War" },
            new War([], world) { Id = 3, Name = "Ash War" }
        ];

        Assert.AreEqual("Ash War", wars.SortByProperty("html", "asc").First().Name);
        Assert.AreEqual("Winter War", wars.SortByProperty("html", "desc").First().Name);
        Assert.AreEqual(3, wars.SortByProperty("id", "asc").First().Id);
        Assert.AreEqual(5, wars.SortByProperty("id", "desc").First().Id);
    }
}

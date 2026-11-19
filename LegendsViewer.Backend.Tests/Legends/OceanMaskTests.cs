using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Maps;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class OceanMaskTests
{
    [TestMethod]
    public void GetOceanMask_OnlyMarksOverworldOceanTiles()
    {
        using World world = new() { Width = 5, Height = 3 };
        world.Regions.Add(new WorldRegion([], world)
        {
            RegionType = RegionType.Ocean,
            Coordinates = [new Location(0, 0), new Location(4, 1), new Location(-1, 2)]
        });
        world.Regions.Add(new WorldRegion([], world)
        {
            RegionType = RegionType.Lake,
            Coordinates = [new Location(2, 0)]
        });
        world.Regions.Add(new WorldRegion([], world)
        {
            RegionType = RegionType.Grassland,
            Coordinates = [new Location(3, 1)]
        });

        var controller = new WorldMapController(world, new Mock<IWorldMapImageGenerator>().Object);
        var response = controller.GetOceanMask() as FileContentResult;

        Assert.IsNotNull(response);
        Assert.AreEqual("application/octet-stream", response.ContentType);
        Assert.HasCount(2, response.FileContents);
        Assert.AreEqual(1, response.FileContents[0] & 1); // (0, 0) is ocean
        Assert.AreEqual(2, response.FileContents[1] & 2); // (4, 1) is ocean
        Assert.AreEqual(0, response.FileContents[0] & (1 << 2)); // lake
        Assert.AreEqual(0, response.FileContents[1] & 1); // grassland
    }
}

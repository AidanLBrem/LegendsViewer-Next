using System.Collections.Generic;
using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Controllers;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Maps;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class WarfareMapTests
{
    [TestMethod]
    public void GetWarfareMap_ReturnsActiveWarsAndBattlesOnlyByDefault()
    {
        var worldMock = new Mock<IWorld>();
        var imageGeneratorMock = new Mock<IWorldMapImageGenerator>();

        worldMock.Setup(w => w.Events).Returns(new List<WorldEvent>());
        worldMock.Setup(w => w.Entities).Returns(new List<Entity>());

        var attacker = new Entity(new List<Property>
        {
            new Property { Name = "id", Value = "1" },
            new Property { Name = "name", Value = "The Iron Empire" }
        }, worldMock.Object);

        var defender = new Entity(new List<Property>
        {
            new Property { Name = "id", Value = "2" },
            new Property { Name = "name", Value = "The Golden Kingdom" }
        }, worldMock.Object);

        worldMock.Setup(w => w.Entities).Returns(new List<Entity> { attacker, defender });

        var activeWar = new War(new List<Property>
        {
            new Property { Name = "id", Value = "10" },
            new Property { Name = "name", Value = "War of Ashes" },
            new Property { Name = "aggressor_ent_id", Value = "1" },
            new Property { Name = "defender_ent_id", Value = "2" }
        }, worldMock.Object)
        {
            StartYear = 50,
            EndYear = -1
        };

        var endedWar = new War(new List<Property>
        {
            new Property { Name = "id", Value = "11" },
            new Property { Name = "name", Value = "Ancient Conflict" },
            new Property { Name = "aggressor_ent_id", Value = "1" },
            new Property { Name = "defender_ent_id", Value = "2" }
        }, worldMock.Object)
        {
            StartYear = 10,
            EndYear = 30
        };

        var activeBattle = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "100" },
            new Property { Name = "name", Value = "Siege of Gold" },
            new Property { Name = "coords", Value = "50,50" },
            new Property { Name = "war_eventcol", Value = "10" }
        }, worldMock.Object)
        {
            StartYear = 55,
            EndYear = -1
        };

        worldMock.Setup(w => w.EventCollections).Returns(new List<EventCollection> { activeWar, endedWar, activeBattle });

        var controller = new WorldMapController(worldMock.Object, imageGeneratorMock.Object);

        var result = controller.GetWarfareMap(activeOnly: true);

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(1, result.Value.Wars.Count);
        Assert.AreEqual(10, result.Value.Wars[0].Id);
        Assert.IsTrue(result.Value.Wars[0].IsActive);
        Assert.AreEqual(1, result.Value.Battles.Count);
        Assert.AreEqual(100, result.Value.Battles[0].Id);
    }

    [TestMethod]
    public void GetWarfareMap_ExcludesAncientAndEndedBattlesFromActiveOverlay()
    {
        var world = new Mock<IWorld>();
        var imageGenerator = new Mock<IWorldMapImageGenerator>();
        world.Setup(w => w.CurrentYear).Returns(200);
        world.Setup(w => w.Events).Returns(new List<WorldEvent>());
        world.Setup(w => w.Entities).Returns(new List<Entity>());

        var activeWar = new War(new List<Property>
        {
            new Property { Name = "id", Value = "1" },
            new Property { Name = "name", Value = "Current War" }
        }, world.Object) { StartYear = 150, EndYear = -1 };

        var endedWar = new War(new List<Property>
        {
            new Property { Name = "id", Value = "2" },
            new Property { Name = "name", Value = "Ended War" }
        }, world.Object) { StartYear = 80, EndYear = 100 };

        var recentBattle = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "3" },
            new Property { Name = "name", Value = "Recent Battle" }
        }, world.Object) { StartYear = 200, EndYear = -1, ParentCollection = activeWar };

        var oldBattle = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "4" },
            new Property { Name = "name", Value = "Old Battle" }
        }, world.Object) { StartYear = 150, EndYear = -1, ParentCollection = activeWar };

        var endedWarBattle = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "5" },
            new Property { Name = "name", Value = "Battle from Ended War" }
        }, world.Object) { StartYear = 200, EndYear = -1, ParentCollection = endedWar };

        world.Setup(w => w.EventCollections).Returns(
            new List<EventCollection> { activeWar, endedWar, recentBattle, oldBattle, endedWarBattle });

        var controller = new WorldMapController(world.Object, imageGenerator.Object);
        var result = controller.GetWarfareMap(activeOnly: true);

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(1, result.Value.Wars.Count);
        Assert.AreEqual(1, result.Value.Battles.Count);
        Assert.AreEqual(3, result.Value.Battles[0].Id);
    }

    [TestMethod]
    public void HistoricalMap_ReturnsDatedSiteOutcomesWithoutPollutingCurrentOverlay()
    {
        var world = new Mock<IWorld>();
        var generator = new Mock<IWorldMapImageGenerator>();
        world.Setup(w => w.Events).Returns(new List<WorldEvent>());
        world.Setup(w => w.Entities).Returns(new List<Entity>());

        var site = new Site([], world.Object)
        {
            Id = 92,
            Name = "Old Citadel",
            Coordinates = [new Location(16, 24)]
        };
        var outcome = new SiteConquered([
            new Property { Name = "id", Value = "42" },
            new Property { Name = "start_year", Value = "150" }
        ], world.Object)
        {
            Site = site,
            ConquerType = LegendsViewer.Backend.Legends.Enums.SiteConqueredType.Pillaging
        };

        world.Setup(w => w.EventCollections).Returns(new List<EventCollection> { outcome });
        var controller = new WorldMapController(world.Object, generator.Object);

        var current = controller.GetWarfareMap(activeOnly: true);
        var history = controller.GetWarfareMap(activeOnly: false);

        Assert.IsNotNull(current.Value);
        Assert.AreEqual(0, current.Value.SiteConquests.Count);
        Assert.IsNotNull(history.Value);
        Assert.AreEqual(1, history.Value.SiteConquests.Count);
        var marker = history.Value.SiteConquests[0];
        Assert.AreEqual(42, marker.Id);
        Assert.AreEqual(92, marker.SiteId);
        Assert.AreEqual("Old Citadel", marker.SiteName);
        Assert.AreEqual(150, marker.Year);
        Assert.AreEqual("Pillaging", marker.Outcome);
        Assert.AreEqual(16, marker.Coordinates!.X);
        Assert.AreEqual(24, marker.Coordinates!.Y);
    }

    [TestMethod]
    public void GetObjectCoordinates_ForWar_ReturnsBoundsAndCoordinates()
    {
        var worldMock = new Mock<IWorld>();
        var imageGeneratorMock = new Mock<IWorldMapImageGenerator>();

        worldMock.Setup(w => w.Events).Returns(new List<WorldEvent>());
        worldMock.Setup(w => w.Entities).Returns(new List<Entity>());

        var war = new War(new List<Property>
        {
            new Property { Name = "id", Value = "20" },
            new Property { Name = "name", Value = "Border War" }
        }, worldMock.Object)
        {
            StartYear = 100,
            EndYear = -1
        };

        var battle1 = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "201" },
            new Property { Name = "name", Value = "Clash at River" },
            new Property { Name = "coords", Value = "10,20" },
            new Property { Name = "war_eventcol", Value = "20" }
        }, worldMock.Object);

        var battle2 = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "202" },
            new Property { Name = "name", Value = "Clash at Mountain" },
            new Property { Name = "coords", Value = "30,40" },
            new Property { Name = "war_eventcol", Value = "20" }
        }, worldMock.Object);

        war.EventCollections.Add(battle1);
        war.EventCollections.Add(battle2);

        worldMock.Setup(w => w.EventCollections).Returns(new List<EventCollection> { war, battle1, battle2 });

        var controller = new WorldMapController(worldMock.Object, imageGeneratorMock.Object);

        var result = controller.GetObjectCoordinates("war", 20);

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(10, result.Value.MinX);
        Assert.AreEqual(30, result.Value.MaxX);
        Assert.AreEqual(20, result.Value.MinY);
        Assert.AreEqual(40, result.Value.MaxY);
        Assert.AreEqual(20.0, result.Value.CenterX);
        Assert.AreEqual(30.0, result.Value.CenterY);
        Assert.AreEqual(2, result.Value.Coordinates.Count);
    }

    [TestMethod]
    public void GetObjectCoordinates_ForBattle_ReturnsSinglePoint()
    {
        var worldMock = new Mock<IWorld>();
        var imageGeneratorMock = new Mock<IWorldMapImageGenerator>();

        worldMock.Setup(w => w.Events).Returns(new List<WorldEvent>());
        worldMock.Setup(w => w.Entities).Returns(new List<Entity>());

        var battle = new Battle(new List<Property>
        {
            new Property { Name = "id", Value = "300" },
            new Property { Name = "name", Value = "Great Battle" },
            new Property { Name = "coords", Value = "15,25" }
        }, worldMock.Object);

        worldMock.Setup(w => w.EventCollections).Returns(new List<EventCollection> { battle });

        var controller = new WorldMapController(worldMock.Object, imageGeneratorMock.Object);

        var result = controller.GetObjectCoordinates("battle", 300);

        Assert.IsNotNull(result.Value);
        Assert.AreEqual(15, result.Value.MinX);
        Assert.AreEqual(15, result.Value.MaxX);
        Assert.AreEqual(25, result.Value.MinY);
        Assert.AreEqual(25, result.Value.MaxY);
        Assert.AreEqual(1, result.Value.Coordinates.Count);
        Assert.AreEqual(15, result.Value.Coordinates[0].X);
        Assert.AreEqual(25, result.Value.Coordinates[0].Y);
    }
}

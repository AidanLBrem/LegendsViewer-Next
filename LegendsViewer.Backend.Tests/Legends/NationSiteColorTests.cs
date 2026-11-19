using System.Drawing;
using System.Reflection;
using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class NationSiteColorTests
{
    [TestMethod]
    public void GenerateCivColors_AssignsDifferentColorsToNationsOfTheSameRaceAndNecromancers()
    {
        using World world = new();
        var dwarvesA = new Entity([], world) { Id = 1, Name = "First Dwarven Kingdom", IsCiv = true };
        var dwarvesB = new Entity([], world) { Id = 2, Name = "Second Dwarven Kingdom", IsCiv = true };
        var necromancers = new Entity([], world) { Id = 3, Name = "Tower Necromancers" };
        var otherNecromancers = new Entity([], world) { Id = 4, Name = "Second Tower Necromancers" };
        var tower = new Site([], world) { Id = 10, SiteType = LegendsViewer.Backend.Legends.Enums.SiteType.Tower };
        var secondTower = new Site([], world) { Id = 11, SiteType = LegendsViewer.Backend.Legends.Enums.SiteType.Tower };

        var firstMountainHall = new Site([], world) { Id = 12, Coordinates = [new Location(10, 10)] };
        var secondMountainHall = new Site([], world) { Id = 13, Coordinates = [new Location(12, 12)] };
        tower.Coordinates.Add(new Location(11, 10));
        secondTower.Coordinates.Add(new Location(13, 12));

        world.Entities.AddRange([dwarvesA, dwarvesB, necromancers, otherNecromancers]);
        tower.OwnerHistory.Add(new OwnerPeriod(tower, necromancers, 15, "founded"));
        secondTower.OwnerHistory.Add(new OwnerPeriod(secondTower, otherNecromancers, 18, "founded"));
        firstMountainHall.OwnerHistory.Add(new OwnerPeriod(firstMountainHall, dwarvesA, 10, "founded"));
        secondMountainHall.OwnerHistory.Add(new OwnerPeriod(secondMountainHall, dwarvesB, 11, "founded"));
        world.Sites.AddRange([tower, secondTower, firstMountainHall, secondMountainHall]);

        typeof(World).GetMethod("GenerateCivColors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(world, null);

        Assert.AreNotEqual(dwarvesA.LineColor.ToArgb(), dwarvesB.LineColor.ToArgb());
        Assert.AreNotEqual(dwarvesA.LineColor.ToArgb(), necromancers.LineColor.ToArgb());
        Assert.AreNotEqual(dwarvesB.LineColor.ToArgb(), necromancers.LineColor.ToArgb());
        Assert.AreNotEqual(necromancers.LineColor.ToArgb(), otherNecromancers.LineColor.ToArgb());
        Assert.AreNotEqual(Color.Empty, necromancers.LineColor);
    }

    [TestMethod]
    public void SiteMarker_UsesFoundingNationColorInsteadOfConquerorColor()
    {
        using World world = new();
        var founders = new Entity([], world)
        {
            Id = 1, Name = "Founding Kingdom", IsCiv = true, LineColor = Color.Red
        };
        var conquerors = new Entity([], world)
        {
            Id = 2, Name = "Conquering Kingdom", IsCiv = true, LineColor = Color.Blue
        };
        world.Entities.AddRange([founders, conquerors]);

        var site = new Site(
            [new Property { Name = "civ_id", Value = "2" }],
            world) { Id = 20, Name = "Conquered Town" };

        var foundingPeriod = new OwnerPeriod(site, founders, 10, "founded") { EndYear = 30 };
        var conquestPeriod = new OwnerPeriod(site, conquerors, 30, "conquest");
        site.OwnerHistory.AddRange([foundingPeriod, conquestPeriod]);

        var marker = new SiteMarkerDto(site);

        Assert.AreEqual(founders.Id, marker.OriginOwnerId);
        Assert.AreEqual(conquerors.Id, marker.OwnerId);
        Assert.AreEqual(founders.LineColor, site.OriginNation!.LineColor);
        Assert.AreNotEqual(marker.Color, marker.OriginColor);
    }

    [TestMethod]
    public void SiteMarker_UsesOriginalCivIdWhenNoFoundingEventExists()
    {
        using World world = new();
        var founders = new Entity([], world)
        {
            Id = 4, Name = "Old Nation", IsCiv = true, LineColor = Color.Green
        };
        world.Entities.Add(founders);

        var site = new Site(
            [new Property { Name = "civ_id", Value = "4" }],
            world) { Id = 21 };

        var marker = new SiteMarkerDto(site);

        Assert.AreEqual(4, site.OriginalCivId);
        Assert.AreEqual(4, marker.OriginOwnerId);
        Assert.AreNotEqual(marker.Color, marker.OriginColor);
    }
    [TestMethod]
    public void GenerateCivColors_MoreThanSixThousandNations_ReusesPaletteWithoutCollisionLoops()
    {
        using World world = new();
        for (int i = 0; i < 7000; i++)
        {
            world.Entities.Add(new Entity([], world)
            {
                Id = i + 1,
                Name = $"Nation {i + 1}",
                IsCiv = true
            });
        }

        typeof(World).GetMethod("GenerateCivColors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(world, null);

        Assert.IsTrue(world.Entities.All(entity => entity.LineColor != Color.Empty));
        Assert.IsTrue(world.Entities.Select(entity => entity.LineColor.ToArgb()).Distinct().Count() < world.Entities.Count);
    }

    [TestMethod]
    public void GenerateCivColors_NearbyNationsWithSamePreferredColor_GetDifferentColors()
    {
        using World world = new();
        var north = new Entity([], world) { Id = 24, Name = "North", IsCiv = true };
        var south = new Entity([], world) { Id = 48, Name = "South", IsCiv = true };
        var farAway = new Entity([], world) { Id = 72, Name = "Far Away", IsCiv = true };
        var northernSite = new Site([], world) { Id = 1, Coordinates = [new Location(10, 10)] };
        var southernSite = new Site([], world) { Id = 2, Coordinates = [new Location(12, 11)] };
        var distantSite = new Site([], world) { Id = 3, Coordinates = [new Location(1000, 1000)] };

        northernSite.OwnerHistory.Add(new OwnerPeriod(northernSite, north, 10, "founded"));
        southernSite.OwnerHistory.Add(new OwnerPeriod(southernSite, south, 11, "founded"));
        distantSite.OwnerHistory.Add(new OwnerPeriod(distantSite, farAway, 12, "founded"));
        world.Entities.AddRange([north, south, farAway]);
        world.Sites.AddRange([northernSite, southernSite, distantSite]);

        typeof(World).GetMethod("GenerateCivColors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(world, null);

        Assert.AreNotEqual(north.LineColor.ToArgb(), south.LineColor.ToArgb());
        // Global uniqueness is not required: this nation is well outside the
        // local neighborhood and may reuse the same palette color as North.
        Assert.AreEqual(north.LineColor.ToArgb(), farAway.LineColor.ToArgb());
    }

    [TestMethod]
    public void EntityCurrentCiv_WithCyclicParents_ReturnsWithoutHanging()
    {
        using World world = new();
        var first = new Entity([], world) { Id = 1, Name = "First group" };
        var second = new Entity([], world) { Id = 2, Name = "Second group" };
        first.SetParent(second);
        second.SetParent(first);

        Assert.IsNull(first.CurrentCiv);
        Assert.IsNull(second.CurrentCiv);
    }

}

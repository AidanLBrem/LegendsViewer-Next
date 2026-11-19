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

        world.Entities.AddRange([dwarvesA, dwarvesB, necromancers, otherNecromancers]);
        tower.OwnerHistory.Add(new OwnerPeriod(tower, necromancers, 15, "founded"));
        secondTower.OwnerHistory.Add(new OwnerPeriod(secondTower, otherNecromancers, 18, "founded"));
        world.Sites.AddRange([tower, secondTower]);

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
}

using System.Drawing;
using System.Reflection;
using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Events;
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
    public void GenerateCivColors_RefreshesBadgesCachedBeforeThePaletteWasAssigned()
    {
        using World world = new();
        var civilization = new Entity([], world)
        {
            Id = 40,
            Name = "The Silver Kingdom",
            IsCiv = true
        };
        var subsidiary = new Entity([], world)
        {
            Id = 41,
            Name = "Silver Kingdom Garrison",
            Parent = civilization
        };
        world.Entities.AddRange([civilization, subsidiary]);

        // Accessing links during parsing caches the initial uncolored badges.
        string oldCivilizationBadge = civilization.Icon;
        string oldGarrisonBadge = subsidiary.Icon;

        typeof(World).GetMethod("GenerateCivColors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(world, null);

        Assert.AreNotEqual(Color.Empty, civilization.LineColor);
        Assert.AreNotEqual(Color.Black.ToArgb(), civilization.LineColor.ToArgb());
        Assert.AreEqual(civilization.LineColor, subsidiary.LineColor);
        Assert.AreNotEqual(oldCivilizationBadge, civilization.Icon);
        Assert.AreNotEqual(oldGarrisonBadge, subsidiary.Icon);
        StringAssert.Contains(civilization.Icon, ColorTranslator.ToHtml(civilization.LineColor));
        StringAssert.Contains(subsidiary.Icon, ColorTranslator.ToHtml(subsidiary.LineColor));
    }

    [TestMethod]
    public void EntityBadge_RebuildsAfterColorChange()
    {
        using World world = new();
        var entity = new Entity([], world)
        {
            Id = 70, Name = "The Red Kingdom", IsCiv = true, LineColor = Color.Red
        };
        string red = entity.Icon;

        entity.LineColor = Color.Blue;

        Assert.AreNotEqual(red, entity.Icon);
        StringAssert.Contains(entity.Icon, ColorTranslator.ToHtml(Color.Blue));
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
    public void SiteMarker_ExportsChronologicalOwnershipForHistoryStepper()
    {
        using World world = new();
        var founders = new Entity([], world)
        {
            Id = 1, Name = "Ancient Kingdom", IsCiv = true, LineColor = Color.Red
        };
        var conquerors = new Entity([], world)
        {
            Id = 2, Name = "Later Kingdom", IsCiv = true, LineColor = Color.Blue
        };
        var site = new Site([], world) { Id = 100, Name = "Border Town" };
        site.OwnerHistory.Add(new OwnerPeriod(site, conquerors, 50, "conquest"));
        site.OwnerHistory.Add(new OwnerPeriod(site, founders, 10, "founded")
        {
            EndYear = 50
        });

        var marker = new SiteMarkerDto(site);

        Assert.AreEqual(2, marker.OwnerHistory.Count);
        Assert.AreEqual(10, marker.OwnerHistory[0].StartYear);
        Assert.AreEqual(50, marker.OwnerHistory[0].EndYear);
        Assert.AreEqual(founders.Id, marker.OwnerHistory[0].OwnerId);
        Assert.AreEqual("founded", marker.OwnerHistory[0].StartCause);
        Assert.AreNotEqual(marker.OwnerHistory[0].Color, marker.OwnerHistory[1].Color);
        Assert.AreEqual(50, marker.OwnerHistory[1].StartYear);
        Assert.AreEqual(conquerors.Id, marker.OwnerHistory[1].OwnerId);
        Assert.AreEqual(-1, marker.OwnerHistory[1].EndYear);
    }

    [TestMethod]
    public void SiteMarker_ExportsCreationDestructionAndOwnershipEndCauses()
    {
        using World world = new();
        var owner = new Entity([], world)
        {
            Id = 300, Name = "Old Kingdom", IsCiv = true, LineColor = Color.Red
        };
        var site = new Site([], world) { Id = 301, Name = "Abandoned Fortress" };
        site.OwnerHistory.Add(new OwnerPeriod(site, owner, 10, "constructed")
        {
            EndYear = 30,
            EndCause = "abandoned"
        });
        site.OwnerHistory.Add(new OwnerPeriod(site, owner, 60, "reclaimed")
        {
            EndYear = 80,
            EndCause = "destroyed"
        });

        var marker = new SiteMarkerDto(site);

        Assert.AreEqual(10, marker.CreatedYear);
        CollectionAssert.AreEqual(new List<int> { 80 }, marker.DestroyedYears);
        Assert.AreEqual("abandoned", marker.OwnerHistory[0].EndCause);
        Assert.AreEqual("destroyed", marker.OwnerHistory[1].EndCause);
    }

    [TestMethod]
    public void SiteMarker_UsesExplicitCreatedAndDestroyedSiteEvents()
    {
        using World world = new();
        var owner = new Entity([], world)
        {
            Id = 360, Name = "The Builders", IsCiv = true
        };
        var site = new Site([], world)
        {
            Id = 361, Name = "A Settlement"
        };
        world.Entities.Add(owner);
        world.Sites.Add(site);

        _ = new CreatedSite([
            new Property { Name = "id", Value = "362" },
            new Property { Name = "year", Value = "12" },
            new Property { Name = "site_id", Value = "361" },
            new Property { Name = "civ_id", Value = "360" },
        ], world);
        _ = new DestroyedSite([
            new Property { Name = "id", Value = "363" },
            new Property { Name = "year", Value = "55" },
            new Property { Name = "site_id", Value = "361" },
        ], world);

        var marker = new SiteMarkerDto(site);
        Assert.AreEqual(12, marker.CreatedYear);
        CollectionAssert.AreEqual(new List<int> { 55 }, marker.DestroyedYears);
        Assert.AreEqual("destroyed", marker.OwnerHistory.Last().EndCause);
    }

    [TestMethod]
    public void SiteMarker_DoesNotInferSiteCreationFromFirstRecordedConquest()
    {
        using World world = new();
        var owner = new Entity([], world)
        {
            Id = 350, Name = "Conquerors", IsCiv = true
        };
        var site = new Site([], world) { Id = 351, Name = "Older Settlement" };
        site.OwnerHistory.Add(new OwnerPeriod(site, owner, 100, "took over"));

        var marker = new SiteMarkerDto(site);

        Assert.IsNull(marker.CreatedYear);
        Assert.AreEqual(0, marker.DestroyedYears.Count);
    }

    [TestMethod]
    public void SiteMarkers_SubordinateNecromancerSitesUseTheirParentFaction()
    {
        using World world = new();
        var necromancer = new Entity([], world)
        {
            Id = 100, Name = "Tower of the Necromancer", LineColor = Color.Purple
        };
        var firstGarrison = new Entity([], world)
        {
            Id = 101, Name = "First Garrison", Parent = necromancer, LineColor = Color.Red
        };
        var secondGarrison = new Entity([], world)
        {
            Id = 102, Name = "Second Garrison", Parent = necromancer, LineColor = Color.Blue
        };
        var firstSite = new Site([], world) { Id = 11, Coordinates = [new Location(10, 10)] };
        var secondSite = new Site([], world) { Id = 12, Coordinates = [new Location(11, 10)] };
        firstSite.OwnerHistory.Add(new OwnerPeriod(firstSite, firstGarrison, 10, "founded"));
        secondSite.OwnerHistory.Add(new OwnerPeriod(secondSite, secondGarrison, 12, "founded"));
        world.Entities.AddRange([necromancer, firstGarrison, secondGarrison]);
        world.Sites.AddRange([firstSite, secondSite]);

        typeof(World).GetMethod("GenerateCivColors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(world, null);

        var firstMarker = new SiteMarkerDto(firstSite);
        var secondMarker = new SiteMarkerDto(secondSite);
        Assert.AreEqual(necromancer.Id, firstMarker.OwnerId);
        Assert.AreEqual(necromancer.Id, secondMarker.OwnerId);
        Assert.AreEqual(necromancer.Id, firstMarker.OriginOwnerId);
        Assert.AreEqual(necromancer.Id, secondMarker.OriginOwnerId);
        Assert.AreEqual(firstGarrison.Id, firstMarker.CurrentOwnerId);
        Assert.AreEqual(secondGarrison.Id, secondMarker.CurrentOwnerId);
        Assert.AreEqual(firstMarker.Color, secondMarker.Color);
        Assert.AreEqual(necromancer.LineColor, firstGarrison.LineColor);
        Assert.AreEqual(necromancer.LineColor, secondGarrison.LineColor);
    }

    [TestMethod]
    public void MapNation_CyclicAncestryTerminates()
    {
        using World world = new();
        var first = new Entity([], world) { Id = 201, Name = "First" };
        var second = new Entity([], world) { Id = 202, Name = "Second", Parent = first };
        first.Parent = second;

        Assert.IsNotNull(first.MapNation);
        Assert.IsNotNull(second.MapNation);
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

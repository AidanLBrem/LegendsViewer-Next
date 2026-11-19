using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class WorldCurrentYearTests
{
    [TestMethod]
    public void ImportedWorld_RecoversCurrentYearFromSiteOwnershipHistory()
    {
        using World world = new();
        Entity owner = new([], world) { Id = 1, Name = "Old Empire" };
        Site town = new([], world) { Id = 2, Name = "Old Town" };
        town.OwnerHistory.Add(new OwnerPeriod(town, owner, 12, "founded") { EndYear = 49 });
        town.OwnerHistory.Add(new OwnerPeriod(town, owner, 50, "reclaimed"));
        world.Sites.Add(town);

        Assert.AreEqual(0, world.CurrentYear);
        world.UpdateCurrentYearFromHistory();
        Assert.AreEqual(50, world.CurrentYear);
    }

    [TestMethod]
    public void ImportedWorld_UsesLaterEventCollectionYearWithoutMovingClockBackward()
    {
        using World world = new();
        War war = new([], world) { StartYear = 25, EndYear = 200 };
        world.EventCollections.Add(war);
        world.UpdateCurrentYearFromHistory();
        Assert.AreEqual(200, world.CurrentYear);

        world.CurrentYear = 250;
        world.UpdateCurrentYearFromHistory();
        Assert.AreEqual(250, world.CurrentYear);
    }
}

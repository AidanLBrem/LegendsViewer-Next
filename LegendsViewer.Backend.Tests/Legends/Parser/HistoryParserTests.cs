using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends.Parser;

[TestClass]
public class HistoryParserTests
{
    [TestMethod]
    public void ReadLeaders_AcceptsReignYearWithoutFollowingCommaAndSkipsMalformedEntries()
    {
        var world = new World();
        var civilization = new Entity([], world) { Name = "The Hammers of Oak" };
        var first = new HistoricalFigure { Id = 100, Name = "Boreg Ashguard" };
        var second = new HistoricalFigure { Id = 101, Name = "Tirgar Copper" };
        world.Entities.Add(civilization);
        world.HistoricalFigures.Add(first);
        world.HistoricalFigures.Add(second);

        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                "Testing World",
                "The Testing Plane",
                "Cave swallow men",
                "The Hammers of Oak, Dwarves",
                " king List",
                "  [*] Boreg Ashguard (b.???, Reign Began: 12), Original Line",
                "  [*] Incomplete Leader (b.???, Reign Began: not-a-year), Unknown",
                "  [*] Tirgar Copper (b.???, Reign Began: 20)",
            ]);

            using var parser = new HistoryParser(world, path);
            parser.Parse();

            Assert.AreEqual(1, civilization.Leaders.Count);
            CollectionAssert.AreEqual(
                new[] { first, second }, civilization.Leaders[0]);
            Assert.AreEqual(12, first.Positions?.Single().StartYear);
            Assert.AreEqual(20, second.Positions?.Single().StartYear);
            StringAssert.Contains(world.ParsingErrors.Print(), "Skipped malformed leader entry");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadLeaders_MissingClosingParenthesisDoesNotAbortLaterValidEntries()
    {
        var world = new World();
        var civilization = new Entity([], world) { Name = "The Gate of Stars" };
        var leader = new HistoricalFigure { Id = 102, Name = "Deler Earthpot" };
        world.Entities.Add(civilization);
        world.HistoricalFigures.Add(leader);

        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                "Testing World",
                "The Testing Plane",
                "Olm men",
                "The Gate of Stars, Dwarves",
                " List",
                " king List",
                "  [*] No Closing Paren (b.???, Reign Began: 17",
                "  [*] Deler Earthpot (b.???, Reign Began: -12)",
            ]);

            using var parser = new HistoryParser(world, path);
            parser.Parse();

            Assert.AreEqual(1, civilization.Leaders.Count);
            CollectionAssert.AreEqual(new[] { leader }, civilization.Leaders[0]);
            Assert.AreEqual(-12, leader.Positions?.Single().StartYear);
            StringAssert.Contains(world.ParsingErrors.Print(), "Skipped malformed leader list heading");
            StringAssert.Contains(world.ParsingErrors.Print(), "Skipped malformed leader entry");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class NationDiplomacyFactorsTests
{
    [TestMethod]
    public void SeparatesRecordedAllianceWarAndUnrelatedEvents()
    {
        using World world = new() { CurrentYear = 100 };
        Entity dwarves = new([], world) { Id = 1, Name = "Dwarves", IsCiv = true };
        Entity elves = new([], world) { Id = 2, Name = "Elves", IsCiv = true };
        Entity goblins = new([], world) { Id = 3, Name = "Goblins", IsCiv = true };

        War war = new([], world) { Id = 10, Name = "The Border War", StartYear = 50,
            Attacker = dwarves, Defender = elves };
        dwarves.Wars.Add(war);
        elves.Wars.Add(war);
        var alliance = new EntityAllianceFormed([], world)
        {
            Id = 20, Year = 20, InitiatingEntity = dwarves, JoiningEntity = elves
        };
        dwarves.Events.Add(alliance);
        elves.Events.Add(alliance); // Same historical event must not be double-counted.
        dwarves.Events.Add(new FieldBattle([], world)
        {
            Id = 30, Year = 75, Attacker = dwarves, Defender = goblins
        });

        var factors = NationDiplomacyFactorsDto.FromEntities(dwarves, elves);

        Assert.IsFalse(factors.ExactScoresAvailable);
        Assert.AreEqual(2, factors.Historical.Count);
        Assert.AreEqual(1, factors.Historical.Count(entry => entry.Kind == "hostile"));
        Assert.AreEqual(1, factors.Historical.Count(entry => entry.Kind == "cooperative"));
        Assert.IsTrue(factors.Historical.Any(entry => entry.Link == "/war/10"));
        Assert.IsFalse(factors.Historical.Any(entry => entry.Description.Contains("Goblins")));
    }

    [TestMethod]
    public void SharedDeityIsContextNotAnInventedNumericScore()
    {
        using World world = new();
        Entity first = new([], world) { Id = 1, IsCiv = true };
        Entity second = new([], world) { Id = 2, IsCiv = true };
        Entity third = new([], world) { Id = 3, IsCiv = true };
        HistoricalFigure deity = new([], world) { Id = 90, Name = "The Sun" };

        first.Worshipped.Add(deity);
        second.Worshipped.Add(deity);

        var shared = NationDiplomacyFactorsDto.FromEntities(first, second);
        var missing = NationDiplomacyFactorsDto.FromEntities(first, third);

        Assert.AreEqual(1, shared.Personal.Count);
        Assert.AreEqual("context", shared.Personal[0].Kind);
        Assert.AreEqual("/hf/90", shared.Personal[0].Link);
        Assert.AreEqual(0, missing.Personal.Count);
        Assert.AreEqual(0, shared.Historical.Count);
    }

    [TestMethod]
    public void PeaceAndSiteAttacksAreOnlyAttributedToNamedOpponents()
    {
        using World world = new();
        Entity first = new([], world) { Id = 1, Name = "First", IsCiv = true };
        Entity second = new([], world) { Id = 2, Name = "Second", IsCiv = true };
        Entity third = new([], world) { Id = 3, Name = "Third", IsCiv = true };

        first.Events.Add(new AttackedSite([], world) { Year = 7, Attacker = first, Defender = second });
        first.Events.Add(new PeaceAccepted([], world) { Year = 8, Source = first, Destination = second });
        first.Events.Add(new PeaceRejected([], world) { Year = 9, Source = first, Destination = third });

        var factors = NationDiplomacyFactorsDto.FromEntities(first, second);

        Assert.AreEqual(2, factors.Historical.Count);
        Assert.IsTrue(factors.Historical.Any(entry => entry.Kind == "hostile" && entry.Year == 7));
        Assert.IsTrue(factors.Historical.Any(entry => entry.Kind == "cooperative" && entry.Year == 8));
        Assert.IsFalse(factors.Historical.Any(entry => entry.Year == 9));
    }
}

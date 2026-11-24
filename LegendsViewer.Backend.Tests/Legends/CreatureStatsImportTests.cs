using LegendsViewer.Backend.Legends.Various;

namespace LegendsViewer.Backend.Tests.Legends;

[TestClass]
public class CreatureStatsImportTests
{
    [TestMethod]
    public void RecordLabelUsesGeneratedDescriptionAndHidesDefaultCaste()
    {
        var race = new CreatureInfo("FORGOTTEN_BEAST_14") { NameSingular = "Forgotten Beast" };
        Assert.AreEqual("Forgotten Beast", race.RecordLabel("Default"));
        CreatureStatsImport.Apply("""
        {"schema_version":1,"creatures":[{"creature_id":"FORGOTTEN_BEAST_14","castes":[
        {"caste_id":"DEFAULT","adult_body_size":100000,"native_army_strength":2000,
         "description":"A gigantic feathered serpent."}]}]}
        """, new[] { race });
        Assert.AreEqual("Forgotten Beast — A gigantic feathered serpent.", race.RecordLabel("Default"));
        Assert.AreEqual("Dwarf · Female", new CreatureInfo("DWARF") { NameSingular = "Dwarf" }.RecordLabel("Female"));
    }

    [TestMethod]
    public void ImportsGeneratedCreatureAndPreservesCasteDifferences()
    {
        var race = new CreatureInfo("FORGOTTEN_BEAST_14");
        var otherWorld = new CreatureInfo("FORGOTTEN_BEAST_14");
        string json = """
        {"schema_version":1,"creatures":[{"creature_id":"FORGOTTEN_BEAST_14","castes":[
        {"caste_id":"MALE","adult_body_size":100000,"native_army_strength":2000},
        {"caste_id":"FEMALE","adult_body_size":200000,"native_army_strength":4000}]}]}
        """;
        Assert.AreEqual(1, CreatureStatsImport.Apply(json, new[] { race }));
        Assert.AreEqual(150000, race.AdultBodySize!.Value);
        Assert.AreEqual(4000, race.NativeForCaste("female")!.Value);
        Assert.AreEqual(100000, race.SizeForCaste("MALE")!.Value);
        Assert.IsNull(race.NativeCombatRating);
        Assert.IsNull(otherWorld.AdultBodySize);
        Assert.IsNull(otherWorld.NativeForCaste("male"));
    }
}

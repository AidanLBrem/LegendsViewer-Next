using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegendsViewer.Backend.Legends.Various;

public record CreatureCasteStats(
    [property: JsonPropertyName("caste_id")] string CasteId,
    [property: JsonPropertyName("adult_body_size")] int? AdultBodySize,
    [property: JsonPropertyName("native_army_strength")] int? NativeArmyStrength,
    [property: JsonPropertyName("description")] string? Description = null);

public static class CreatureStatsImport
{
    private sealed record Export(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("creatures")] CreatureEntry[] Creatures);
    private sealed record CreatureEntry(
        [property: JsonPropertyName("creature_id")] string CreatureId,
        [property: JsonPropertyName("castes")] CreatureCasteStats[] Castes);

    public static int Apply(string json, IEnumerable<CreatureInfo> creatures)
    {
        var export = JsonSerializer.Deserialize<Export>(json);
        if (export?.SchemaVersion != 1 || export.Creatures == null)
            throw new JsonException("Unsupported creature statistics export.");
        var entries = new Dictionary<string, CreatureEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in export.Creatures)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.CreatureId) || entry.Castes == null)
                throw new JsonException("Invalid creature entry.");
            if (!entries.TryAdd(entry.CreatureId, entry))
                throw new JsonException("Duplicate creature ID.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var caste in entry.Castes)
                if (caste == null || string.IsNullOrWhiteSpace(caste.CasteId) || !ids.Add(caste.CasteId))
                    throw new JsonException("Invalid or duplicate caste ID.");
        }

        int count = 0;
        foreach (var creature in creatures)
        {
            if (ReferenceEquals(creature, CreatureInfo.Unknown) || !entries.TryGetValue(creature.Id, out var entry)) continue;
            creature.CasteStats.Clear();
            foreach (var caste in entry.Castes)
                creature.CasteStats.Add(caste.CasteId, caste with {
                    AdultBodySize = caste.AdultBodySize is > 0 ? caste.AdultBodySize : null,
                    NativeArmyStrength = caste.NativeArmyStrength is > 0 ? caste.NativeArmyStrength : null
                });
            // Squads lack caste data: use the mean adult size if all castes have it.
            var castes = creature.CasteStats.Values.ToArray();
            if (castes.Length > 0 && castes.All(c => c.AdultBodySize > 0))
                creature.AdultBodySize = (int)Math.Round(castes.Average(c => (double)c.AdultBodySize!.Value));
            // Never guess a native rating for mixed-caste squads.
            creature.NativeCombatRating = castes.Length > 0 && castes.All(c => c.NativeArmyStrength > 0)
                && castes.Select(c => c.NativeArmyStrength).Distinct().Count() == 1
                ? castes[0].NativeArmyStrength : null;
            count++;
        }
        return count;
    }
}

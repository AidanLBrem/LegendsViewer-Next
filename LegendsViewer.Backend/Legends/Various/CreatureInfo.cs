using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Utilities;

namespace LegendsViewer.Backend.Legends.Various;

public class CreatureInfo
{
    public static readonly CreatureInfo Unknown = new("UNKNOWN");
    public string Id { get; set; } = string.Empty;

    // Optional native adult caste rating; export only when all relevant castes agree.
    public int? NativeCombatRating { get; set; }
    public int? AdultBodySize { get; set; }
    public Dictionary<string, CreatureCasteStats> CasteStats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int? KnownAdultSize => AdultBodySize ?? VanillaCombatSizes.Find(Id);
    public int? SizeForCaste(string caste) => CasteStats.TryGetValue(caste ?? string.Empty, out var stats) ? stats.AdultBodySize ?? KnownAdultSize : KnownAdultSize;
    public int? NativeForCaste(string caste) => CasteStats.TryGetValue(caste ?? string.Empty, out var stats) ? stats.NativeArmyStrength ?? NativeCombatRating : NativeCombatRating;
    public int? EstimatedCombatRating
    {
        get
        {
            int? size = KnownAdultSize;
            return size is > 0 ? Math.Max(1, size.Value / 50) : null;
        }
    }

    public string NameSingular { get; set; } = string.Empty;
    public string NamePlural { get; set; } = string.Empty;

    public CreatureInfo(List<Property> properties, IWorld world)
    {
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "creature_id": Id = string.Intern(property.Value); break;
                case "native_combat_rating":
                    NativeCombatRating = int.TryParse(property.Value, out int rating) && rating > 0 ? rating : null;
                    break;
                case "adult_body_size":
                    AdultBodySize = int.TryParse(property.Value, out int size) && size > 0 ? size : null;
                    break;
                case "name_singular": NameSingular = string.Intern(Formatting.FormatRace(property.Value)); break;
                case "name_plural": NamePlural = string.Intern(Formatting.FormatRace(property.Value)); break;
                // TODO read the other properties and use them
                default:
                    property.Known = true;
                    break;
            }
        }
    }

    public CreatureInfo(string identifier)
    {
        Id = identifier.ToLower();
        NameSingular = string.Intern(Formatting.FormatRace(identifier));
        NamePlural = string.Intern(Formatting.MakePopulationPlural(NameSingular));
    }

    public override string ToString()
    {
        return NameSingular ?? NamePlural ?? string.Empty;
    }
}



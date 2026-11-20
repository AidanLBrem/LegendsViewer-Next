using LegendsViewer.Backend.Legends.Parser;

namespace LegendsViewer.Backend.Legends.Various;

/// <summary>Retains export fields, including unfamiliar objectives and nested targets.</summary>
public record IntrigueField(string Name, string Value, List<IntrigueField> Children)
{
    public static List<IntrigueField> Read(List<Property> properties) => properties.Select(p =>
        new IntrigueField(p.Name, p.Value, p.SubProperties == null ? [] : Read(p.SubProperties))).ToList();

    public static bool Flag(Property property) => property.Value.Trim().ToLowerInvariant() is not ("false" or "0" or "no");
}

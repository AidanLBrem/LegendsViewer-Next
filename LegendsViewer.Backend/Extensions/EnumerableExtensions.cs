using System.Reflection;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> SortByProperty<T>(this IEnumerable<T> source, string? sortKey, string? sortOrder)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (sortKey == null || sortOrder == null)
        {
            return source;
        }

        // Overview rows expose a formatted HTML link under "html", but their
        // backing WorldObject has a plain Name instead. Sort by that text,
        // not by presentation markup or a nonexistent reflection property.
        // This applies to all WorldObject listing endpoints, including the
        // Factions and Groups Overview and event-collection listings.
        string propertyName = typeof(WorldObject).IsAssignableFrom(typeof(T)) &&
            sortKey.Equals("html", StringComparison.OrdinalIgnoreCase)
            ? nameof(WorldObject.Name) : sortKey;

        PropertyInfo? propertyInfo = typeof(T).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (propertyInfo == null)
        {
            throw new ArgumentException($"Property '{sortKey}' not found on type '{typeof(T).Name}'");
        }

        // Unknown population is not zero: always place missing values last,
        // regardless of direction, before paginating the nation overview.
        if (typeof(Entity).IsAssignableFrom(typeof(T)) &&
            propertyName.Equals(nameof(Entity.PopulationCount), StringComparison.OrdinalIgnoreCase))
        {
            var withPopulation = source.Select(item =>
                (Item: item, Count: (int?)propertyInfo.GetValue(item))).ToList();
            return sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? withPopulation.OrderBy(x => x.Count == null).ThenByDescending(x => x.Count).Select(x => x.Item)
                : withPopulation.OrderBy(x => x.Count == null).ThenBy(x => x.Count).Select(x => x.Item);
        }

        // Sort in ascending or descending order based on sortOrder
        if (sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            return source.OrderByDescending(x => propertyInfo.GetValue(x, null));
        }
        else
        {
            return source.OrderBy(x => propertyInfo.GetValue(x, null));
        }
    }
}

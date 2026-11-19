using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;

namespace LegendsViewer.Backend.Contracts;

/// <summary>
/// Lightweight row for the shared paginated overview tables.
/// Warfare-only numeric columns are null on other world object types.
/// </summary>
public class WorldObjectDto
{
    public WorldObjectDto(WorldObject worldObject, DwarfObject? pointOfView = null)
    {
        Id = worldObject.Id;
        StartDate = (worldObject as EventCollection)?.StartDate ?? string.Empty;
        EndDate = (worldObject as EventCollection)?.EndDate ?? string.Empty;
        Name = worldObject.Name;
        Type = worldObject.Type;
        Subtype = worldObject.Subtype;
        Html = worldObject.ToLink(true, pointOfView);
        EventCount = worldObject.EventCount;
        EventCollectionCount = worldObject.EventCollectionCount;

        WarfareSiteOutcomesDto? sites = null;
        if (worldObject is Battle battle)
        {
            DeathCount = battle.DeathCount;
            AttackerDeathCount = battle.AttackerDeathCount;
            DefenderDeathCount = battle.DefenderDeathCount;
            sites = battle.SiteOutcomes;
        }
        else if (worldObject is War war)
        {
            DeathCount = war.DeathCount;
            AttackerDeathCount = war.AttackerDeathCount;
            DefenderDeathCount = war.DefenderDeathCount;
            sites = war.SiteOutcomes;
        }

        if (sites != null)
        {
            AttackerSitesGained = sites.AttackerGained.Count;
            AttackerSitesLost = sites.AttackerLost.Count;
            AttackerSitesPlundered = sites.AttackerPlundered.Count;
            DefenderSitesGained = sites.DefenderGained.Count;
            DefenderSitesLost = sites.DefenderLost.Count;
            DefenderSitesPlundered = sites.DefenderPlundered.Count;
        }
    }

    public int Id { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public string Name { get; set; }
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public string Html { get; set; }
    public int EventCount { get; set; }
    public int EventCollectionCount { get; set; }

    public int? DeathCount { get; set; }
    public int? AttackerDeathCount { get; set; }
    public int? DefenderDeathCount { get; set; }
    public int? AttackerSitesGained { get; set; }
    public int? AttackerSitesLost { get; set; }
    public int? AttackerSitesPlundered { get; set; }
    public int? DefenderSitesGained { get; set; }
    public int? DefenderSitesLost { get; set; }
    public int? DefenderSitesPlundered { get; set; }
}

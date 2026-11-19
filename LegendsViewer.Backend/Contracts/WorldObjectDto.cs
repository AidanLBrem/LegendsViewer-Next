using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;

namespace LegendsViewer.Backend.Contracts;

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

        // Add numeric fields to the paginated row, instead of returning only
        // the full detail object. Unrelated object types leave them null.
        if (worldObject is War war)
        {
            DeathCount = war.DeathCount;
            AttackerDeathCount = war.AttackerDeathCount;
            DefenderDeathCount = war.DefenderDeathCount;
            SetSiteCounts(war.SiteOutcomes);
        }
        else if (worldObject is Battle battle)
        {
            DeathCount = battle.DeathCount;
            AttackerDeathCount = battle.AttackerDeathCount;
            DefenderDeathCount = battle.DefenderDeathCount;
            SetSiteCounts(battle.SiteOutcomes);
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

    public int? AttackerSitesGainedCount { get; set; }
    public int? AttackerSitesLostCount { get; set; }
    public int? AttackerSitesPlunderedCount { get; set; }
    public int? DefenderSitesGainedCount { get; set; }
    public int? DefenderSitesLostCount { get; set; }
    public int? DefenderSitesPlunderedCount { get; set; }

    private void SetSiteCounts(WarfareSiteOutcomesDto sites)
    {
        AttackerSitesGainedCount = sites.AttackerGained.Count;
        AttackerSitesLostCount = sites.AttackerLost.Count;
        AttackerSitesPlunderedCount = sites.AttackerPlundered.Count;
        DefenderSitesGainedCount = sites.DefenderGained.Count;
        DefenderSitesLostCount = sites.DefenderLost.Count;
        DefenderSitesPlunderedCount = sites.DefenderPlundered.Count;
    }
}

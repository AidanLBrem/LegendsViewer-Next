using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.WorldObjects;

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
            AttackerTroopCount = war.AttackerBattleDeployments;
            DefenderTroopCount = war.DefenderBattleDeployments;
            SetSiteCounts(war.SiteOutcomes);
        }
        else if (worldObject is Battle battle)
        {
            DeathCount = battle.DeathCount;
            AttackerDeathCount = battle.AttackerDeathCount;
            DefenderDeathCount = battle.DefenderDeathCount;
            AttackerTroopCount = battle.AttackerCount;
            DefenderTroopCount = battle.DefenderCount;
            var strength = battle.ArmyStrength;
            AttackerArmyStrength = strength.Attacker;
            DefenderArmyStrength = strength.Defender;
            ArmyStrengthBasis = strength.Basis;
            AttackerRatingCoverage = strength.AttackerEstimate.Coverage;
            DefenderRatingCoverage = strength.DefenderEstimate.Coverage;
            ArmyStrengthRatio = strength.Defender > 0 ? strength.Attacker / strength.Defender : null;
            SetSiteCounts(battle.SiteOutcomes);
        }
        else if (worldObject is Entity entity)
        {
            PopulationCount = entity.PopulationCount;
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
    public int? PopulationCount { get; set; }

    public int? DeathCount { get; set; }
    public int? AttackerDeathCount { get; set; }
    public int? DefenderDeathCount { get; set; }
    // For battles: starting combatants. For wars: sum of recorded battle
    // deployments (not unique troops or a war-start snapshot).
    public int? AttackerTroopCount { get; set; }
    public int? DefenderTroopCount { get; set; }

    public double? AttackerArmyStrength { get; set; }
    public double? DefenderArmyStrength { get; set; }
    public string? ArmyStrengthBasis { get; set; }
    public double? ArmyStrengthRatio { get; set; }
    public double? AttackerRatingCoverage { get; set; }
    public double? DefenderRatingCoverage { get; set; }

    public int? AttackerSitesGainedCount { get; set; }
    public int? AttackerSitesLostCount { get; set; }
    public int? AttackerSitesPlunderedCount { get; set; }
    public int? DefenderSitesGainedCount { get; set; }
    public int? DefenderSitesLostCount { get; set; }
    public int? DefenderSitesPlunderedCount { get; set; }
    public int? UnattributedSitesCount { get; set; }

    private void SetSiteCounts(WarfareSiteOutcomesDto sites)
    {
        AttackerSitesGainedCount = sites.AttackerGained.Count;
        AttackerSitesLostCount = sites.AttackerLost.Count;
        AttackerSitesPlunderedCount = sites.AttackerPlundered.Count;
        DefenderSitesGainedCount = sites.DefenderGained.Count;
        DefenderSitesLostCount = sites.DefenderLost.Count;
        DefenderSitesPlunderedCount = sites.DefenderPlundered.Count;
        UnattributedSitesCount = sites.Unattributed.Count;
    }
}

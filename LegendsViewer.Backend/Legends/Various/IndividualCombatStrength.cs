using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Legends.Various;

/// <summary>
/// Export-based estimate, not the complete native historical-figure rating.
/// See docs/individual-combat-strength.md for verified arithmetic and assumptions.
/// </summary>
public static class IndividualCombatStrength
{
    private static readonly HashSet<string> MaterialSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "AXE", "SWORD", "DAGGER", "MACE", "HAMMER", "SPEAR", "CROSSBOW",
        "SHIELD", "ARMOR", "PIKE", "WHIP", "BOW", "BLOWGUN"
    };
    private static readonly HashSet<string> DefensiveSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "DISCIPLINE", "SITUATIONAL_AWARENESS", "WRESTLING", "BITE", "GRASP_STRIKE",
        "STANCE_STRIKE", "DODGING"
    };

    public const int AssumedMaterialBonus = 10;
    public record SkillPoints(long Material, long Defensive)
    {
        public long Total => Material + Defensive;
    }

    public static SkillPoints Points(IEnumerable<Skill> skills)
    {
        long material = 0, defensive = 0;
        // Export normally has one entry per token. Avoid inflating duplicate imports.
        foreach (var skill in skills.GroupBy(s => s.Name.Replace(' ', '_'), StringComparer.OrdinalIgnoreCase))
        {
            int points = Math.Max(0, skill.Max(s => s.Points));
            if (MaterialSkills.Contains(skill.Key)) material += points;
            if (DefensiveSkills.Contains(skill.Key)) defensive += points;
        }
        return new(material, defensive);
    }

    public static bool SupportsEstimate(HistoricalFigure figure) =>
        !figure.Animated && !figure.Zombie && !figure.Skeleton && figure.UndeadTypes.Count == 0 &&
        string.IsNullOrEmpty(figure.AnimatedType) && figure.CreatureTypes.Count == 0 &&
        string.IsNullOrEmpty(figure.PreviousRace);

    // Native positive-IP arithmetic: min(80, 40 + trunc(IP / 500)).
    public static long SkillFactor(long points) => 40 + Math.Min(40, Math.Max(0, points) / 500);

    public static double Estimate(double casteRating, IEnumerable<Skill> skills)
    {
        var points = Points(skills);
        // Only add the training increment over assumed untrained equipment.
        // Equipment itself is absent from exports; the 10-point proxy is a model choice.
        long trainingBonus = AssumedMaterialBonus * SkillFactor(points.Material) / 40 - AssumedMaterialBonus;
        return Math.Floor((casteRating + trainingBonus) * SkillFactor(points.Defensive) / 40);
    }
}

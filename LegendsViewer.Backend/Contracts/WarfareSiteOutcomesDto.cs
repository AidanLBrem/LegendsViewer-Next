using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Contracts;

/// <summary>
/// Site outcomes attributed to the two sides of a battle or war.
/// A conquest changes possession; destruction only loses a site; pillaging
/// records a raid, not a change of owner. Multiple actions on the same site
/// in one category are displayed once.
/// </summary>
public class WarfareSiteOutcomesDto
{
    public List<string> AttackerGained { get; set; } = [];
    public List<string> AttackerLost { get; set; } = [];
    public List<string> AttackerPlundered { get; set; } = [];
    public List<string> DefenderGained { get; set; } = [];
    public List<string> DefenderLost { get; set; } = [];
    public List<string> DefenderPlundered { get; set; } = [];

    public static WarfareSiteOutcomesDto FromConquerings(
        IEnumerable<SiteConquered> conquests, Entity? attacker, Entity? defender)
    {
        WarfareSiteOutcomesDto result = new();
        HashSet<int> attackerGainedIds = [];
        HashSet<int> attackerLostIds = [];
        HashSet<int> attackerPlunderedIds = [];
        HashSet<int> defenderGainedIds = [];
        HashSet<int> defenderLostIds = [];
        HashSet<int> defenderPlunderedIds = [];

        foreach (SiteConquered conquest in conquests.Distinct())
        {
            if (conquest.Site is not Site site)
            {
                continue;
            }

            bool actionByAttacker = IsSameSide(conquest.Attacker, attacker);
            bool actionByDefender = IsSameSide(conquest.Attacker, defender);

            // Some site events omit the attacking party. The defending party
            // can still identify the opposing side when both war sides exist.
            if (!actionByAttacker && !actionByDefender && attacker != null && defender != null)
            {
                actionByAttacker = IsSameSide(conquest.Defender, defender);
                actionByDefender = IsSameSide(conquest.Defender, attacker);
            }

            // A missing or ambiguous affiliation is not evidence of ownership
            // changing for either side.
            if (actionByAttacker == actionByDefender)
            {
                continue;
            }

            switch (conquest.ConquerType)
            {
                case SiteConqueredType.Conquest:
                    if (actionByAttacker)
                    {
                        AddSite(result.AttackerGained, attackerGainedIds, site);
                        AddSite(result.DefenderLost, defenderLostIds, site);
                    }
                    else
                    {
                        AddSite(result.DefenderGained, defenderGainedIds, site);
                        AddSite(result.AttackerLost, attackerLostIds, site);
                    }
                    break;

                case SiteConqueredType.Destruction:
                    if (actionByAttacker)
                    {
                        AddSite(result.DefenderLost, defenderLostIds, site);
                    }
                    else
                    {
                        AddSite(result.AttackerLost, attackerLostIds, site);
                    }
                    break;

                case SiteConqueredType.Pillaging:
                    if (actionByAttacker)
                    {
                        AddSite(result.AttackerPlundered, attackerPlunderedIds, site);
                    }
                    else
                    {
                        AddSite(result.DefenderPlundered, defenderPlunderedIds, site);
                    }
                    break;
            }
        }

        return result;
    }

    private static bool IsSameSide(Entity? participant, Entity? side)
    {
        return participant != null && side != null &&
            (participant == side ||
             participant.EqualsOrParentEquals(side) ||
             participant.MapNation == side.MapNation);
    }

    private static void AddSite(List<string> links, HashSet<int> ids, Site site)
    {
        if (ids.Add(site.Id))
        {
            links.Add(site.ToLink());
        }
    }
}

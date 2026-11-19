using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Contracts;

// These metrics describe recorded behavior, not the game's intrinsic civilization
// values or ethics. No unrecorded actions are inferred from an absence of events.
public sealed class NationCulturePeriodDto
{
    public int WindowYears { get; set; }
    public string Character { get; set; } = "";
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public int ObservedYears { get; set; }
    public int Conquests { get; set; }
    public int SettlementsFounded { get; set; }
    public int SitesReclaimed { get; set; }
    public int SitesLost { get; set; }
    public int OffensiveOperations { get; set; }
    public int DefensiveOperations { get; set; }
    public int WarsInitiated { get; set; }
    public int WarsDefended { get; set; }
    public int SitesPlundered { get; set; }
    public int PeaceAgreements { get; set; }
    public int Alliances { get; set; }
    public int InternalUpheavals { get; set; }
    public int HarshEdictsIssued { get; set; }
    public int HarshEdictsRevoked { get; set; }
    public double ExpansionPerCentury { get; set; }
    public double OffensiveOperationsPerCentury { get; set; }
    public double PlundersPerCentury { get; set; }
    public double DiplomacyPerCentury { get; set; }
    public double UpheavalsPerCentury { get; set; }
}

public sealed class NationCultureEvidenceDto
{
    public int Year { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Link { get; set; }
}

public sealed class NationCultureTimelineDto
{
    public int Year { get; set; }
    public int Conquests { get; set; }
    public int SettlementsFounded { get; set; }
    public int OffensiveOperations { get; set; }
    public int PeaceAgreements { get; set; }
}

public sealed class NationCultureSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public int CurrentSites { get; set; }
    public string Character { get; set; } = "";
    public List<NationCulturePeriodDto> Periods { get; set; } = [];
}

public sealed class NationCultureProfileDto
{
    public int EntityId { get; set; }
    public int CurrentYear { get; set; }
    public string Character { get; set; } = "";
    public int WorshippedDeities { get; set; }
    public bool IntrinsicValuesAvailable { get; set; }
    public string IntrinsicValuesNote { get; set; } =
        "Civilization values and ethics are not parsed by this Legends Viewer importer. These are observed historical actions, not innate traits.";
    public List<NationCulturePeriodDto> Periods { get; set; } = [];
    public List<NationCultureTimelineDto> Timeline { get; set; } = [];
    public List<NationCultureEvidenceDto> Evidence { get; set; } = [];

    private sealed record Fact(int Year, string Category, string Description, string? Link);

    public static NationCultureProfileDto FromEntity(Entity nation)
    {
        var facts = new List<Fact>();
        void Add(int year, string category, string description, string? link = null)
        {
            if (year >= 0)
            {
                facts.Add(new Fact(year, category, description, link));
            }
        }

        // The owning civilization also receives site periods from subordinate
        // site governments. Deduplicate by site, year and outcome.
        var seenSites = new HashSet<(int SiteId, int Year, string Category)>();
        foreach (var period in nation.SiteHistory)
        {
            if (period.Site == null)
            {
                continue;
            }
            string cause = period.StartCause?.Trim().ToLowerInvariant() ?? "";
            string? category = cause switch
            {
                "took over" or "taken over" or "conquered" => "conquest",
                "founded" or "constructed" => "settlement",
                "reclaimed" or "unretired" => "reclaim",
                _ => null
            };
            if (category != null && period.StartYear >= 0 &&
                seenSites.Add((period.Site.Id, period.StartYear, category)))
            {
                Add(period.StartYear, category, $"{period.Site.Name}: {cause}",
                    $"/site/{period.Site.Id}");
            }
            if (period.EndYear >= 0 &&
                seenSites.Add((period.Site.Id, period.EndYear, "loss")))
            {
                Add(period.EndYear, "loss",
                    $"{period.Site.Name}: {period.EndCause ?? "ownership ended"}",
                    $"/site/{period.Site.Id}");
            }
        }

        // Historical event actors are checked explicitly: appearing in a
        // site's history does not mean the nation initiated that conflict.
        static bool SameNation(Entity? actor, Entity nation) =>
            actor != null && (actor == nation || actor.CurrentCiv == nation);

        foreach (var entry in nation.Events.Distinct())
        {
            switch (entry)
            {
                case EntityCreated founded when SameNation(founded.Entity, nation):
                    Add(entry.Year, "origin", "Civilization founded");
                    break;
                case EntityLaw law when SameNation(law.Entity, nation) && law.Law == EntityLawType.Harsh:
                    Add(entry.Year, law.LawLaid ? "harshLaw" : "lawRepealed",
                        law.LawLaid ? "Harsh edicts issued" : "Harsh edicts repealed");
                    break;
                case AttackedSite attack:
                    if (SameNation(attack.Attacker, nation))
                    {
                        Add(entry.Year, "offensive", $"Attack on {attack.Site?.Name ?? "a site"}",
                            attack.Site == null ? null : $"/site/{attack.Site.Id}");
                    }
                    else if (SameNation(attack.Defender, nation))
                    {
                        Add(entry.Year, "defensive", $"Defended {attack.Site?.Name ?? "a site"}",
                            attack.Site == null ? null : $"/site/{attack.Site.Id}");
                    }
                    break;
                case FieldBattle battle:
                    if (SameNation(battle.Attacker, nation))
                    {
                        Add(entry.Year, "offensive", "Offensive field battle");
                    }
                    else if (SameNation(battle.Defender, nation))
                    {
                        Add(entry.Year, "defensive", "Defensive field battle");
                    }
                    break;
                case PlunderedSite plunder when SameNation(plunder.Attacker, nation):
                    Add(entry.Year, "plunder", $"Plundered {plunder.Site?.Name ?? "a site"}",
                        plunder.Site == null ? null : $"/site/{plunder.Site.Id}");
                    break;
                case PeaceAccepted peace when SameNation(peace.Source, nation) ||
                                              SameNation(peace.Destination, nation):
                    Add(entry.Year, "peace", "Peace offer accepted");
                    break;
                case EntityAllianceFormed alliance when SameNation(alliance.InitiatingEntity, nation) ||
                                                        SameNation(alliance.JoiningEntity, nation):
                    Add(entry.Year, "alliance", "Alliance formed");
                    break;
                case InsurrectionStarted rebellion when SameNation(rebellion.Civ, nation):
                    Add(entry.Year, "upheaval", "Insurrection");
                    break;
                case EntityOverthrown coup when SameNation(coup.Entity, nation):
                    Add(entry.Year, "upheaval", "Government overthrown");
                    break;
            }
        }

        foreach (var war in nation.Wars.Distinct())
        {
            if (war.StartYear < 0)
            {
                continue;
            }
            if (SameNation(war.Attacker, nation))
            {
                Add(war.StartYear, "warInitiated", $"War begun: {war.Name}", $"/war/{war.Id}");
            }
            else if (SameNation(war.Defender, nation))
            {
                Add(war.StartYear, "warDefended", $"War defended: {war.Name}", $"/war/{war.Id}");
            }
        }

        int latestFactYear = facts.Count == 0 ? 0 : facts.Max(f => f.Year);
        int currentYear = Math.Max(nation.World?.CurrentYear ?? 0, latestFactYear);
        int firstYear = facts.Count == 0 ? currentYear : facts.Min(f => f.Year);

        NationCulturePeriodDto Period(int windowYears)
        {
            int start = windowYears == 0
                ? firstYear
                : Math.Max(firstYear, Math.Max(0, currentYear - windowYears + 1));
            var entries = facts.Where(f => f.Year >= start && f.Year <= currentYear).ToList();
            int Count(string category) => entries.Count(f => f.Category == category);
            int duration = Math.Max(1, currentYear - start + 1);
            double Rate(int count) => Math.Round(count * 100.0 / duration, 2);
            int conquests = Count("conquest");
            int settlements = Count("settlement");
            int offense = Count("offensive");
            int plunders = Count("plunder");
            int peace = Count("peace");
            int alliances = Count("alliance");
            int upheavals = Count("upheaval");
            string character = conquests >= 2 && conquests >= 2 * settlements
                ? "Conquest-oriented"
                : settlements >= 2 && settlements >= 2 * conquests
                    ? "Settlement-oriented"
                    : offense >= 3 && offense > Count("defensive")
                        ? "Offensively active"
                        : Count("defensive") >= 3 && Count("defensive") > offense
                            ? "Defensively engaged"
                            : peace + alliances >= 2 && peace + alliances >= offense
                                ? "Diplomatically active"
                                : entries.Any(f => f.Category != "origin")
                                    ? "Mixed recorded activity"
                                    : "Insufficient recorded activity";
            return new NationCulturePeriodDto
            {
                WindowYears = windowYears,
                Character = character,
                StartYear = start,
                EndYear = currentYear,
                ObservedYears = duration,
                Conquests = conquests,
                SettlementsFounded = settlements,
                SitesReclaimed = Count("reclaim"),
                SitesLost = Count("loss"),
                OffensiveOperations = offense,
                DefensiveOperations = Count("defensive"),
                WarsInitiated = Count("warInitiated"),
                WarsDefended = Count("warDefended"),
                SitesPlundered = plunders,
                PeaceAgreements = peace,
                Alliances = alliances,
                InternalUpheavals = upheavals,
                HarshEdictsIssued = Count("harshLaw"),
                HarshEdictsRevoked = Count("lawRepealed"),
                ExpansionPerCentury = Rate(conquests + settlements),
                OffensiveOperationsPerCentury = Rate(offense),
                PlundersPerCentury = Rate(plunders),
                DiplomacyPerCentury = Rate(peace + alliances),
                UpheavalsPerCentury = Rate(upheavals)
            };
        }

        var all = Period(0);
        var byDecade = facts.GroupBy(f => f.Year / 10 * 10)
            .ToDictionary(g => g.Key, g => g.ToList());
        var timeline = new List<NationCultureTimelineDto>();
        for (int year = firstYear / 10 * 10; year <= currentYear; year += 10)
        {
            byDecade.TryGetValue(year, out var decade);
            int Count(string category) => decade?.Count(f => f.Category == category) ?? 0;
            timeline.Add(new NationCultureTimelineDto
            {
                Year = year,
                Conquests = Count("conquest"),
                SettlementsFounded = Count("settlement"),
                OffensiveOperations = Count("offensive"),
                PeaceAgreements = Count("peace")
            });
        }

        return new NationCultureProfileDto
        {
            EntityId = nation.Id,
            CurrentYear = currentYear,
            Character = all.Character,
            WorshippedDeities = nation.Worshipped.Count,
            Periods = [all, Period(100), Period(25)],
            Timeline = timeline,
            // The latest 500 are clickable evidence samples, not the full
            // counts. All counts above use the complete record.
            Evidence = facts.OrderByDescending(f => f.Year)
                .ThenBy(f => f.Category)
                .Take(500)
                .Select(f => new NationCultureEvidenceDto
                {
                    Year = f.Year,
                    Category = f.Category,
                    Description = f.Description,
                    Link = f.Link
                }).ToList()
        };
    }
}

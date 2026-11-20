using System.Runtime.CompilerServices;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.EventCollections;
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
    public int SitesLostInWar { get; set; }
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
    // Existing total territorial growth rate, retained for nation detail pages.
    public double ExpansionPerCentury { get; set; }
    public double SettlementExpansionPerCentury { get; set; }
    public double ConquestPerCentury { get; set; }
    public double SitesLostInWarPerCentury { get; set; }
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
    private sealed record ConquestFact(Site Site, int Year, string Description);

    // Culture comparisons request a profile for every nation. Index the world's
    // capture events once instead of scanning the full event history per nation.
    // The weak key lets a replaced/imported world be collected normally.
    private static readonly ConditionalWeakTable<IWorld, ConquestIndex> ConquestIndexes = new();

    private sealed class ConquestIndex
    {
        private readonly Dictionary<Entity, List<ConquestFact>> _byNation = [];
        private readonly Dictionary<Entity, HashSet<(int SiteId, int Year)>> _seen = [];

        public ConquestIndex(IWorld world)
        {
            void Record(Site? site, Entity? attacker, Entity? defender, int year, string description)
            {
                if (site == null || attacker == null || year < 0)
                {
                    return;
                }

                Entity winner = attacker.MapNation;
                if (defender != null && defender.MapNation == winner)
                {
                    return; // An internal change of local government is not a conquest.
                }

                if (!_seen.TryGetValue(winner, out var seen))
                {
                    seen = [];
                    _seen[winner] = seen;
                }
                if (!seen.Add((site.Id, year)))
                {
                    return;
                }
                if (!_byNation.TryGetValue(winner, out var facts))
                {
                    facts = [];
                    _byNation[winner] = facts;
                }
                facts.Add(new ConquestFact(site, year, description));
            }

            // Ownership periods can be attached to a subsidiary before its
            // civilization parent is assigned. In that case the site's own
            // history survives but the nation's SiteHistory is never updated.
            foreach (Site site in world.Sites)
            {
                Entity? previousOwner = null;
                foreach (var period in site.OwnerHistory.OrderBy(p => p.StartYear))
                {
                    string cause = period.StartCause?.Trim().ToLowerInvariant() ?? "";
                    if (cause is "took over" or "taken over" or "conquered")
                    {
                        Record(site, period.Owner, previousOwner, period.StartYear, cause);
                    }
                    if (period.Owner != null)
                    {
                        previousOwner = period.Owner;
                    }
                }
            }

            foreach (WorldEvent entry in world.Events)
            {
                switch (entry)
                {
                    case SiteTakenOver taken:
                        Record(taken.Site, taken.Attacker, taken.Defender ?? taken.SiteEntity,
                            entry.Year, "taken over");
                        break;
                    case NewSiteLeader leader:
                        Record(leader.Site, leader.Attacker, leader.Defender ?? leader.SiteEntity,
                            entry.Year, "captured");
                        break;
                    case SiteSurrendered surrendered:
                        Record(surrendered.Site, surrendered.Attacker,
                            surrendered.Defender ?? surrendered.SiteEntity,
                            entry.Year, "surrendered");
                        break;
                }
            }

            // Some exports contain a confirmed conquest collection without a
            // separate takeover/leadership event. Conversely, invasions,
            // pillaging and attempted attacks must not become conquests.
            foreach (SiteConquered conquest in world.SiteConquerings)
            {
                if (conquest.ConquerType != SiteConqueredType.Conquest)
                {
                    continue;
                }

                int year = conquest.StartYear >= 0 ? conquest.StartYear :
                    conquest.GetSubEvents().Where(e => e.Year >= 0)
                        .Select(e => e.Year).DefaultIfEmpty(-1).Max();
                Site? site = conquest.Site ?? conquest.GetSubEvents()
                    .Select(e => e switch
                    {
                        SiteTakenOver taken => taken.Site,
                        NewSiteLeader leader => leader.Site,
                        SiteSurrendered surrendered => surrendered.Site,
                        AttackedSite attacked => attacked.Site,
                        _ => null
                    })
                    .FirstOrDefault(candidate => candidate != null);
                Record(site, conquest.Attacker, conquest.Defender, year, "conquered");
            }
        }

        public IEnumerable<ConquestFact> For(Entity nation) =>
            _byNation.TryGetValue(nation, out var facts) ? facts : [];
    }

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

        // SiteHistory can be attached to a subsidiary site government before
        // its parent civilization is linked. Capture events independently
        // identify the victor, so use them as a second source and deduplicate
        // against already-counted ownership periods.
        if (nation.World != null)
        {
            foreach (ConquestFact conquest in
                ConquestIndexes.GetValue(nation.World, static world => new ConquestIndex(world))
                    .For(nation))
            {
                if (seenSites.Add((conquest.Site.Id, conquest.Year, "conquest")))
                {
                    Add(conquest.Year, "conquest",
                        $"{conquest.Site.Name}: {conquest.Description}",
                        $"/site/{conquest.Site.Id}");
                }
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

        // A site is a wartime loss only if a recorded war associates its
        // capture, surrender or destruction with the opposing belligerent.
        // Plunder, abandonment and withering are not wartime captures.
        var seenWarLosses = new HashSet<(int SiteId, int Year)>();

        static War? WarOf(EventCollection? collection)
        {
            var visited = new HashSet<EventCollection>();
            while (collection != null && visited.Add(collection))
            {
                if (collection is War war)
                {
                    return war;
                }
                collection = collection.ParentCollection;
            }
            return null;
        }

        bool IsOpposingSide(Entity? victor, War war)
        {
            if (victor != null && SameNation(victor, nation))
            {
                return false;
            }

            Entity? opponent = SameNation(war.Attacker, nation) ? war.Defender :
                SameNation(war.Defender, nation) ? war.Attacker : null;
            if (opponent == null || SameNation(opponent, nation))
            {
                return false;
            }

            // An unnamed conqueror is attributed to the opposing side only
            // when the event/collection is explicitly linked to this war.
            return victor == null || SameNation(victor, opponent);
        }

        void AddWarLoss(Site? site, int year, Entity? victor, Entity? victim,
            War war, string description)
        {
            if (site == null || year < 0 || !SameNation(victim, nation) ||
                !IsOpposingSide(victor, war) || !seenWarLosses.Add((site.Id, year)))
            {
                return;
            }

            Add(year, "warLoss", $"{site.Name}: {description} in {war.Name}",
                $"/site/{site.Id}");
        }

        void ApplyWarEvent(WorldEvent entry, War war)
        {
            switch (entry)
            {
                case SiteTakenOver takeover:
                    AddWarLoss(takeover.Site, entry.Year, takeover.Attacker,
                        takeover.Defender ?? takeover.SiteEntity, war, "taken over");
                    break;
                case NewSiteLeader newLeader:
                    AddWarLoss(newLeader.Site, entry.Year, newLeader.Attacker,
                        newLeader.Defender ?? newLeader.SiteEntity, war, "captured");
                    break;
                case SiteSurrendered surrender:
                    AddWarLoss(surrender.Site, entry.Year, surrender.Attacker,
                        surrender.Defender ?? surrender.SiteEntity, war, "surrendered");
                    break;
                case DestroyedSite destroyed:
                    AddWarLoss(destroyed.Site, entry.Year, destroyed.Attacker,
                        destroyed.Defender ?? destroyed.SiteEntity, war, "destroyed");
                    break;
                case HfDestroyedSite hfDestroyed:
                    AddWarLoss(hfDestroyed.Site, entry.Year, null,
                        hfDestroyed.DefenderCiv ?? hfDestroyed.SiteCiv, war, "destroyed");
                    break;
            }
        }

        foreach (var war in nation.Wars.Distinct())
        {
            if (!SameNation(war.Attacker, nation) && !SameNation(war.Defender, nation))
            {
                continue;
            }

            foreach (var entry in war.GetSubEvents().Distinct())
            {
                ApplyWarEvent(entry, war);
            }

            // Some Legends exports record the outcome in SiteConquered but
            // omit a concrete takeover/destruction event. Use the collection
            // itself as evidence only for an explicit conquest or destruction.
            foreach (var conquest in war.Conquerings.Distinct())
            {
                if (conquest.ConquerType != SiteConqueredType.Conquest &&
                    conquest.ConquerType != SiteConqueredType.Destruction)
                {
                    continue;
                }
                int year = conquest.StartYear >= 0
                    ? conquest.StartYear
                    : conquest.Events.Where(e => e.Year >= 0)
                        .Select(e => e.Year).DefaultIfEmpty(-1).Max();
                AddWarLoss(conquest.Site, year, conquest.Attacker, conquest.Defender,
                    war, conquest.ConquerType == SiteConqueredType.Destruction
                        ? "destroyed" : "conquered");
            }
        }

        // Certain exports link events via ParentCollection without including
        // them in the enclosing war's Events list.
        foreach (var entry in nation.Events.Distinct())
        {
            if (WarOf(entry.ParentCollection) is War war)
            {
                ApplyWarEvent(entry, war);
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
                SitesLostInWar = Count("warLoss"),
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
                SettlementExpansionPerCentury = Rate(settlements),
                ConquestPerCentury = Rate(conquests),
                SitesLostInWarPerCentury = Rate(Count("warLoss")),
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

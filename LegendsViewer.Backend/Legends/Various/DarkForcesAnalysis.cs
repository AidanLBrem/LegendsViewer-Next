using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Legends.Various;

// These are settlement-based measures. Legends does not encode the physical
// boundary or military strength of a necromancer's empire for every year.
public sealed record DarkForceFactionDto(int Id, string Name, int FromYear, int? ThroughYear, string Evidence);
public sealed record DarkForceEventDto(
    int Year, string Kind, string Description, int? SiteId, string? SiteName,
    int? TargetEntityId, string? TargetEntityName, bool IsInvasion, bool IsPrelude,
    string Attribution);
public sealed record DarkForceDto(
    int FigureId, string Name, int? AwakeningYear, string Status, bool Defeated,
    int? DefeatYear, int PeakSettlements, int? PeakYear, int CurrentSettlements,
    int Invasions, int Conquests, int NotableKills, int PowerIndex,
    List<DarkForceFactionDto> Factions, List<DarkForceEventDto> Timeline,
    int RecordedEvents, int? FirstInvasionYear);
public sealed record DarkForcesPageDto(
    List<DarkForceDto> Items, int TotalCount, int TotalFilteredCount,
    int FallenCount, int TotalInvasions, int MaximumTerritory);

public static class DarkForcesAnalysis
{
    // Weighted *evidence index*, not a game stat. The frontend explains the formula.
    // Territorial control is the largest component, followed by confirmed captures,
    // invasion events, and distinct recorded kills (capped for readability).
    public static int PowerIndex(int peak, int conquests, int invasions, int kills) =>
        peak * 10 + conquests * 8 + invasions * 3 + Math.Min(kills, 100);

    private sealed record Tenure(Entity Root, int Start, int EndExclusive, string Evidence)
    {
        public bool Active(int year) => year >= Start && year < EndExclusive;
    }

    public static List<DarkForceDto> Calculate(IWorld world)
    {
        int currentYear = Math.Max(0, world.CurrentYear);
        // Index candidate settlements once. Large Legends exports can contain
        // many thousands of sites and hundreds of necromancers.
        var sitesByNation = world.Sites.SelectMany(site =>
                site.OwnerHistory.Where(p => p.Owner != null)
                    .SelectMany(p => new[] { p.Owner!, p.Owner!.MapNation }.Distinct()
                        .Select(root => new { Nation = root, Site = site })))
            .GroupBy(x => x.Nation)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Site).Distinct().ToList());
        var entitiesByNation = world.Entities.GroupBy(e => e.MapNation)
            .ToDictionary(g => g.Key, g => g.Distinct().ToList());
        var result = new List<DarkForceDto>();
        foreach (HistoricalFigure figure in world.HistoricalFigures.Where(IsNecromancer))
        {
            // Mere membership of a civilization does not mean this necromancer
            // commanded the civilization. Only founding or recorded leadership
            // grants territorial and faction-military attribution.
            var tenures = GetTenures(figure, currentYear);
            bool InFaction(Entity? entity, int year)
            {
                if (entity == null) return false;
                return tenures.Any(t => t.Active(year) && BelongsToFaction(entity, t.Root));
            }

            var associatedSites = tenures.SelectMany(t =>
                    sitesByNation.TryGetValue(t.Root, out var sites)
                        ? (IEnumerable<Site>)sites : Enumerable.Empty<Site>())
                .Distinct().ToList();
            var territory = CalculateTerritory(associatedSites, tenures, currentYear);
            var involvedEntities = tenures.SelectMany(t =>
                    t.Root.IsCiv || !t.Root.MapNation.IsCiv
                        ? (entitiesByNation.TryGetValue(t.Root, out var members)
                            ? (IEnumerable<Entity>)members : [t.Root])
                        : [t.Root])
                .Distinct().ToList();
            var sourceEvents = figure.Events.Concat(
                    involvedEntities.SelectMany(t => t.Events))
                .Where(e => e.Year >= 0 && e.Year <= currentYear)
                .DistinctBy(e => e.Id)
                .OrderBy(e => e.Year).ThenBy(e => e.Seconds72).ThenBy(e => e.Id)
                .ToList();

            int? awakening = figure.Events.OfType<HfLearnsSecret>()
                .Where(e => e.Student == figure && e.Year >= 0)
                .Select(e => (int?)e.Year).OrderBy(y => y).FirstOrDefault();
            awakening ??= figure.CreatureTypes.Where(t =>
                    t.Type.Contains("necromancer", StringComparison.OrdinalIgnoreCase) && t.StartYear >= 0)
                .Select(t => (int?)t.StartYear).OrderBy(y => y).FirstOrDefault();

            var annotated = new List<(WorldEvent Source, string Kind, Site? Site, Entity? Target, bool Invasion, string Attribution)>();
            foreach (WorldEvent incident in sourceEvents)
            {
                var classified = incident switch
                {
                    HfLearnsSecret e when e.Student == figure =>
                        ("Learned a dark secret", (Site?)null, (Entity?)null, false, "Personal"),
                    EntityCreated e when e.Creator == figure =>
                        ("Founded a faction", e.Site, (Entity?)null, false, "Personal"),
                    CreatedSite e when e.Builder == figure =>
                        ("Established a site", e.Site, (Entity?)null, false, "Personal"),
                    ChangeHfState e when e.HistoricalFigure == figure &&
                        (!awakening.HasValue || incident.Year >= awakening.Value) &&
                        e.State is HfState.Settled or HfState.Wandering or HfState.Scouting =>
                        ("Movement and preparation", e.Site, (Entity?)null, false, "Personal"),
                    HfAttackedSite e when e.Attacker == figure =>
                        ("Attacked a settlement", e.Site, e.DefenderCiv ?? e.SiteCiv, true, "Personal"),
                    AttackedSite e when e.AttackerGeneral == figure =>
                        ("Led an invasion", e.Site, e.Defender, true, "Named commander"),
                    AttackedSite e when InFaction(e.Attacker, e.Year) =>
                        ("Faction invaded", e.Site, e.Defender, true, "Associated faction"),
                    SiteTakenOver e when InFaction(e.Attacker, e.Year) =>
                        ("Captured a settlement", e.Site, e.Defender, false, "Associated faction"),
                    PlunderedSite e when InFaction(e.Attacker, e.Year) =>
                        ("Plundered a settlement", e.Site, e.Defender, false, "Associated faction"),
                    FieldBattle e when e.AttackerGeneral == figure =>
                        ("Led a field battle", (Site?)null, e.Defender, false, "Named commander"),
                    FieldBattle e when InFaction(e.Attacker, e.Year) =>
                        ("Faction fought a field battle", (Site?)null, e.Defender, false, "Associated faction"),
                    EntityDissolved e when InFaction(e.Entity, e.Year) =>
                        ("Faction dissolved", (Site?)null, (Entity?)null, false, "Associated faction"),
                    HfDied e when e.HistoricalFigure == figure =>
                        ("Necromancer died", e.Site, (Entity?)null, false, "Personal"),
                    _ => ((string?)null, (Site?)null, (Entity?)null, false, "")
                };
                if (classified.Item1 != null)
                    annotated.Add((incident, classified.Item1, classified.Item2,
                        classified.Item3, classified.Item4, classified.Item5));
            }

            var invasionEvents = annotated.Where(e => e.Invasion).ToList();
            int? firstInvasion = invasionEvents.Select(e => (int?)e.Source.Year)
                .OrderBy(y => y).FirstOrDefault();
            int invasions = invasionEvents
                .DistinctBy(e => (e.Source.Year, e.Site?.Id ?? -e.Source.Id))
                .Count();
            int conquests = annotated
                .Where(e => e.Source is SiteTakenOver && e.Site != null)
                .DistinctBy(e => (e.Site!.Id, e.Source.Year)).Count();
            int kills = figure.NotableKills.Select(e => e.Id).Distinct().Count();

            var timeline = annotated.Select(e => new DarkForceEventDto(
                    e.Source.Year, e.Kind, e.Source.Print(false),
                    e.Site?.Id, e.Site?.Name, e.Target?.Id, e.Target?.Name,
                    e.Invasion, firstInvasion.HasValue && e.Source.Year < firstInvasion.Value,
                    e.Attribution)).ToList();

            // A dead necromancer has fallen, but the export does not always
            // establish that an army defeated them. Surviving figures whose
            // settlements vanished are only marked fallen with recorded loss.
            bool lossRecorded = annotated.Any(e => e.Source is EntityDissolved) ||
                associatedSites.SelectMany(s => s.OwnerHistory).Any(p =>
                    p.EndYear >= 0 && tenures.Any(t =>
                        BelongsToFaction(p.Owner, t.Root)) &&
                    (p.EndCause?.Contains("taken over", StringComparison.OrdinalIgnoreCase) == true ||
                     p.EndCause?.Contains("destroy", StringComparison.OrdinalIgnoreCase) == true ||
                     p.EndCause?.Contains("conquer", StringComparison.OrdinalIgnoreCase) == true));
            // "Territory currently commanded" can be zero because a leadership
            // term ended, while the faction itself still survives. Do not call
            // that a military defeat unless the recorded faction holdings vanished.
            bool factionStillHoldsLand = associatedSites.Any(site =>
                site.OwnerHistory.Any(p => p.EndYear < 0 &&
                    tenures.Any(t => BelongsToFaction(p.Owner, t.Root))));
            bool realmLost = territory.Peak > 0 && !factionStillHoldsLand && lossRecorded;
            bool killed = figure.DeathYear >= 0 &&
                (figure.DeathEvent?.Slayer != null || figure.DeathCause is
                    DeathCause.PutToRest or DeathCause.Struck or DeathCause.Murdered or
                    DeathCause.Shot or DeathCause.DragonsFire or DeathCause.Slaughtered or
                    DeathCause.ExecutedGeneric or DeathCause.ExecutedFedToBeasts or
                    DeathCause.ExecutedBurnedAlive or DeathCause.ExecutedCrucified or
                    DeathCause.ExecutedDrowned or DeathCause.ExecutedHackedToPieces or
                    DeathCause.ExecutedBuriedAlive or DeathCause.ExecutedBeheaded);
            bool defeated = killed || realmLost;
            string status = killed ? "Killed / put to rest" :
                figure.DeathYear >= 0 ? "Deceased (cause not defeat)" :
                realmLost ? "Realm lost" : "No recorded downfall";
            var factionList = tenures.OrderBy(t => t.Start).ThenBy(t => t.Root.Id)
                .Select(t => new DarkForceFactionDto(t.Root.Id, t.Root.Name, t.Start,
                    t.EndExclusive > currentYear ? null : t.EndExclusive - 1, t.Evidence)).ToList();
            result.Add(new DarkForceDto(figure.Id, figure.Name, awakening,
                status, defeated, figure.DeathYear >= 0 ? figure.DeathYear : null,
                territory.Peak, territory.PeakYear, territory.Current, invasions, conquests,
                kills, PowerIndex(territory.Peak, conquests, invasions, kills),
                factionList, timeline.Take(300).ToList(), timeline.Count, firstInvasion));
        }
        return result.OrderBy(r => r.FigureId).ToList();
    }

    public static bool IsNecromancer(HistoricalFigure figure) =>
        HistoricalFigureExtensions.IsNecromancer(figure) ||
        figure.CreatureTypes.Any(t =>
            t.Type.Contains("necromancer", StringComparison.OrdinalIgnoreCase)) ||
        figure.Events.OfType<HfLearnsSecret>().Any(e => e.Student == figure);

    private static List<Tenure> GetTenures(HistoricalFigure figure, int currentYear)
    {
        var tenures = new List<Tenure>();
        void Add(Entity? entity, int fromYear, int? throughYear, string evidence)
        {
            if (entity == null) return;
            // Leadership of a local government inside an ordinary civilization
            // does not imply command of the entire civilization.
            Entity root = entity.IsCiv || !entity.MapNation.IsCiv
                ? entity.MapNation : entity;
            int from = Math.Max(0, fromYear);
            int endExclusive = Math.Min(currentYear + 1,
                throughYear.HasValue ? throughYear.Value + 1 : currentYear + 1);
            if (figure.DeathYear >= 0)
                endExclusive = Math.Min(endExclusive, figure.DeathYear + 1);
            if (endExclusive <= from) return;
            tenures.Add(new(root, from, endExclusive, evidence));
        }

        foreach (var founded in figure.Events.OfType<EntityCreated>().Where(e => e.Creator == figure))
            Add(founded.Entity, founded.Year, null, "Founded by necromancer");
        foreach (var site in figure.Events.OfType<CreatedSite>().Where(e => e.Builder == figure))
        {
            var owner = site.SiteEntity ?? site.Civ ?? site.ResidentCiv;
            if (owner != null && !owner.MapNation.IsCiv)
                Add(owner, site.Year, null, "Built by necromancer");
        }
        foreach (var position in figure.Positions ?? [])
            if (position.PositionId == 0)
                Add(position.Entity, position.StartYear ?? 0, position.EndYear, "Recorded leadership");
        foreach (var link in figure.RelatedEntities.Where(l =>
                     (l.Type is EntityLinkType.Position or EntityLinkType.FormerPosition) && l.PositionId == 0))
            Add(link.Entity, link.StartYear, link.EndYear < 0 ? null : link.EndYear, "Recorded position link");
        return tenures.Distinct().ToList();
    }

    private static bool BelongsToFaction(Entity? entity, Entity root)
    {
        if (entity == null) return false;
        if (entity == root) return true;
        // Full organizational roll-up is only appropriate for a sovereign
        // civilization or independent non-civilization faction, never a local
        // government nested under a third-party civilization.
        return (root.IsCiv || !root.MapNation.IsCiv) && entity.MapNation == root;
    }

    private sealed record TerritoryResult(int Peak, int? PeakYear, int Current);

    private static TerritoryResult CalculateTerritory(IEnumerable<Site> sites,
        IReadOnlyList<Tenure> tenures, int currentYear)
    {
        var change = new SortedDictionary<int, int>();
        int current = 0;
        foreach (var site in sites)
        {
            var intervals = new List<(int Start, int End)>();
            foreach (var period in site.OwnerHistory.Where(p => p.Owner != null))
            {
                foreach (var tenure in tenures)
                {
                    if (!BelongsToFaction(period.Owner, tenure.Root))
                        continue;
                    int start = Math.Max(Math.Max(0, period.StartYear), tenure.Start);
                    int end = Math.Min(period.EndYear < 0 ? currentYear + 1 : period.EndYear,
                        tenure.EndExclusive);
                    if (end > start) intervals.Add((start, end));
                }
            }
            // Merge same-site intervals; multi-government and overlapping tenure
            // records must never count one settlement twice.
            var ordered = intervals.OrderBy(x => x.Start).ThenBy(x => x.End).ToList();
            int? left = null, right = 0;
            void Emit()
            {
                if (!left.HasValue) return;
                change[left.Value] = change.GetValueOrDefault(left.Value) + 1;
                change[right] = change.GetValueOrDefault(right) - 1;
                if (left <= currentYear && right > currentYear) current++;
            }
            foreach (var interval in ordered)
            {
                if (!left.HasValue) { left = interval.Start; right = interval.End; }
                else if (interval.Start <= right) right = Math.Max(right, interval.End);
                else { Emit(); left = interval.Start; right = interval.End; }
            }
            Emit();
        }

        int held = 0, peak = 0;
        int? peakYear = null;
        foreach (var point in change)
        {
            held += point.Value;
            if (held > peak && point.Key <= currentYear)
            {
                peak = held;
                peakYear = point.Key;
            }
        }
        return new(peak, peakYear, current);
    }
}

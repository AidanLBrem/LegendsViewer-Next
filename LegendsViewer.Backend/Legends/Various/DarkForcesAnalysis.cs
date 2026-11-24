using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Legends.Various;

// These are settlement-based measures. Legends does not encode the physical
// boundary or military strength of a necromancer's empire for every year.
public sealed record DarkForceFactionDto(int Id, string Name, int FromYear, int? ThroughYear, string Evidence);
public sealed record DarkForceGroupDto(int Id, string Name, string Relationship, int? HoldingId, string? HoldingName);
// Apprentices are people explicitly taught a dark secret by this figure,
// not everyone who follows, serves or belongs to their faction.
public sealed record DarkForceApprenticeDto(int FigureId, string Name, int FirstLearnedYear);
public sealed record DarkForceRaisedFigureDto(
    int FigureId, string Name, int FirstRaisedYear, int RecordedRaiseEvents);
public sealed record DarkForceEventDto(
    int Year, string Kind, string Description, int? SiteId, string? SiteName,
    int? TargetEntityId, string? TargetEntityName, bool IsInvasion, bool IsPrelude,
    string Attribution);
public sealed record DarkForceDto(
    int FigureId, string Name, int? AwakeningYear, string Status, bool Defeated,
    int? DefeatYear, int PeakSettlements, int? PeakYear, int CurrentSettlements,
    int Invasions, int Conquests, int NotableKills, int PowerIndex,
    List<DarkForceFactionDto> Factions, List<DarkForceEventDto> Timeline,
    int RecordedEvents, int? FirstInvasionYear, int ApprenticeCount,
    List<DarkForceApprenticeDto> Apprentices, List<DarkForceGroupDto> Groups,
    int HistoricalRaisedCount, int HistoricalRaiseEvents,
    int? NonhistoricalRaisedCount, int UnidentifiedRaiseEvents,
    List<DarkForceRaisedFigureDto> RaisedHistoricalFigures);
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

    // ANIMATE and UNDEAD_RES are the explicit reanimation interactions in
    // Legends. Other curses (SECRET, VAMPIRE, WEREBEAST) are not resurrections.
    private static bool IsRaisingInteraction(string? interaction) =>
        interaction?.Contains("ANIMATE", StringComparison.OrdinalIgnoreCase) == true ||
        interaction?.Contains("UNDEAD_RES", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record Tenure(Entity Root, int Start, int EndExclusive, string Evidence)
    {
        public bool Active(int year) => year >= Start && year < EndExclusive;
    }

    public static List<DarkForceDto> Calculate(IWorld world)
    {
        int currentYear = Math.Max(0, world.CurrentYear);
        // Index candidate settlements once. Large Legends exports can contain
        // many thousands of sites and hundreds of necromancers.
        // Index by the actual owner AND each ancestor, not by MapNation alone.
        // A necromancer leading a subholding must not inherit parent/sibling holdings.
        var sitesByFaction = world.Sites.SelectMany(site =>
                site.OwnerHistory.Where(p => p.Owner != null)
                    .SelectMany(p => SelfAndParents(p.Owner!)
                        .Select(root => new { Root = root, Site = site })))
            .GroupBy(x => x.Root)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Site).Distinct().ToList());
        // Some event parsers attach only to a site/region, not to the actor.
        // Index their explicitly named subjects so those acts still appear.
        var experimentalEvents = world.Events.OfType<HfPerformedHorribleExperiments>()
            .Where(e => e.GroupHistoricalFigure != null)
            .ToLookup(e => e.GroupHistoricalFigure!);
        // HfRevived is attached to the victim, NOT to the actor. Index all
        // world events once so named necromancers receive their own raises.
        var revivalsByActor = world.Events.OfType<HfRevived>()
            .Where(e => e.Actor != null)
            .ToLookup(e => e.Actor!);
        var animationsByActor = world.Events.OfType<HfDoesInteraction>()
            .Where(e => e.Doer != null && IsRaisingInteraction(e.Interaction))
            .ToLookup(e => e.Doer!);
        var entitiesByFaction = world.Entities.SelectMany(entity =>
                SelfAndParents(entity).Select(root => new { Root = root, Entity = entity }))
            .GroupBy(x => x.Root)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Entity).Distinct().ToList());
        // Legends+ records present-day position assignments, sometimes without
        // a dated appointment event in the standard Legends export. These can
        // establish current leadership, but NOT past years of command.
        var currentRulers = world.Entities
            .Select(e => new
            {
                Entity = e,
                Leader = e.EntityPositionAssignments
                    .Where(a => a.PositionId == 0)
                    .OrderBy(a => a.PositionId).FirstOrDefault()?.HistoricalFigure
            })
            .Where(e => e.Leader != null)
            .GroupBy(e => e.Leader!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Entity).Distinct().ToList());
        var result = new List<DarkForceDto>();
        foreach (HistoricalFigure figure in world.HistoricalFigures.Where(IsNecromancer))
        {
            // Mere membership of a civilization does not mean this necromancer
            // commanded the civilization. Only founding or recorded leadership
            // grants territorial and faction-military attribution.
            var tenures = GetTenures(figure, currentYear,
                currentRulers.TryGetValue(figure, out var currentPositions)
                    ? currentPositions : []);
            bool InFaction(Entity? entity, int year)
            {
                if (entity == null) return false;
                return tenures.Any(t => t.Active(year) && BelongsToFaction(entity, t.Root));
            }
            string FactionAttribution(Entity? entity, int year) =>
                tenures.Any(t => t.Active(year) && t.Root == entity)
                    ? "Associated faction"
                    : "Linked subsidiary (current affiliation; historical link unverified)";

            var associatedSites = tenures.SelectMany(t =>
                    sitesByFaction.TryGetValue(t.Root, out var sites)
                        ? (IEnumerable<Site>)sites : Enumerable.Empty<Site>())
                .Distinct().ToList();
            var territory = CalculateTerritory(associatedSites, tenures, currentYear);
            var involvedEntities = tenures.SelectMany(t =>
                    entitiesByFaction.TryGetValue(t.Root, out var members)
                        ? (IEnumerable<Entity>)members : new[] { t.Root })
                .Distinct().ToList();
            var sourceEvents = figure.Events.Concat(
                    experimentalEvents[figure]).Concat(revivalsByActor[figure])
                .Concat(involvedEntities.SelectMany(t => t.Events))
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
                    HfRevived e when e.Actor == figure && !e.Disturbance =>
                        ("Raised a historical figure", e.Site, (Entity?)null, false, "Personal"),
                    HfLearnsSecret e when e.Student == figure =>
                        ("Learned a dark secret", (Site?)null, (Entity?)null, false, "Personal"),
                    HfLearnsSecret e when e.Teacher == figure =>
                        ("Taught a dark secret", (Site?)null, (Entity?)null, false, "Personal"),
                    HfDoesInteraction e when e.Target == figure &&
                        e.Interaction?.Contains("SECRET", StringComparison.OrdinalIgnoreCase) == true =>
                        ("Received a dark secret", e.Site, (Entity?)null, false, "Personal"),
                    HfGainsSecretGoal e when e.HistoricalFigure == figure =>
                        ("Formed a secret ambition", (Site?)null, (Entity?)null, false, "Personal"),
                    HfDoesInteraction e when e.Doer == figure &&
                        e.Interaction?.Contains("SECRET", StringComparison.OrdinalIgnoreCase) == true =>
                        ("Passed on a dark secret", e.Site, (Entity?)null, false, "Personal"),
                    HfDoesInteraction e when e.Doer == figure &&
                        (e.Interaction?.Contains("ANIMATE", StringComparison.OrdinalIgnoreCase) == true ||
                         e.Interaction?.Contains("UNDEAD_RES", StringComparison.OrdinalIgnoreCase) == true) =>
                        ("Practiced necromancy", e.Site, (Entity?)null, false, "Personal"),
                    HfPerformedHorribleExperiments e when e.GroupHistoricalFigure == figure =>
                        ("Performed horrible experiments", e.Site, (Entity?)null, false, "Personal"),
                    HfProfanedStructure e when e.HistoricalFigure == figure =>
                        ("Profaned a structure", e.Site, (Entity?)null, false, "Personal"),
                    HfRecruitedUnitTypeForEntity e when e.HistoricalFigure == figure =>
                        ("Recruited a unit type", e.Site, (Entity?)null, false, "Personal"),
                    AddHfEntityLink e when e.HistoricalFigure == figure &&
                        e.LinkType == HfEntityLinkType.Position && e.PositionId == 0 =>
                        ("Assumed leadership", (Site?)null, (Entity?)null, false, "Personal"),
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
                        ("Faction invaded", e.Site, e.Defender, true, FactionAttribution(e.Attacker, e.Year)),
                    SiteTakenOver e when InFaction(e.Attacker, e.Year) =>
                        ("Captured a settlement", e.Site, e.Defender, false, FactionAttribution(e.Attacker, e.Year)),
                    PlunderedSite e when InFaction(e.Attacker, e.Year) =>
                        ("Plundered a settlement", e.Site, e.Defender, false, FactionAttribution(e.Attacker, e.Year)),
                    EntityRampagedInSite e when InFaction(e.RampageCiv, e.Year) =>
                        ("Faction rampaged through a site", e.Site, (Entity?)null, false, FactionAttribution(e.RampageCiv, e.Year)),
                    FieldBattle e when e.AttackerGeneral == figure =>
                        ("Led a field battle", (Site?)null, e.Defender, false, "Named commander"),
                    FieldBattle e when InFaction(e.Attacker, e.Year) =>
                        ("Faction fought a field battle", (Site?)null, e.Defender, false, FactionAttribution(e.Attacker, e.Year)),
                    EntityDissolved e when InFaction(e.Entity, e.Year) =>
                        ("Faction dissolved", (Site?)null, (Entity?)null, false, FactionAttribution(e.Entity, e.Year)),
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

            // Directly attributed necromancy only: do not count actions by an
            // associated group, another necromancer or spontaneous revenants.
            // A revived historical figure may be raised repeatedly, so expose
            // both the unique-person count and recorded action count.
            var raisingEvents = revivalsByActor[figure].Cast<WorldEvent>()
                .Concat(figure.Events.OfType<HfRevived>()
                    .Where(e => e.Actor == figure))
                .Concat(animationsByActor[figure])
                .Concat(figure.Events.OfType<HfDoesInteraction>()
                    .Where(e => e.Doer == figure && IsRaisingInteraction(e.Interaction)))
                .Where(e => e.Year >= 0 && e.Year <= currentYear)
                .Where(e => e is HfRevived revival ? !revival.Disturbance : true)
                .DistinctBy(e => e.Id)
                .ToList();

            HistoricalFigure? RaisedPerson(WorldEvent evt) => evt switch
            {
                HfRevived e => e.HistoricalFigure,
                HfDoesInteraction e => e.Target,
                _ => null
            };
            var knownRaises = raisingEvents.Where(e => RaisedPerson(e) != null).ToList();
            var raisedHistorical = knownRaises
                .GroupBy(e => RaisedPerson(e)!.Id)
                .Select(group =>
                {
                    var first = group.OrderBy(e => e.Year).ThenBy(e => e.Id).First();
                    return new DarkForceRaisedFigureDto(RaisedPerson(first)!.Id,
                        RaisedPerson(first)!.Name, first.Year, group.Count());
                })
                .OrderBy(x => x.FirstRaisedYear).ThenBy(x => x.Name)
                .ThenBy(x => x.FigureId).ToList();
            int unidentifiedRaises = raisingEvents.Count - knownRaises.Count;
            // Ordinary unnamed undead do not have stable historical-figure
            // identifiers or an exported unit-count per reanimation. Null is
            // intentional: never mistake unknown for zero. An untargeted
            // animation event may refer to multiple, zero or unknown units.
            int? nonhistoricalRaised = null;
            // The parser attaches HfLearnsSecret to both teacher and student.
            // Count distinct recorded students rather than the number of
            // lessons, as a student can be taught repeatedly.
            var apprentices = figure.Events.OfType<HfLearnsSecret>()
                .Where(e => e.Teacher == figure && e.Student != null &&
                    e.Student != figure && e.Year >= 0 && e.Year <= currentYear)
                .GroupBy(e => e.Student!.Id)
                .Select(group =>
                {
                    var first = group.OrderBy(e => e.Year).ThenBy(e => e.Id).First();
                    return new DarkForceApprenticeDto(first.Student!.Id,
                        first.Student.Name, first.Year);
                })
                .OrderBy(a => a.FirstLearnedYear).ThenBy(a => a.Name).ThenBy(a => a.FigureId)
                .ToList();

            var timeline = annotated.Select(e => new DarkForceEventDto(
                    e.Source.Year, e.Kind, e.Source.Print(true),
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
            var groups = GetAssociatedGroups(figure, tenures);
            result.Add(new DarkForceDto(figure.Id, figure.Name, awakening,
                status, defeated, figure.DeathYear >= 0 ? figure.DeathYear : null,
                territory.Peak, territory.PeakYear, territory.Current, invasions, conquests,
                kills, PowerIndex(territory.Peak, conquests, invasions, kills),
                factionList, timeline.Take(300).ToList(), timeline.Count, firstInvasion,
                apprentices.Count, apprentices, groups,
                raisedHistorical.Count, knownRaises.Count, nonhistoricalRaised,
                unidentifiedRaises, raisedHistorical));
        }
        return result.OrderBy(r => r.FigureId).ToList();
    }

    public static bool IsNecromancer(HistoricalFigure figure) =>
        HistoricalFigureExtensions.IsNecromancer(figure) ||
        figure.CreatureTypes.Any(t =>
            t.Type.Contains("necromancer", StringComparison.OrdinalIgnoreCase)) ||
        figure.Events.OfType<HfLearnsSecret>().Any(e => e.Student == figure);

    private static List<Tenure> GetTenures(HistoricalFigure figure, int currentYear,
        IEnumerable<Entity> currentlyLedEntities)
    {
        var tenures = new List<Tenure>();
        void Add(Entity? entity, int fromYear, int? throughYear, string evidence)
        {
            if (entity == null) return;
            // Preserve exactly the founded/led holding. MapNation is for
            // identifying its broader group, NEVER expanding personal control.
            Entity root = entity;
            int from = Math.Max(0, fromYear);
            // Founding is historical evidence, not a lifetime appointment.
            // End an inferred tenure at the first recorded departure from this office.
            if (!throughYear.HasValue)
            {
                var departures = (figure.Positions ?? [])
                    .Where(p => p.Entity == root && p.PositionId == 0 && p.EndYear.HasValue)
                    .Select(p => p.EndYear!.Value)
                    .Concat(figure.RelatedEntities
                        .Where(l => l.Entity == root && l.PositionId == 0 &&
                            l.Type == EntityLinkType.FormerPosition && l.EndYear >= 0)
                        .Select(l => l.EndYear))
                    .Where(year => year >= from)
                    .OrderBy(year => year)
                    .ToList();
                if (departures.Count > 0)
                    throughYear = departures[0];
            }
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
        if (figure.DeathYear < 0)
            foreach (var entity in currentlyLedEntities)
                Add(entity, currentYear, null, "Current leader (Legends+; start unknown)");
        return tenures.Distinct().ToList();
    }

    private static IEnumerable<Entity> SelfAndParents(Entity entity)
    {
        // The parent relationship is a present-day hierarchy. Treat matching
        // descendant activity as associated, rather than proven historical command.
        HashSet<Entity> visited = [];
        Entity? current = entity;
        while (current != null && visited.Add(current))
        {
            yield return current;
            // A sovereign civilization never inherits another civilization's assets.
            if (current.IsCiv) yield break;
            current = current.Parent;
        }
    }

    private static bool BelongsToFaction(Entity? entity, Entity root) =>
        entity != null && SelfAndParents(entity).Contains(root);

    private static List<DarkForceGroupDto> GetAssociatedGroups(
        HistoricalFigure figure, IReadOnlyList<Tenure> tenures)
    {
        var links = figure.RelatedEntities
            .Where(l => l.Entity != null && l.Type is
                EntityLinkType.Member or EntityLinkType.FormerMember or
                EntityLinkType.Position or EntityLinkType.FormerPosition)
            .ToList();
        // Group membership and territorial command are deliberately separate.
        // Collapse membership links to the parent organization for display only.
        var groups = links.Select(l => l.Entity!.MapNation)
            .Concat(tenures.Select(t => t.Root.MapNation))
            .Distinct()
            .ToList();

        return groups.Select(group =>
        {
            var groupTenure = tenures.FirstOrDefault(t => t.Root == group);
            var subholding = tenures.FirstOrDefault(t => t.Root != group &&
                BelongsToFaction(t.Root, group));
            bool currentMember = links.Any(l => l.Entity!.MapNation == group &&
                l.Type is EntityLinkType.Member or EntityLinkType.Position);
            string relationship = groupTenure != null
                ? "Group leader / founder"
                : subholding != null ? "Subholding leader / founder"
                : currentMember ? "Member" : "Former member";
            return new DarkForceGroupDto(group.Id, group.Name, relationship,
                subholding?.Root.Id, subholding?.Root.Name);
        })
        .OrderBy(g => g.Relationship == "Former member" ? 1 : 0)
        .ThenBy(g => g.Relationship == "Group leader / founder" ? 0 :
                     g.Relationship == "Subholding leader / founder" ? 1 : 2)
        .ThenBy(g => g.Name)
        .ToList();
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
            int? left = null;
            int right = 0;
            void Emit()
            {
                if (!left.HasValue) return;
                change[left.Value] = change.GetValueOrDefault(left.Value) + 1;
                change[right] = change.GetValueOrDefault(right) - 1;
                if (left.Value <= currentYear && right > currentYear) current++;
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

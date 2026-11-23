using System.Text;
using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Cytoscape;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Extensions;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using LegendsViewer.Backend.Utilities;
using System.Drawing;
using System.Text.Json.Serialization;

namespace LegendsViewer.Backend.Legends.EventCollections;

public class War : EventCollection, IHasComplexSubtype
{
    public int Length { get; set; }
    public int DeathCount { get; set; }
    public int AttackerDeathCount { get; set; }
    public int DefenderDeathCount { get; set; }

    // Wars can involve more than the two civilizations that name the war.
    // Dwarf Fortress exports multiple aggressor/defender IDs and also an
    // involved_enid roster. Keep the sides separate, as involvement alone
    // does not establish which coalition a civilization fought for.
    [JsonIgnore]
    public List<Entity> AttackingParticipants { get; } = [];
    [JsonIgnore]
    public List<Entity> DefendingParticipants { get; } = [];
    [JsonIgnore]
    public List<Entity> OtherParticipants { get; } = [];

    public string GetParticipantRole(Entity participant)
    {
        static bool Matches(Entity member, Entity nation) =>
            member == nation || member.Id == nation.Id ||
            member.Parent == nation || member.CurrentCiv == nation;

        bool attacking = AttackingParticipants.Any(member => Matches(member, participant));
        bool defending = DefendingParticipants.Any(member => Matches(member, participant));

        if (attacking && defending)
        {
            return "Both sides";
        }
        if (attacking)
        {
            return "Attacking side";
        }
        if (defending)
        {
            return "Defending side";
        }
        return "Participant (side unspecified)";
    }

    // Battle and conquest records are useful fallbacks when a war's roster
    // is absent or incomplete. A battle's local attacker is not necessarily
    // on the war's aggressor side, so infer only from an already known side.
    public void RegisterOpponents(Entity? attacker, Entity? defender)
    {
        bool? KnownSide(Entity? entity)
        {
            if (entity == null)
            {
                return null;
            }
            bool isAttacker = AttackingParticipants.Any(member =>
                member == entity || member.CurrentCiv == entity.CurrentCiv && entity.CurrentCiv != null);
            bool isDefender = DefendingParticipants.Any(member =>
                member == entity || member.CurrentCiv == entity.CurrentCiv && entity.CurrentCiv != null);
            return isAttacker == isDefender ? null : isAttacker;
        }

        bool? attackerSide = KnownSide(attacker);
        bool? defenderSide = KnownSide(defender);
        bool? inferredAttackerSide = attackerSide;
        bool? inferredDefenderSide = defenderSide;

        if (!attackerSide.HasValue && defenderSide.HasValue)
        {
            inferredAttackerSide = !defenderSide.Value;
        }
        if (!defenderSide.HasValue && attackerSide.HasValue)
        {
            inferredDefenderSide = !attackerSide.Value;
        }

        RegisterParticipant(attacker, inferredAttackerSide);
        RegisterParticipant(defender, inferredDefenderSide);
    }

    public void RegisterParticipant(Entity? participant, bool? attackingSide = null)
    {
        if (participant == null)
        {
            return;
        }

        List<Entity> roster = attackingSide switch
        {
            true => AttackingParticipants,
            false => DefendingParticipants,
            null => OtherParticipants
        };
        if (!roster.Contains(participant))
        {
            roster.Add(participant);
        }

        // Preserve the existing parent-group association, and also link
        // subsidiary participants back to their civilization.
        foreach (Entity entity in new Entity?[]
                 { participant, participant.Parent, participant.CurrentCiv }
                 .OfType<Entity>().Distinct())
        {
            if (!entity.Wars.Contains(this))
            {
                entity.Wars.Add(this);
                entity.AddEventCollection(this);
            }
        }
    }

    // These are battle deployments, NOT unique soldiers or an independently
    // recorded army at the start of the war. A soldier appearing in two
    // battles counts twice. Map local attacking/defending positions back to
    // the aggressor and defender of the war.
    // Numeric aliases used by the generic overview server-side sorter.
    [JsonIgnore]
    public int AttackerTroopCount => AttackerBattleDeployments;
    [JsonIgnore]
    public int DefenderTroopCount => DefenderBattleDeployments;

    public int AttackerBattleDeployments => Battles.Sum(battle =>
        BattleAttackerIsWarAttacker(battle) ? battle.AttackerCount : battle.DefenderCount);
    public int DefenderBattleDeployments => Battles.Sum(battle =>
        BattleAttackerIsWarAttacker(battle) ? battle.DefenderCount : battle.AttackerCount);

    public ChartDataDto AttackerDeploymentsByRace => BuildTroopChart(
        Battles.SelectMany(battle => BattleAttackerIsWarAttacker(battle)
            ? battle.Attackers : battle.Defenders));

    public ChartDataDto DefenderDeploymentsByRace => BuildTroopChart(
        Battles.SelectMany(battle => BattleAttackerIsWarAttacker(battle)
            ? battle.Defenders : battle.Attackers));

    private bool BattleAttackerIsWarAttacker(Battle battle)
    {
        static bool Matches(Entity? participant, Entity? side) =>
            participant != null && side != null &&
            (participant.EqualsOrParentEquals(side) || participant.CurrentCiv == side);

        return Matches(battle.Attacker, Attacker) ||
            (!Matches(battle.Attacker, Defender) &&
             Matches(battle.Defender, Defender));
    }

    private ChartDataDto BuildTroopChart(IEnumerable<Battle.Squad> squads)
    {
        ChartDataDto chart = new();
        ChartDatasetDto dataset = new();

        foreach (var group in squads.Where(s => s.Numbers > 0)
                     .GroupBy(s => (s.Race, s.Animated))
                     .Select(group => new { group.Key.Race, group.Key.Animated, Count = group.Sum(s => s.Numbers) })
                     .OrderByDescending(x => x.Count))
        {
            chart.Labels.Add(group.Animated ? $"Undead {group.Race.NamePlural}" : group.Race.NamePlural);
            dataset.Data.Add(group.Count);
            Color color = World != null && World.MainRaces.TryGetValue(group.Race, out var raceColor)
                ? raceColor : Color.SlateGray;
            dataset.BorderColor.Add(color.ToRgbaString());
            dataset.BackgroundColor.Add(color.ToRgbaString(0.2f));
        }

        chart.Datasets.Add(dataset);
        return chart;
    }

    // Compute after the legends import is complete; cache the result so
    // sorting hundreds of wars does not re-aggregate the entire world.
    private WarfareSiteOutcomesDto? _siteOutcomes;
    public WarfareSiteOutcomesDto SiteOutcomes =>
        _siteOutcomes ??= WarfareSiteOutcomesDto.FromWar(this);

    // Numeric counterparts to the detailed site lists. Keeping these on the
    // domain objects lets the existing server-side sorter order all records
    // before pagination rather than just sorting the visible page.
    [JsonIgnore]
    public int AttackerSitesGainedCount => SiteOutcomes.AttackerGained.Count;
    [JsonIgnore]
    public int AttackerSitesLostCount => SiteOutcomes.AttackerLost.Count;
    [JsonIgnore]
    public int AttackerSitesPlunderedCount => SiteOutcomes.AttackerPlundered.Count;
    [JsonIgnore]
    public int DefenderSitesGainedCount => SiteOutcomes.DefenderGained.Count;
    [JsonIgnore]
    public int DefenderSitesLostCount => SiteOutcomes.DefenderLost.Count;
    [JsonIgnore]
    public int DefenderSitesPlunderedCount => SiteOutcomes.DefenderPlundered.Count;
    [JsonIgnore]
    public int UnattributedSitesCount => SiteOutcomes.Unattributed.Count;

    public List<ListItemDto> MiscList
    {
        get
        {
            var list = new List<ListItemDto>();

            if (Attacker != null)
            {
                list.Add(new ListItemDto
                {
                    Title = $"Attacker",
                    Subtitle = $"{Attacker.ToLink(true, this)} (Deaths: {AttackerDeathCount} ✝)",
                });
            }

            if (Defender != null)
            {
                list.Add(new ListItemDto
                {
                    Title = $"Defender",
                    Subtitle = $"{Defender.ToLink(true, this)} (Deaths: {DefenderDeathCount} ✝)",
                });
            }
            foreach ((string title, List<Entity> participants) in new[]
            {
                ("Attacking participants", AttackingParticipants),
                ("Defending participants", DefendingParticipants),
                ("Other involved civilizations", OtherParticipants
                    .Where(entity => !AttackingParticipants.Contains(entity) &&
                                     !DefendingParticipants.Contains(entity)).ToList())
            })
            {
                if (participants.Count > 0)
                {
                    list.Add(new ListItemDto
                    {
                        Title = title,
                        Subtitle = string.Join(", ", participants.Select(entity => entity.ToLink(true, this)))
                    });
                }
            }

            WarfareSiteOutcomesDto sites = SiteOutcomes;
            list.Add(new ListItemDto
            {
                Title = "Casualties",
                Subtitle = $"Total: {DeathCount:N0} deaths | Attacker losses: {AttackerDeathCount:N0} | Defender losses: {DefenderDeathCount:N0}"
            });
            list.Add(new ListItemDto
            {
                Title = "Attacker site outcomes",
                Subtitle = $"Gained: {sites.AttackerGained.Count:N0} | Lost: {sites.AttackerLost.Count:N0} | Plundered: {sites.AttackerPlundered.Count:N0}"
            });
            list.Add(new ListItemDto
            {
                Title = "Defender site outcomes",
                Subtitle = $"Gained: {sites.DefenderGained.Count:N0} | Lost: {sites.DefenderLost.Count:N0} | Plundered: {sites.DefenderPlundered.Count:N0}"
            });

            if (sites.Unattributed.Count > 0)
            {
                list.Add(new ListItemDto
                {
                    Title = "Other or unattributed site changes",
                    Subtitle = $"{sites.Unattributed.Count:N0} unique sites affected by records that cannot be assigned to either war side"
                });
            }

            return list;
        }
    }

    [JsonIgnore]
    public List<HistoricalFigure> NotableDeaths => AllEvents?
        .OfType<HfDied>()
        .Where(death => death.HistoricalFigure != null)
        .Select(death => death.HistoricalFigure!)
        .Distinct()
        .ToList() ?? [];

    public List<string> NotableDeathLinks => NotableDeaths.ConvertAll(x => x.ToLink(true, this));

    public ChartDataDto DeathsByRace
    {
        get
        {
            // War totals include battle casualties, so build the race breakdown
            // from those same battle totals rather than counting unrelated events.
            Dictionary<(CreatureInfo Race, bool Animated), int> deaths = [];
            foreach (var squad in Battles.SelectMany(battle => battle.Attackers.Concat(battle.Defenders)))
            {
                var key = (squad.Race, squad.Animated);
                deaths.TryGetValue(key, out int previous);
                deaths[key] = previous + squad.Deaths;
            }

            ChartDataDto chart = new();
            ChartDatasetDto dataset = new();
            foreach (var death in deaths.Where(entry => entry.Value > 0).OrderByDescending(entry => entry.Value))
            {
                chart.Labels.Add(death.Key.Animated ? $"Undead {death.Key.Race.NamePlural}" : death.Key.Race.NamePlural);
                dataset.Data.Add(death.Value);
                if (World != null && World.MainRaces.TryGetValue(death.Key.Race, out var raceColor))
                {
                    dataset.BorderColor.Add(raceColor.ToRgbaString());
                    dataset.BackgroundColor.Add(raceColor.ToRgbaString(0.2f));
                }
                else
                {
                    dataset.BorderColor.Add(Color.SlateGray.ToRgbaString());
                    dataset.BackgroundColor.Add(Color.SlateGray.ToRgbaString(0.2f));
                }
            }
            chart.Datasets.Add(dataset);
            return chart;
        }
    }

    private CytoscapeData? _battleGraphData;
    public CytoscapeData? BattleGraphData
    {
        get
        {
            if (_battleGraphData == null && Battles.Count > 0)
            {
                Dictionary<int, CytoscapeNodeElement> nodes = [];
                List<CytoscapeEdgeElement> edges = [];

                foreach (var item in Battles)
                {
                    if (item.Attacker != null && !nodes.ContainsKey(item.Attacker.Id))
                    {
                        nodes.Add(item.Attacker.Id, item.Attacker.GetCytoscapeNode());
                    }
                    if (item.Defender != null && !nodes.ContainsKey(item.Defender.Id))
                    {
                        nodes.Add(item.Defender.Id, item.Defender.GetCytoscapeNode());
                    }
                    if (item.Attacker != null && item.Defender != null)
                    {
                        int edgeWidth = item.DeathCount / 10;
                        edges.Add(new CytoscapeEdgeElement(new CytoscapeEdgeData
                        {
                            Source = $"node-{item.Attacker.Id}",
                            Target = $"node-{item.Defender.Id}",
                            Href = $"/battle/{item.Id}",
                            BackgroundColor = item.Attacker.LineColor.ToRgbaString(0.6f),
                            ForegroundColor = Formatting.GetReadableForegroundColor(item.Attacker.LineColor),
                            Width = edgeWidth > 15 ? 15 : edgeWidth == 0 ? 1 : edgeWidth,
                            Label = $"{item.DeathCount} ✝",
                            Tooltip = $"{item.ToLink(true, item)}<br/>{item.Subtype}"
                        }));
                    }
                }

                var warGraphData = new CytoscapeData();
                warGraphData.Nodes.AddRange(nodes.Values);
                warGraphData.Edges.AddRange(edges);
                _battleGraphData = warGraphData;
            }
            return _battleGraphData;
        }

        set => _battleGraphData = value;
    }

    [JsonIgnore]
    public Entity? Attacker { get; set; }
    [JsonIgnore]
    public Entity? Defender { get; set; }
    [JsonIgnore]
    public List<Battle> Battles => EventCollections.OfType<Battle>().ToList();

    private const int LeadUpWindowYears = 50;
    private LeadUpAnalysis? _leadUpAnalysis;

    // Do not conflate a recorded incident with an explanation of why a war began.
    // Build the three views together to keep filtering and diagnostic counts consistent.
    public List<ListItemDto> LeadUp => AnalyzeLeadUp().Timeline;
    public List<ListItemDto> LeadUpContext => AnalyzeLeadUp().Context;
    public List<ListItemDto> LeadUpDiagnostics => AnalyzeLeadUp().Diagnostics;
    public int LeadUpYears => LeadUpWindowYears;

    private sealed class LeadUpAnalysis
    {
        public List<ListItemDto> Timeline { get; } = [];
        public List<ListItemDto> Context { get; } = [];
        public List<ListItemDto> Diagnostics { get; } = [];
    }

    private LeadUpAnalysis AnalyzeLeadUp() => _leadUpAnalysis ??= BuildLeadUpAnalysis();

    private LeadUpAnalysis BuildLeadUpAnalysis()
    {
        LeadUpAnalysis result = new();
        if (StartYear < 0)
        {
            result.Diagnostics.Add(new ListItemDto
            {
                Title = "No known start year",
                Subtitle = "The war has no dated start, so a pre-war timeline cannot be reconstructed."
            });
            return result;
        }

        int firstYear = Math.Max(0, StartYear - LeadUpWindowYears);
        var attacking = AttackingParticipants.ToHashSet();
        var defending = DefendingParticipants.ToHashSet();
        if (Attacker != null) attacking.Add(Attacker);
        if (Defender != null) defending.Add(Defender);

        // A local government can change owners. Resolve membership for the
        // *year of each incident*, rather than using only its current parent.
        var attackerPeriods = attacking.SelectMany(e => e.SiteHistory)
            .Where(p => p.Owner != null).ToLookup(p => p.Owner!);
        var defenderPeriods = defending.SelectMany(e => e.SiteHistory)
            .Where(p => p.Owner != null).ToLookup(p => p.Owner!);

        int SideAt(Entity? entity, int year)
        {
            if (entity == null)
                return 0;

            bool isAttacker = attacking.Contains(entity);
            bool isDefender = defending.Contains(entity);
            if (isAttacker || isDefender)
                return isAttacker == isDefender ? 0 : isAttacker ? 1 : -1;

            static bool Active(OwnerPeriod p, int y) =>
                p.StartYear <= y && (p.EndYear < 0 || y < p.EndYear);

            bool hasPeriods = attackerPeriods.Contains(entity) || defenderPeriods.Contains(entity);
            isAttacker = attackerPeriods[entity].Any(p => Active(p, year));
            isDefender = defenderPeriods[entity].Any(p => Active(p, year));

            // Undated subsidiary groups have only a parent link. This is a
            // weaker fallback; NEVER override known historical owner periods.
            if (!hasPeriods)
            {
                isAttacker = attacking.Any(root => entity.Parent == root ||
                    (root.IsCiv && entity.CurrentCiv == root));
                isDefender = defending.Any(root => entity.Parent == root ||
                    (root.IsCiv && entity.CurrentCiv == root));
            }
            return isAttacker == isDefender ? 0 : isAttacker ? 1 : -1;
        }

        // Search both sides. Include groups represented by ownership periods
        // and undated child governments, not just events attached to attackers.
        var roots = attacking.Concat(defending).ToHashSet();
        var linkedGroups = (World?.Entities ?? []).Where(e => e != null && !roots.Contains(e) &&
            (roots.Contains(e.Parent!) || (e.CurrentCiv != null && roots.Contains(e.CurrentCiv))));
        var members = roots.Concat(attackerPeriods.Select(g => g.Key))
            .Concat(defenderPeriods.Select(g => g.Key)).Concat(linkedGroups).Distinct().ToList();
        var attached = members.SelectMany(e => e.Events)
            .Where(e => e.Year >= firstYear && e.Year <= StartYear).ToList();
        var incidents = attached.DistinctBy(e => e.Id)
            .OrderBy(e => e.Year).ThenBy(e => e.Seconds72).ThenBy(e => e.Id).ToList();

        int unsupported = 0, unknownFaction = 0, otherFaction = 0, excludedAtStart = 0;
        int inferredAffiliations = 0;
        int disputeCount = 0, hostileCount = 0, diplomaticCount = 0;
        int otherCampaigns = 0;

        foreach (var incident in incidents)
        {
            (Entity? first, Entity? second, string? label, bool hostile) = incident switch
            {
                FirstContact e => (e.Contactor, e.Contacted, "First contact", false),
                SiteDispute e => (e.Entity1, e.Entity2, "Site dispute", false),
                DiplomatLost e => (e.Entity, e.InvolvedEntity, "Missing diplomat", false),
                Merchant e when e.Seizure || e.LostValue || e.AllDead || e.HardShip =>
                    (e.Source, e.Destination, "Caravan incident", false),
                AgreementRejected e => (e.Source, e.Destination, "Rejected agreement", false),
                PeaceRejected e => (e.Source, e.Destination, "Peace rejected", false),
                PeaceAccepted e => (e.Source, e.Destination, "Peace accepted", false),
                SiteTributeForced e => (e.Attacker, e.Defender ?? e.SiteEntity, "Forced tribute", true),
                PlunderedSite e => (e.Attacker, e.Defender ?? e.SiteEntity, "Plunder", true),
                AttackedSite e => (e.Attacker, e.Defender ?? e.SiteEntity, "Site attack", true),
                FieldBattle e => (e.Attacker, e.Defender, "Field battle", true),
                _ => (null, null, null, false)
            };

            if (label == null)
            {
                unsupported++;
                continue;
            }

            int firstSide = SideAt(first, incident.Year);
            int secondSide = SideAt(second, incident.Year);

            // Count military activity by the attacking coalition elsewhere,
            // without misrepresenting it as an incident against this opponent.
            if (hostile && firstSide == 1 && secondSide != -1 && incident.Year >= StartYear - 10)
                otherCampaigns++;

            if (firstSide == 0 || secondSide == 0)
            {
                unknownFaction++;
                continue;
            }
            if (firstSide == secondSide)
            {
                otherFaction++;
                continue;
            }

            bool atStart = false;
            if (incident.Year == StartYear)
            {
                // Never attribute an undated same-year encounter to the prelude.
                // Events exactly at the start must belong to THIS war.
                if (StartSeconds72 < 0 || incident.Month == 0 ||
                    incident.Seconds72 < 0 || incident.Seconds72 > StartSeconds72)
                {
                    excludedAtStart++;
                    continue;
                }
                atStart = incident.Seconds72 == StartSeconds72;
                if (atStart)
                {
                    EventCollection? parent = incident.ParentCollection;
                    while (parent != null && parent != this)
                        parent = parent.ParentCollection;
                    if (parent == null)
                    {
                        excludedAtStart++;
                        continue;
                    }
                }
            }

            // Current parentage is not a dated historical affiliation. Mark
            // these as possible coalition links rather than established ones.
            bool inferred = new Entity?[] { first, second }.OfType<Entity>().Any(e =>
                !roots.Contains(e) && !attackerPeriods.Contains(e) &&
                !defenderPeriods.Contains(e));
            if (inferred)
                inferredAffiliations++;
            result.Timeline.Add(new ListItemDto
            {
                Title = (atStart ? "War opening · " : "") +
                    (inferred ? "Possible affiliation · " : "") + label,
                Subtitle = incident.Print(true, this)
            });
            if (incident is SiteDispute)
                disputeCount++;
            else if (hostile)
                hostileCount++;
            else
                diplomaticCount++;
        }

        // Prior wars provide historical context even if no individual encounter
        // is present in the lead-up window. They are NOT treated as war triggers.
        var priorWars = (World?.Wars ?? []).Where(w => w != this &&
                w.StartYear >= 0 && w.StartYear < StartYear &&
                w.EndYear >= 0 && w.EndYear < StartYear &&
                (SideAt(w.Attacker, w.StartYear) * SideAt(w.Defender, w.StartYear) == -1))
            .OrderByDescending(w => w.StartYear).ThenBy(w => w.Id).ToList();

        if (disputeCount > 0)
            result.Context.Add(new ListItemDto
            {
                Title = $"{disputeCount} recorded site dispute(s)",
                Subtitle = "Documented disputes between the two coalitions in the lead-up window; these may be relevant, but causation is not established."
            });
        if (hostileCount > 0)
            result.Context.Add(new ListItemDto
            {
                Title = $"{hostileCount} military incident(s) involving both sides",
                Subtitle = "Recorded attacks, plunderings, battles or forced tribute between the factions; includes opening events when recorded."
            });
        if (diplomaticCount > 0)
            result.Context.Add(new ListItemDto
            {
                Title = $"{diplomaticCount} diplomatic or contact event(s)",
                Subtitle = "Recorded contact, diplomacy or trade disruptions. These are evidence of interaction, not necessarily worsening relations."
            });
        if (priorWars.Count > 0)
            result.Context.Add(new ListItemDto
            {
                Title = $"{priorWars.Count} previous war(s) between these factions",
                Subtitle = string.Join("; ", priorWars.Take(5).Select(w => $"{w.ToLink(true, this)} (year {w.StartYear})")) +
                    (priorWars.Count > 5 ? "; and earlier wars" : "")
            });
        if (otherCampaigns > 0)
            result.Context.Add(new ListItemDto
            {
                Title = $"{otherCampaigns} other military incident(s) involving the attacking side",
                Subtitle = "Campaigns against other or unidentified factions during the preceding ten years; context for expansion, not evidence of a motive for this war."
            });
        if (result.Context.Count == 0)
            result.Context.Add(new ListItemDto
            {
                Title = "No documented cause identified",
                Subtitle = "There may be no surviving pre-war events, or the export may not identify the factions. Do not interpret an empty timeline as proof of an unprovoked war."
            });

        void Diagnostic(string title, string subtitle) =>
            result.Diagnostics.Add(new ListItemDto { Title = title, Subtitle = subtitle });

        Diagnostic("Search window", $"Years {firstYear}–{StartYear} (up to {LeadUpWindowYears} years before the war); dated earlier-in-year events only.");
        Diagnostic("Histories searched", $"{members.Count} coalition entities and linked governments, including both war sides.");
        Diagnostic("Attached records examined", $"{incidents.Count} unique dated records within the window ({attached.Count - incidents.Count} duplicate links collapsed).");
        Diagnostic("Displayed incidents", $"{result.Timeline.Count} cross-coalition events, including verified opening events.");
        Diagnostic("Unsupported event types", $"{unsupported} records do not have a supported two-party event classification.");
        Diagnostic("Unresolved faction links", $"{unknownFaction} classified records lack two identifiable opposing sides; this includes events whose XML omits faction IDs.");
        Diagnostic("Undated group affiliations", $"{inferredAffiliations} displayed incident(s) relied on current parentage because no dated owner period was available; these are marked as possible links.");
        Diagnostic("Other or same-side records", $"{otherFaction} classified records involved the same side rather than opposing war coalitions.");
        Diagnostic("Excluded at war start", $"{excludedAtStart} events occurred after the start, were undated that year, or were opening events belonging to another war.");
        Diagnostic("Previous wars", $"{priorWars.Count} earlier completed wars found involving both coalitions.");
        return result;
    }

    public List<ListItemDto> BattleList
    {
        get
        {
            var list = new List<ListItemDto>();
            foreach (var battle in Battles)
            {
                list.Add(new ListItemDto
                {
                    Title = battle.ToLink(true, this),
                    Subtitle = battle.Subtype,
                    Append = HtmlStyleUtil.GetChipString($"{battle.DeathCount} ✝")
                });
            }
            return list;
        }
    }

    [JsonIgnore]
    public List<SiteConquered> Conquerings => EventCollections.OfType<SiteConquered>().ToList();
    [JsonIgnore]
    public List<Site?> SitesLost => Conquerings
        .Where(conquering => conquering.ConquerType == SiteConqueredType.Conquest || conquering.ConquerType == SiteConqueredType.Destruction)
        .Select(conquering => conquering.Site)
        .ToList();
    [JsonIgnore]
    public List<Site?> AttackerSitesLost => DefenderConquerings
        .Where(conquering => conquering.ConquerType == SiteConqueredType.Conquest || conquering.ConquerType == SiteConqueredType.Destruction)
        .Select(conquering => conquering.Site)
        .ToList();
    [JsonIgnore]
    public List<Site?> DefenderSitesLost => AttackerConquerings
        .Where(conquering => conquering.ConquerType == SiteConqueredType.Conquest || conquering.ConquerType == SiteConqueredType.Destruction)
        .Select(conquering => conquering.Site)
        .ToList();
    [JsonIgnore]
    public List<EventCollection> AttackerVictories { get; set; } = [];
    [JsonIgnore]
    public List<EventCollection> DefenderVictories { get; set; } = [];
    [JsonIgnore]
    public List<Battle> AttackerBattleVictories => AttackerVictories.OfType<Battle>().ToList();
    [JsonIgnore]
    public List<Battle> DefenderBattleVictories => DefenderVictories.OfType<Battle>().ToList();
    [JsonIgnore]
    public List<Battle> ErrorBattles => EventCollections.OfType<Battle>()
        .Where(battle =>
            (!battle.Attacker?.EqualsOrParentEquals(Attacker) ?? false) && (!battle.Attacker?.EqualsOrParentEquals(Defender) ?? false) ||
            (!battle.Defender?.EqualsOrParentEquals(Defender) ?? false) && (!battle.Defender?.EqualsOrParentEquals(Attacker) ?? false))
        .ToList();
    [JsonIgnore]
    public List<SiteConquered> AttackerConquerings => EventCollections.OfType<SiteConquered>().Where(conquering => conquering.Attacker?.EqualsOrParentEquals(Attacker) ?? false).ToList();
    [JsonIgnore]
    public List<SiteConquered> DefenderConquerings => EventCollections.OfType<SiteConquered>().Where(conquering => conquering.Attacker?.EqualsOrParentEquals(Defender) ?? false).ToList();
    public double AttackerToDefenderKills
    {
        get
        {
            if (AttackerDeathCount == 0 && DefenderDeathCount == 0)
            {
                return 0;
            }

            return AttackerDeathCount == 0 ? double.MaxValue : Math.Round(DefenderDeathCount / Convert.ToDouble(AttackerDeathCount), 2);
        }
    }
    public double AttackerToDefenderVictories
    {
        get
        {
            if (DefenderBattleVictories.Count == 0 && AttackerBattleVictories.Count == 0)
            {
                return 0;
            }

            return DefenderBattleVictories.Count == 0
                ? double.MaxValue
                : Math.Round(AttackerBattleVictories.Count / Convert.ToDouble(DefenderBattleVictories.Count), 2);
        }
    }

    public War(List<Property> properties, IWorld world)
        : base(properties, world)
    {
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "name": Name = Formatting.InitCaps(property.Value); break;
                case "aggressor_ent_id":
                    Attacker = world.GetEntity(Convert.ToInt32(property.Value));
                    RegisterParticipant(Attacker, true);
                    break;
                case "defender_ent_id":
                    Defender = world.GetEntity(Convert.ToInt32(property.Value));
                    RegisterParticipant(Defender, false);
                    break;
                case "involved_enid":
                    RegisterParticipant(world.GetEntity(Convert.ToInt32(property.Value)));
                    break;
            }
        }

        if (EndYear >= 0)
        {
            Length = EndYear - StartYear;
        }
        else if (world.Events.Count > 0)
        {
            Length = world.Events.Last().Year - StartYear;
        }
        Icon = HtmlStyleUtil.GetIconString("sword-cross");
    }

    public void GenerateComplexSubType()
    {
        if (string.IsNullOrEmpty(Subtype))
        {
            Subtype = $"{Attacker?.ToLink(true, this)} => {Defender?.ToLink(true, this)}";
        }
    }

    public override string ToLink(bool link = true, DwarfObject? pov = null, WorldEvent? worldEvent = null)
    {
        if (link)
        {
            string title = GetTitle();

            return pov != this
                ? HtmlStyleUtil.GetAnchorString(Icon, "war", Id, title, Name)
                : HtmlStyleUtil.GetAnchorCurrentString(Icon, title, HtmlStyleUtil.CurrentDwarfObject(Name));
        }
        return Name;
    }

    private string GetTitle()
    {
        var sb = new StringBuilder();
        sb.Append(Type);
        sb.Append("&#13");
        sb.Append(Attacker?.PrintEntity(false));
        sb.Append(" (Attacker)");
        sb.Append("&#13");
        sb.Append(Defender?.PrintEntity(false));
        sb.Append(" (Defender)");
        sb.Append("&#13");
        sb.Append("Deaths: ");
        sb.Append(DeathCount);
        sb.Append(" | (");
        sb.Append(StartYear);
        sb.Append(" - ");
        sb.Append(EndYear == -1 ? "Present" : EndYear.ToString());
        sb.Append(")");
        return sb.ToString();
    }

    public override string ToString()
    {
        return Name;
    }

    public override string GetIcon()
    {
        return Icon;
    }

    public override bool MatchesFilterCriteria(WorldObjectFilterDto filter)
    {
        if (!base.MatchesFilterCriteria(filter))
        {
            return false;
        }

        foreach (var rule in filter.Filters)
        {
            if (rule.PropertyName.Equals("IsOngoing", StringComparison.InvariantCultureIgnoreCase) &&
                rule.ViolatesBooleanCriteria(EndYear == -1))
            {
                return false;
            }

            if (rule.PropertyName.Equals(nameof(DeathCount), StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(rule.Value, out int deathCount) &&
                rule.ViolatesIntegerCriteria(DeathCount, deathCount))
            {
                return false;
            }

            if (rule.PropertyName.Equals(nameof(AttackerDeathCount), StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(rule.Value, out int attackerDeaths) &&
                rule.ViolatesIntegerCriteria(AttackerDeathCount, attackerDeaths))
            {
                return false;
            }

            if (rule.PropertyName.Equals(nameof(DefenderDeathCount), StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(rule.Value, out int defenderDeaths) &&
                rule.ViolatesIntegerCriteria(DefenderDeathCount, defenderDeaths))
            {
                return false;
            }

            if ((rule.PropertyName.Equals("BattleCount", StringComparison.InvariantCultureIgnoreCase) ||
                 rule.PropertyName.Equals("Battles", StringComparison.InvariantCultureIgnoreCase)) &&
                int.TryParse(rule.Value, out int battleCount) &&
                rule.ViolatesIntegerCriteria(Battles.Count, battleCount))
            {
                return false;
            }

            if ((rule.PropertyName.Equals("SitesLostCount", StringComparison.InvariantCultureIgnoreCase) ||
                 rule.PropertyName.Equals("SitesLost", StringComparison.InvariantCultureIgnoreCase)) &&
                int.TryParse(rule.Value, out int sitesLostCount) &&
                rule.ViolatesIntegerCriteria(SiteOutcomes.AttackerLost
                    .Concat(SiteOutcomes.DefenderLost).Distinct().Count(), sitesLostCount))
            {
                return false;
            }

            if (rule.PropertyName.Equals(nameof(Length), StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(rule.Value, out int length) &&
                rule.ViolatesIntegerCriteria(Length, length))
            {
                return false;
            }
        }

        return true;
    }
}


using System.Text;
using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Events.IncidentalEvents;
using LegendsViewer.Backend.Legends.Extensions;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Parser;
using LegendsViewer.Backend.Legends.Various;
using LegendsViewer.Backend.Legends.WorldObjects;
using LegendsViewer.Backend.Utilities;
using System.Drawing;
using System.Text.Json.Serialization;

namespace LegendsViewer.Backend.Legends.EventCollections;

public class Battle : EventCollection, IHasComplexSubtype
{
    public BattleOutcome Outcome { get; set; }
    public Location? Coordinates { get; set; }
    [JsonIgnore]
    public SiteConquered? Conquering { get; set; }
    [JsonIgnore]
    public Entity? Attacker { get; set; }
    [JsonIgnore]
    public Entity? Defender { get; set; }
    [JsonIgnore]
    public Entity? Victor { get; set; }
    [JsonIgnore]
    public List<Squad> Attackers { get; set; } = [];
    [JsonIgnore]
    public List<Squad> Defenders { get; set; } = [];
    [JsonIgnore]
    public List<HistoricalFigure> NotableAttackers { get; set; } = [];
    [JsonIgnore]
    public List<HistoricalFigure> NotableDefenders { get; set; } = [];
    [JsonIgnore]
    public List<HistoricalFigure> NonCombatants { get; set; } = [];
    [JsonIgnore]
    public List<Squad> AttackerSquads { get; set; } = [];
    [JsonIgnore]
    public List<Squad> DefenderSquads { get; set; } = [];
    [JsonIgnore]
    public int AttackerTroopCount => AttackerCount;
    [JsonIgnore]
    public int DefenderTroopCount => DefenderCount;
    public int AttackerCount => NotableAttackers.Distinct().Count() + AttackerSquads.Sum(squad => squad.Numbers);
    public int DefenderCount => NotableDefenders.Distinct().Count() + DefenderSquads.Sum(squad => squad.Numbers);
    // Keep both armies on one scale; unknown sizes receive the adult dwarf proxy.
    public const int PlaceholderCombatRating = 1200;

    public record PlaceholderRating(string Race, int Count);
    public record StrengthEstimate(double Total, int Combatants, List<PlaceholderRating> Placeholders)
    {
        public int PlaceholderCount => Placeholders.Sum(p => p.Count);
        public double? Coverage => Combatants > 0 ? 100d * (Combatants - PlaceholderCount) / Combatants : null;
    }

    [JsonIgnore]
    public (double Attacker, double Defender, string Basis, StrengthEstimate AttackerEstimate, StrengthEstimate DefenderEstimate) ArmyStrength
    {
        get
        {
            var attacker = CalculateSideStrength(true, true);
            var defender = CalculateSideStrength(false, true);
            if (attacker.Combatants > 0 && defender.Combatants > 0 &&
                attacker.PlaceholderCount == 0 && defender.PlaceholderCount == 0)
                return (attacker.Total, defender.Total, "native" + SkillBasisSuffix, attacker, defender);

            attacker = CalculateSideStrength(true, false);
            defender = CalculateSideStrength(false, false);
            bool placeholders = attacker.PlaceholderCount > 0 || defender.PlaceholderCount > 0;
            string basis = HasAnimatedCombatants ? "body-size-undead" : "body-size";
            if (placeholders) basis += "-placeholders";
            basis += SkillBasisSuffix;
            return (attacker.Total, defender.Total, basis, attacker, defender);
        }
    }

    public double AttackerArmyStrength => ArmyStrength.Attacker;
    public double DefenderArmyStrength => ArmyStrength.Defender;
    public string ArmyStrengthBasis => ArmyStrength.Basis;
    public StrengthEstimate AttackerStrengthEstimate => ArmyStrength.AttackerEstimate;
    public StrengthEstimate DefenderStrengthEstimate => ArmyStrength.DefenderEstimate;
    public double? AttackerRatingCoverage => AttackerStrengthEstimate.Coverage;
    public double? DefenderRatingCoverage => DefenderStrengthEstimate.Coverage;
    public double? ArmyStrengthRatio
    {
        get
        {
            var strength = ArmyStrength;
            return strength.Defender > 0 ? strength.Attacker / strength.Defender : null;
        }
    }

    private static bool IsAnimated(HistoricalFigure f) =>
        f.Animated || f.Zombie || f.Skeleton || f.UndeadTypes.Count > 0 || !string.IsNullOrEmpty(f.AnimatedType);

    private bool HasAnimatedCombatants =>
        AttackerSquads.Concat(DefenderSquads).Any(s => s.Numbers > 0 && s.Animated) ||
        NotableAttackers.Concat(NotableDefenders).Any(IsAnimated);

    private string SkillBasisSuffix => NotableAttackers.Concat(NotableDefenders)
        .Any(f => IndividualCombatStrength.SupportsEstimate(f) && IndividualCombatStrength.HistoricalPoints(f, StartYear, World?.CurrentYear).Total > 0)
        ? "-skill-estimate" : "";

    private StrengthEstimate CalculateSideStrength(bool attacking, bool native)
    {
        double total = 0;
        int combatants = 0;
        var placeholders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Add(CreatureInfo? race, int count, bool animated, bool transformed, string? caste = null, HistoricalFigure? figure = null)
        {
            if (count <= 0) return;
            int? rating = native && (transformed || animated) ? null :
                native ? (caste == null ? race?.NativeCombatRating : race?.NativeForCaste(caste)) :
                caste == null ? race?.EstimatedCombatRating : race?.SizeForCaste(caste) is int size && size > 0 ? Math.Max(1, size / 50) : null;
            combatants += count;
            bool knownRating = rating is > 0;
            if (!knownRating)
            {
                string name = race?.Id ?? "unknown";
                if (string.IsNullOrWhiteSpace(name)) name = "unknown";
                if (animated) name += " (undead)";
                if (transformed) name += " (transformed)";
                placeholders[name] = placeholders.GetValueOrDefault(name) + count;
                rating = PlaceholderCombatRating;
            }
            total += (double)count * (knownRating && figure != null && IndividualCombatStrength.SupportsEstimate(figure)
                ? IndividualCombatStrength.Estimate(rating!.Value, IndividualCombatStrength.HistoricalPoints(figure, StartYear, World?.CurrentYear)) : rating!.Value);
        }

        foreach (var squad in attacking ? AttackerSquads : DefenderSquads)
            Add(squad.Race, squad.Numbers, squad.Animated, false);

        foreach (var figure in (attacking ? NotableAttackers : NotableDefenders).Distinct())
            Add(figure.Race, 1, IsAnimated(figure),
                figure.CreatureTypes.Count > 0 || !string.IsNullOrEmpty(figure.PreviousRace), figure.Caste, figure);

        return new StrengthEstimate(total, combatants, placeholders.OrderBy(p => p.Key)
            .Select(p => new PlaceholderRating(p.Key, p.Value)).ToList());
    }

    public int AttackersRemainingCount => Attackers.Sum(squad => Math.Max(0, squad.Numbers - squad.Deaths));
    public int DefendersRemainingCount => Defenders.Sum(squad => Math.Max(0, squad.Numbers - squad.Deaths));
    public int DeathCount => AttackerDeathCount + DefenderDeathCount;
    [JsonIgnore]
    public Dictionary<CreatureInfo, int> Deaths { get; set; } = [];

    // Conquests and raw site events may be attached to a battle or to
    // the surrounding war. Use explicit battle links and parent ownership.
    private WarfareSiteOutcomesDto? _siteOutcomes;
    public WarfareSiteOutcomesDto SiteOutcomes =>
        _siteOutcomes ??= WarfareSiteOutcomesDto.FromBattle(this);

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
                    Title = $"Attacker{(Victor == Attacker ? " (Victorious)":"")}",
                    Subtitle = $"{Attacker.ToLink(true, this)} (Deaths: {AttackerDeathCount} ✝)",
                });
            }

            if (Defender != null)
            {
                list.Add(new ListItemDto
                {
                    Title = $"Defender{(Victor == Defender ? " (Victorious)" : "")}",
                    Subtitle = $"{Defender.ToLink(true, this)} (Deaths: {DefenderDeathCount} ✝)",
                });
            }
            WarfareSiteOutcomesDto sites = SiteOutcomes;
            list.Add(new ListItemDto
            {
                Title = "Casualties",
                Subtitle = $"Total: {DeathCount:N0} deaths | Attackers: {AttackerDeathCount:N0} | Defenders: {DefenderDeathCount:N0}"
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
                    Subtitle = $"{sites.Unattributed.Count:N0} unique sites affected by records with uncertain participants"
                });
            }
            return list;
        }
    }

    [JsonIgnore]
    public List<HistoricalFigure> NotableDeaths
    {
        get
        {
            HashSet<HistoricalFigure> deceased = GetSubEvents().OfType<HfDied>()
                .Where(death => death.HistoricalFigure != null)
                .Select(death => death.HistoricalFigure!)
                .ToHashSet();
            return NotableAttackers.Concat(NotableDefenders)
                .Distinct().Where(deceased.Contains).ToList();
        }
    }

    public List<string> NotableDeathLinks => NotableDeaths.ConvertAll(x => x.ToLink(true, this));

    public List<ListItemDto> NotableParticipants
    {
        get
        {
            var attackers = NotableAttackers.Concat(AttackerSupportMercenaryHfs).Select(f => f.Id).ToHashSet();
            var defenders = NotableDefenders.Concat(DefenderSupportMercenaryHfs).Select(f => f.Id).ToHashSet();
            var nonCombatants = NonCombatants.Select(f => f.Id).ToHashSet();
            var deaths = NotableDeaths.Select(f => f.Id).ToHashSet();
            return NotableAttackers.Concat(AttackerSupportMercenaryHfs)
                .Concat(NotableDefenders).Concat(DefenderSupportMercenaryHfs)
                .Concat(NonCombatants).Concat(NotableDeaths)
                .DistinctBy(f => f.Id)
                .Select(f =>
                {
                    string side = attackers.Contains(f.Id) && defenders.Contains(f.Id) ? "Both sides" :
                        attackers.Contains(f.Id) ? "Attacker" : defenders.Contains(f.Id) ? "Defender" :
                        nonCombatants.Contains(f.Id) ? "Non-combatant" : "Side unknown";
                    decimal? fraction = IndividualCombatStrength.SupportsEstimate(f)
                        ? IndividualCombatStrength.HistoricalSkillFraction(f, StartYear, World?.CurrentYear) : null;
                    var skills = f.Skills.Where(s => IndividualCombatStrength.IsCombatSkill(s.Name))
                        .GroupBy(s => s.Name.Replace(' ', '_'), StringComparer.OrdinalIgnoreCase)
                        .Select(g => new Skill(g.Key, (int)(Math.Max(0, g.Max(s => s.Points)) * (fraction ?? 1))))
                        .Where(s => s.Points >= 2600)
                        .OrderByDescending(s => s.Points).ThenBy(s => s.Name)
                        .Select(s => System.Net.WebUtility.HtmlEncode(s.Rank + " " + s.Name.Replace('_', ' ')))
                        .ToList();
                    string details = side + (deaths.Contains(f.Id) ? " · Died in battle" : "");
                    if (skills.Count > 0)
                        details += "<br>" + (fraction.HasValue ? "Estimated at battle: " : "Export-time skills: ") + string.Join(" · ", skills);
                    else
                        details += "<br>No recorded skilled-or-higher combat skills at " + (fraction.HasValue ? "estimated battle time" : "export time");
                    return new ListItemDto { Title = f.ToLink(true, this), Subtitle = details };
                }).ToList();
        }
    }

    // Keep animated undead separate from the living form of the same race.
    // The chart totals still reconcile with the reported troop/death totals.
    public ChartDataDto DeathsByRace => BuildSquadChart(Attackers.Concat(Defenders), squad => squad.Deaths);

    public ChartDataDto AttackerInitialArmyByRace =>
        BuildSquadChart(Attackers, squad => squad.Numbers);

    public ChartDataDto DefenderInitialArmyByRace =>
        BuildSquadChart(Defenders, squad => squad.Numbers);

    public ChartDataDto AttackerDeathsByRace =>
        BuildSquadChart(Attackers, squad => squad.Deaths);

    public ChartDataDto DefenderDeathsByRace =>
        BuildSquadChart(Defenders, squad => squad.Deaths);

    private ChartDataDto BuildSquadChart(IEnumerable<Squad> squads, Func<Squad, int> countSelector)
    {
        ChartDataDto chart = new();
        ChartDatasetDto dataset = new();
        foreach (var group in squads
                     .GroupBy(squad => (squad.Race, squad.Animated))
                     .Select(group => new { group.Key.Race, group.Key.Animated, Count = group.Sum(countSelector) })
                     .Where(group => group.Count > 0)
                     .OrderByDescending(group => group.Count)
                     .ThenBy(group => group.Race.NamePlural)
                     .ThenBy(group => group.Animated))
        {
            chart.Labels.Add(group.Animated ? $"Undead {group.Race.NamePlural}" : group.Race.NamePlural);
            dataset.Data.Add(group.Count);
            if (World != null && World.MainRaces.TryGetValue(group.Race, out var raceColor))
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

    public int AttackerDeathCount { get; set; }
    public int DefenderDeathCount { get; set; }
    public double AttackersToDefenders
    {
        get
        {
            if (AttackerCount == 0 && DefenderCount == 0)
            {
                return 0;
            }

            return DefenderCount == 0 ? double.MaxValue : Math.Round(AttackerCount / Convert.ToDouble(DefenderCount), 2);
        }
        set { }
    }
    public double AttackersToDefendersRemaining
    {
        get
        {
            if (AttackersRemainingCount == 0 && DefendersRemainingCount == 0)
            {
                return 0;
            }

            return DefendersRemainingCount == 0
                ? double.MaxValue
                : Math.Round(AttackersRemainingCount / Convert.ToDouble(DefendersRemainingCount), 2);
        }
        set { }
    }

    public bool IndividualMercenaries { get; set; }
    public bool CompanyMercenaries { get; set; }
    [JsonIgnore]
    public Entity? AttackingMercenaryEntity { get; set; }
    [JsonIgnore]
    public Entity? DefendingMercenaryEntity { get; set; }
    public bool AttackingSquadAnimated { get; set; }
    public bool DefendingSquadAnimated { get; set; }
    [JsonIgnore]
    public List<Entity> AttackerSupportMercenaryEntities { get; set; } = [];
    [JsonIgnore]
    public List<Entity> DefenderSupportMercenaryEntities { get; set; } = [];
    [JsonIgnore]
    public List<HistoricalFigure> AttackerSupportMercenaryHfs { get; set; } = [];
    [JsonIgnore]
    public List<HistoricalFigure> DefenderSupportMercenaryHfs { get; set; } = [];

    public Battle(List<Property> properties, IWorld world)
        : base(properties, world)
    {
        var attackerSquadRaces = new List<CreatureInfo>();
        var attackerSquadAnimated = new List<bool>();
        var attackerSquadEntityPopulation = new List<int>();
        var attackerSquadNumbers = new List<int>();
        var attackerSquadDeaths = new List<int>();
        var attackerSquadSite = new List<int>();
        var defenderSquadRaces = new List<CreatureInfo>();
        var defenderSquadAnimated = new List<bool>();
        var defenderSquadEntityPopulation = new List<int>();
        var defenderSquadNumbers = new List<int>();
        var defenderSquadDeaths = new List<int>();
        var defenderSquadSite = new List<int>();
        foreach (Property property in properties)
        {
            switch (property.Name)
            {
                case "outcome":
                    switch (property.Value)
                    {
                        case "attacker won": Outcome = BattleOutcome.AttackerWon; break;
                        case "defender won": Outcome = BattleOutcome.DefenderWon; break;
                        default: Outcome = BattleOutcome.Unknown; world.ParsingErrors.Report("Unknown Battle Outcome: " + property.Value); break;
                    }
                    break;
                case "name": Name = Formatting.InitCaps(property.Value); break;
                case "coords": Coordinates = Formatting.ConvertToLocation(property.Value, world); break;
                case "war_eventcol": ParentCollection = world.GetEventCollection(Convert.ToInt32(property.Value)); break;
                case "attacking_hfid":
                    HistoricalFigure? attackingHf = world.GetHistoricalFigure(Convert.ToInt32(property.Value));
                    if (attackingHf != null)
                    {
                        NotableAttackers.Add(attackingHf);
                    }
                    break;
                case "defending_hfid":
                    HistoricalFigure? defendingHf = world.GetHistoricalFigure(Convert.ToInt32(property.Value));
                    if (defendingHf != null)
                    {
                        NotableDefenders.Add(defendingHf);
                    }
                    break;
                case "attacking_squad_race":
                    attackerSquadRaces.Add(world.GetCreatureInfo(property.Value));
                    attackerSquadAnimated.Add(false);
                    break;
                case "attacking_squad_entity_pop": attackerSquadEntityPopulation.Add(Convert.ToInt32(property.Value)); break;
                case "attacking_squad_number":
                    int attackerSquadNumber = Convert.ToInt32(property.Value);
                    attackerSquadNumbers.Add(Math.Max(0, attackerSquadNumber));
                    break;
                case "attacking_squad_deaths":
                    int attackerSquadDeath = Convert.ToInt32(property.Value);
                    attackerSquadDeaths.Add(Math.Max(0, attackerSquadDeath));
                    break;
                case "attacking_squad_site": attackerSquadSite.Add(Convert.ToInt32(property.Value)); break;
                case "defending_squad_race":
                    defenderSquadRaces.Add(world.GetCreatureInfo(property.Value));
                    defenderSquadAnimated.Add(false);
                    break;
                case "defending_squad_entity_pop": defenderSquadEntityPopulation.Add(Convert.ToInt32(property.Value)); break;
                case "defending_squad_number":
                    int defenderSquadNumber = Convert.ToInt32(property.Value);
                    defenderSquadNumbers.Add(Math.Max(0, defenderSquadNumber));
                    break;
                case "defending_squad_deaths":
                    int defenderSquadDeath = Convert.ToInt32(property.Value);
                    defenderSquadDeaths.Add(Math.Max(0, defenderSquadDeath));
                    break;
                case "defending_squad_site": defenderSquadSite.Add(Convert.ToInt32(property.Value)); break;
                case "noncom_hfid":
                    HistoricalFigure? nonCombatantHf = world.GetHistoricalFigure(Convert.ToInt32(property.Value));
                    if (nonCombatantHf != null)
                    {
                        NonCombatants.Add(nonCombatantHf);
                    }
                    break;
                case "individual_merc": property.Known = true; IndividualMercenaries = true; break;
                case "company_merc": property.Known = true; CompanyMercenaries = true; break;
                case "attacking_merc_enid":
                    AttackingMercenaryEntity = world.GetEntity(Convert.ToInt32(property.Value));
                    //if (AttackingMercenaryEntity != null)
                    //{
                    //    AttackingMercenaryEntity.Type = EntityType.MercenaryCompany;
                    //}
                    break;
                case "defending_merc_enid":
                    DefendingMercenaryEntity = world.GetEntity(Convert.ToInt32(property.Value));
                    //if (DefendingMercenaryEntity != null)
                    //{
                    //    DefendingMercenaryEntity.Type = EntityType.MercenaryCompany;
                    //}
                    break;
                case "attacking_squad_animated":
                    property.Known = true;
                    // Flags are recorded on individual squads; do not mark every
                    // squad on the side as undead just because one is animated.
                    if (attackerSquadAnimated.Count > 0)
                    {
                        attackerSquadAnimated[^1] = true;
                    }
                    AttackingSquadAnimated = true;
                    break;
                case "defending_squad_animated":
                    property.Known = true;
                    if (defenderSquadAnimated.Count > 0)
                    {
                        defenderSquadAnimated[^1] = true;
                    }
                    DefendingSquadAnimated = true;
                    break;
                case "a_support_merc_enid":
                    var attackerSupportMercenaryEntity = world.GetEntity(Convert.ToInt32(property.Value));
                    if (attackerSupportMercenaryEntity != null)
                    {
                        AttackerSupportMercenaryEntities.Add(attackerSupportMercenaryEntity);
                        //attackerSupportMercenaryEntity.Type = EntityType.MercenaryCompany;
                    }
                    break;
                case "d_support_merc_enid":
                    var defenderSupportMercenaryEntity = world.GetEntity(Convert.ToInt32(property.Value));
                    if (defenderSupportMercenaryEntity != null)
                    {
                        DefenderSupportMercenaryEntities.Add(defenderSupportMercenaryEntity);
                        //defenderSupportMercenaryEntity.Type = EntityType.MercenaryCompany;
                    }
                    break;
                case "a_support_merc_hfid":
                    HistoricalFigure? attackerSupportMercenaryHf = world.GetHistoricalFigure(Convert.ToInt32(property.Value));
                    if (attackerSupportMercenaryHf != null)
                    {
                        AttackerSupportMercenaryHfs.Add(attackerSupportMercenaryHf);
                    }
                    break;
                case "d_support_merc_hfid":
                    HistoricalFigure? defenderSupportMercenaryHf = world.GetHistoricalFigure(Convert.ToInt32(property.Value));
                    if(defenderSupportMercenaryHf != null)
                    {
                        DefenderSupportMercenaryHfs.Add(defenderSupportMercenaryHf);
                    }
                    break;
            }
        }

        AttackedSite? attackedSiteEvent = Events.OfType<AttackedSite>().FirstOrDefault();
        if (attackedSiteEvent != null)
        {
            Attacker = attackedSiteEvent.Attacker;
            Defender = attackedSiteEvent.Defender;
        }
        else
        {
            FieldBattle? fieldBattleEvent = Events.OfType<FieldBattle>().FirstOrDefault();
            if (fieldBattleEvent != null)
            {
                Attacker = fieldBattleEvent.Attacker;
                Defender = fieldBattleEvent.Defender;
            }
        }

        if (ParentCollection is War war)
        {
            if (Attacker == null)
            {
                Attacker = war.Attacker;
            }
            if (Defender == null)
            {
                Defender = war.Defender;
            }
        }

        foreach (HistoricalFigure involvedHfOnAttackingSide in NotableAttackers.Where(hf => hf != null))
        {
            involvedHfOnAttackingSide.Battles.Add(this);
            involvedHfOnAttackingSide.AddEventCollection(this);
            BattleFought battleFought = new BattleFought(involvedHfOnAttackingSide, this, World, true);
            World?.Events.Add(battleFought);
            involvedHfOnAttackingSide.AddEvent(battleFought);
        }

        foreach (HistoricalFigure involvedHfOnDefendingSide in NotableDefenders.Where(hf => hf != null))
        {
            involvedHfOnDefendingSide.Battles.Add(this);
            involvedHfOnDefendingSide.AddEventCollection(this);
            BattleFought battleFought = new BattleFought(involvedHfOnDefendingSide, this, World, false);
            World?.Events.Add(battleFought);
            involvedHfOnDefendingSide.AddEvent(battleFought);
        }

        foreach (HistoricalFigure involvedSupportMercenaryOnAttackingSide in AttackerSupportMercenaryHfs.Where(hf => hf != null))
        {
            involvedSupportMercenaryOnAttackingSide.Battles.Add(this);
            involvedSupportMercenaryOnAttackingSide.AddEventCollection(this);
            BattleFought battleFought = new BattleFought(involvedSupportMercenaryOnAttackingSide, this, World, true, true, true);
            World?.Events.Add(battleFought);
            involvedSupportMercenaryOnAttackingSide.AddEvent(battleFought);
        }

        foreach (HistoricalFigure involvedSupportMercenaryOnDefendingSide in DefenderSupportMercenaryHfs.Where(hf => hf != null))
        {
            involvedSupportMercenaryOnDefendingSide.Battles.Add(this);
            involvedSupportMercenaryOnDefendingSide.AddEventCollection(this);
            BattleFought battleFought = new BattleFought(involvedSupportMercenaryOnDefendingSide, this, World, false, true, true);
            World?.Events.Add(battleFought);
            involvedSupportMercenaryOnDefendingSide.AddEvent(battleFought);
        }

        for (int i = 0; i < attackerSquadRaces.Count; i++)
        {
            int deaths = attackerSquadDeaths.ElementAtOrDefault(i);
            int number = Math.Max(attackerSquadNumbers.ElementAtOrDefault(i), deaths);
            int site = i < attackerSquadSite.Count ? attackerSquadSite[i] : -1;
            int population = i < attackerSquadEntityPopulation.Count ? attackerSquadEntityPopulation[i] : -1;
            AttackerSquads.Add(new Squad(attackerSquadRaces[i], number, deaths, site, population, attackerSquadAnimated[i]));
        }

        for (int i = 0; i < defenderSquadRaces.Count; i++)
        {
            int deaths = defenderSquadDeaths.ElementAtOrDefault(i);
            int number = Math.Max(defenderSquadNumbers.ElementAtOrDefault(i), deaths);
            int site = i < defenderSquadSite.Count ? defenderSquadSite[i] : -1;
            int population = i < defenderSquadEntityPopulation.Count ? defenderSquadEntityPopulation[i] : -1;
            DefenderSquads.Add(new Squad(defenderSquadRaces[i], number, deaths, site, population, defenderSquadAnimated[i]));
        }

        HashSet<HistoricalFigure> deceasedFigures = GetSubEvents().OfType<HfDied>()
            .Where(death => death.HistoricalFigure != null)
            .Select(death => death.HistoricalFigure!)
            .ToHashSet();

        Attackers = AggregateCombatants(AttackerSquads, NotableAttackers, deceasedFigures);
        Defenders = AggregateCombatants(DefenderSquads, NotableDefenders, deceasedFigures);

        Deaths = [];
        foreach (Squad squad in Attackers.Concat(Defenders))
        {
            if (Deaths.TryGetValue(squad.Race, out int currentDeaths))
            {
                Deaths[squad.Race] = currentDeaths + squad.Deaths;
            }
            else
            {
                Deaths[squad.Race] = squad.Deaths;
            }
        }

        AttackerDeathCount = Attackers.Sum(attacker => attacker.Deaths);
        DefenderDeathCount = Defenders.Sum(defender => defender.Deaths);

        if (Outcome == BattleOutcome.AttackerWon)
        {
            Victor = Attacker;
        }
        else if (Outcome == BattleOutcome.DefenderWon)
        {
            Victor = Defender;
        }

        if (ParentCollection is War parentWar)
        {
            parentWar.RegisterOpponents(Attacker, Defender);

            bool battleAttackerBelongsToWarAttacker =
                BelongsToWarSide(Attacker, parentWar.Attacker) ||
                (!BelongsToWarSide(Attacker, parentWar.Defender) &&
                 BelongsToWarSide(Defender, parentWar.Defender));

            if (battleAttackerBelongsToWarAttacker)
            {
                parentWar.AttackerDeathCount += AttackerDeathCount;
                parentWar.DefenderDeathCount += DefenderDeathCount;
            }
            else
            {
                parentWar.AttackerDeathCount += DefenderDeathCount;
                parentWar.DefenderDeathCount += AttackerDeathCount;
            }

            parentWar.DeathCount += DeathCount;

            if (Outcome == BattleOutcome.AttackerWon)
            {
                if (battleAttackerBelongsToWarAttacker)
                {
                    parentWar.AttackerVictories.Add(this);
                }
                else
                {
                    parentWar.DefenderVictories.Add(this);
                }
            }
            else if (Outcome == BattleOutcome.DefenderWon)
            {
                if (battleAttackerBelongsToWarAttacker)
                {
                    parentWar.DefenderVictories.Add(this);
                }
                else
                {
                    parentWar.AttackerVictories.Add(this);
                }
            }
        }
        Region?.Battles.Add(this);
        UndergroundRegion?.Battles.Add(this);

        if (attackerSquadDeaths.Sum() + defenderSquadDeaths.Sum() + Events.OfType<HfDied>().Count() == 0)
        {
            Notable = false;
        }

        if (attackerSquadNumbers.Sum() + NotableAttackers.Count > (defenderSquadNumbers.Sum() + NotableDefenders.Count) * 10 //NotableDefenders outnumbered 10 to 1
            && Victor == Attacker
            && AttackerDeathCount < (NotableAttackers.Count + attackerSquadNumbers.Sum()) * 0.1) //NotableAttackers losses < 10%
        {
            Notable = false;
        }
        Attacker?.AddEventCollection(this);
        if (Defender != Attacker)
        {
            Defender?.AddEventCollection(this);
        }

        Icon = HtmlStyleUtil.GetIconString("chess-bishop");
    }

    private static bool BelongsToWarSide(Entity? entity, Entity? side)
    {
        return entity != null && side != null &&
            (entity.EqualsOrParentEquals(side) || entity.CurrentCiv == side);
    }

    internal bool IsUndeadCombatant(HistoricalFigure figure)
    {
        // Reuse the battle's dated classification when attributing duel deaths.
        // Race alone is no longer unique when living and animated troops coexist.
        return figure.UndeadTypes.Count > 0
            ? figure.UndeadTypes.Any(type => type.StartYear <= StartYear)
            : figure.Skeleton || figure.Zombie || figure.Ghost || figure.Animated;
    }

    private List<Squad> AggregateCombatants(
        IEnumerable<Squad> squads,
        IEnumerable<HistoricalFigure> notableFigures,
        HashSet<HistoricalFigure> deceasedFigures)
    {
        Dictionary<(CreatureInfo Race, bool Animated), Squad> grouped = [];
        foreach (Squad squad in squads)
        {
            var key = (squad.Race, squad.Animated);
            if (!grouped.TryGetValue(key, out Squad? aggregate))
            {
                aggregate = new Squad(squad.Race, 0, 0, -1, -1, squad.Animated);
                grouped.Add(key, aggregate);
            }
            aggregate.Numbers += squad.Numbers;
            aggregate.Deaths += squad.Deaths;
        }

        foreach (HistoricalFigure figure in notableFigures.Distinct())
        {
            bool undead = IsUndeadCombatant(figure);
            var key = (figure.Race, undead);
            if (!grouped.TryGetValue(key, out Squad? aggregate))
            {
                aggregate = new Squad(figure.Race, 0, 0, -1, -1, undead);
                grouped.Add(key, aggregate);
            }
            aggregate.Numbers++;
            if (deceasedFigures.Contains(figure))
            {
                aggregate.Deaths++;
            }
        }
        return grouped.Values.ToList();
    }

    public void GenerateComplexSubType()
    {
        if (string.IsNullOrEmpty(Subtype))
        {
            Subtype = $"{Attacker?.ToLink(true, this)}{(Victor != null && Victor == Attacker ? "(V)" : "")} => {Defender?.ToLink(true, this)}{(Victor != null && Victor == Defender ? "(V)" : "")}";
        }
    }

    public class Squad
    {
        public const int MAX_SIZE = 100;
        public CreatureInfo Race { get; set; }
        public int Numbers { get; set; }
        public int Deaths { get; set; }
        public int Site { get; set; }
        public int Population { get; set; }
        public bool Animated { get; set; }
        public Squad(CreatureInfo race, int numbers, int deaths, int site, int population, bool animated = false)
        {
            Race = race;
            Numbers = numbers;
            Deaths = deaths;
            Site = site;
            Population = population;
            Animated = animated;
        }
    }

    public override string ToLink(bool link = true, DwarfObject? pov = null, WorldEvent? worldEvent = null)
    {
        if (link)
        {
            string title = GetTitle();

            string linkedString = pov != this
                ? HtmlStyleUtil.GetAnchorString(Icon, "battle", Id, title, Name)
                : HtmlStyleUtil.GetAnchorCurrentString(Icon, title, HtmlStyleUtil.CurrentDwarfObject(Name));
            return linkedString;
        }
        return Name;
    }

    private string GetTitle()
    {
        var sb = new StringBuilder();
        sb.Append(Type);
        sb.Append("&#13");
        sb.Append(Attacker != null ? Attacker.PrintEntity(false) : "UNKNOWN");
        sb.Append(" (Attacker)");
        if (Victor != null && Victor == Attacker)
        {
            sb.Append("(V)");
        }

        sb.Append("&#13");
        sb.Append("Kills: ");
        sb.Append(DefenderDeathCount);
        sb.Append("&#13");
        sb.Append(Defender != null ? Defender.PrintEntity(false) : "UNKNOWN");
        sb.Append(" (Defender)");
        if (Victor != null && Victor == Defender)
        {
            sb.Append("(V)");
        }

        sb.Append("&#13");
        sb.Append("Kills: ");
        sb.Append(AttackerDeathCount);
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
            if (rule.PropertyName.Equals("HasMercenaries", StringComparison.InvariantCultureIgnoreCase) &&
                rule.ViolatesBooleanCriteria(IndividualMercenaries || CompanyMercenaries ||
                                            AttackingMercenaryEntity != null || DefendingMercenaryEntity != null ||
                                            AttackerSupportMercenaryEntities.Count > 0 || DefenderSupportMercenaryEntities.Count > 0 ||
                                            AttackerSupportMercenaryHfs.Count > 0 || DefenderSupportMercenaryHfs.Count > 0))
            {
                return false;
            }

            if (rule.PropertyName.Equals("HasUndeadSquads", StringComparison.InvariantCultureIgnoreCase) &&
                rule.ViolatesBooleanCriteria(AttackingSquadAnimated || DefendingSquadAnimated))
            {
                return false;
            }

            if (rule.PropertyName.Equals("HasNotableDefenders", StringComparison.InvariantCultureIgnoreCase) &&
                rule.ViolatesBooleanCriteria(NotableDefenders.Count > 0))
            {
                return false;
            }

            if ((rule.PropertyName.Equals("Outcome", StringComparison.InvariantCultureIgnoreCase) ||
                 rule.PropertyName.Equals("BattleOutcome", StringComparison.InvariantCultureIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(rule.Value))
            {
                string outcomeStr = Outcome.ToString();
                string outcomeDesc = Outcome.GetDescription();
                if (rule.Operator == FilterOperator.Equals &&
                    !outcomeStr.Equals(rule.Value, StringComparison.InvariantCultureIgnoreCase) &&
                    !outcomeDesc.Equals(rule.Value, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
                if (rule.Operator == FilterOperator.NotEquals &&
                    (outcomeStr.Equals(rule.Value, StringComparison.InvariantCultureIgnoreCase) ||
                     outcomeDesc.Equals(rule.Value, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return false;
                }
            }

            if ((rule.PropertyName.Equals("TotalDeaths", StringComparison.InvariantCultureIgnoreCase) ||
                 rule.PropertyName.Equals("DeathCount", StringComparison.InvariantCultureIgnoreCase)) &&
                int.TryParse(rule.Value, out int totalDeaths) &&
                rule.ViolatesIntegerCriteria(DeathCount, totalDeaths))
            {
                return false;
            }

            if (rule.PropertyName.Equals("AttackerCount", StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(rule.Value, out int attackerCount) &&
                rule.ViolatesIntegerCriteria(AttackerCount, attackerCount))
            {
                return false;
            }

            if (rule.PropertyName.Equals("DefenderCount", StringComparison.InvariantCultureIgnoreCase) &&
                int.TryParse(rule.Value, out int defenderCount) &&
                rule.ViolatesIntegerCriteria(DefenderCount, defenderCount))
            {
                return false;
            }

            if ((rule.PropertyName.Equals("AttackerDeaths", StringComparison.InvariantCultureIgnoreCase) ||
                 rule.PropertyName.Equals("AttackerDeathCount", StringComparison.InvariantCultureIgnoreCase)) &&
                int.TryParse(rule.Value, out int attackerDeaths) &&
                rule.ViolatesIntegerCriteria(AttackerDeathCount, attackerDeaths))
            {
                return false;
            }

            if ((rule.PropertyName.Equals("DefenderDeaths", StringComparison.InvariantCultureIgnoreCase) ||
                 rule.PropertyName.Equals("DefenderDeathCount", StringComparison.InvariantCultureIgnoreCase)) &&
                int.TryParse(rule.Value, out int defenderDeaths) &&
                rule.ViolatesIntegerCriteria(DefenderDeathCount, defenderDeaths))
            {
                return false;
            }
        }

        return true;
    }
}


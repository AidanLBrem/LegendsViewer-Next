using System.Runtime.CompilerServices;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.WorldObjects;

namespace LegendsViewer.Backend.Contracts;

/// <summary>
/// Distinct sites affected by recorded actions involving the two warfare sides.
/// Repeated captures of a site count once in each applicable category, not once
/// per event. Events whose affiliation is genuinely unknown are exposed rather
/// than silently omitted.
/// </summary>
public class WarfareSiteOutcomesDto
{
    public List<string> AttackerGained { get; set; } = [];
    public List<string> AttackerLost { get; set; } = [];
    public List<string> AttackerPlundered { get; set; } = [];
    public List<string> DefenderGained { get; set; } = [];
    public List<string> DefenderLost { get; set; } = [];
    public List<string> DefenderPlundered { get; set; } = [];
    public List<string> Unattributed { get; set; } = [];

    private enum Action { Conquest, Destruction, Plunder, Gain, Loss }

    // Index events linked by their parent pointers exactly once per imported
    // world. This avoids scanning millions of world events separately for every
    // war on the sortable overview page.
    private static readonly ConditionalWeakTable<IWorld, LinkedEventIndex> EventIndexes = new();

    public static WarfareSiteOutcomesDto FromWar(War war)
    {
        LinkedEventIndex? index = war.World == null
            ? null : EventIndexes.GetValue(war.World, static world => new LinkedEventIndex(world));
        List<EventCollection> collections = CollectCollections(war);
        IEnumerable<SiteConquered> conquests = collections.OfType<SiteConquered>();
        if (index != null && index.WarConquests.TryGetValue(war, out var linkedConquests))
        {
            conquests = conquests.Concat(linkedConquests);
        }

        IEnumerable<WorldEvent> events = collections.SelectMany(collection => collection.Events);
        if (index != null && index.WarEvents.TryGetValue(war, out var linkedEvents))
        {
            events = events.Concat(linkedEvents);
        }
        return Build(conquests, events, war.Attacker, war.Defender);
    }

    public static WarfareSiteOutcomesDto FromBattle(Battle battle)
    {
        LinkedEventIndex? index = battle.World == null
            ? null : EventIndexes.GetValue(battle.World, static world => new LinkedEventIndex(world));
        List<EventCollection> collections = CollectCollections(battle);
        IEnumerable<SiteConquered> conquests = collections.OfType<SiteConquered>();
        if (battle.Conquering != null)
        {
            conquests = conquests.Append(battle.Conquering);
        }
        if (battle.ParentCollection is War war)
        {
            conquests = conquests.Concat(war.Conquerings.Where(conquest => conquest.Battle == battle));
        }
        if (index != null && index.BattleConquests.TryGetValue(battle, out var linkedConquests))
        {
            conquests = conquests.Concat(linkedConquests);
        }

        IEnumerable<WorldEvent> events = collections.SelectMany(collection => collection.Events);
        if (index != null && index.BattleEvents.TryGetValue(battle, out var linkedEvents))
        {
            events = events.Concat(linkedEvents);
        }
        return Build(conquests, events, battle.Attacker, battle.Defender);
    }

    // Kept for callers that only possess collection data (including imported
    // save regression tests).
    public static WarfareSiteOutcomesDto FromConquerings(
        IEnumerable<SiteConquered> conquests, Entity? attacker, Entity? defender) =>
        Build(conquests, [], attacker, defender);

    private static WarfareSiteOutcomesDto Build(
        IEnumerable<SiteConquered> conquests,
        IEnumerable<WorldEvent> events,
        Entity? attacker, Entity? defender)
    {
        Builder builder = new(attacker, defender);
        HashSet<WorldEvent> handled = [];
        foreach (SiteConquered conquest in conquests.Distinct())
        {
            List<WorldEvent> childEvents = CollectCollections(conquest)
                .SelectMany(collection => collection.Events).Distinct().ToList();
            bool hasConcreteOutcome = false;
            foreach (WorldEvent worldEvent in childEvents)
            {
                if (builder.ApplyEvent(worldEvent, conquest))
                {
                    hasConcreteOutcome = true;
                }
                handled.Add(worldEvent);
            }

            // Some exports contain only the collection's outcome and a child
            // attack with the site ID; some contain detailed result events.
            // Prefer the actual events when they exist.
            if (!hasConcreteOutcome)
            {
                Site? site = conquest.Site ?? childEvents.Select(GetSite).FirstOrDefault(x => x != null);
                switch (conquest.ConquerType)
                {
                    case SiteConqueredType.Conquest:
                        builder.Apply(site, conquest.Attacker, conquest.Defender, Action.Conquest);
                        break;
                    case SiteConqueredType.Destruction:
                        builder.Apply(site, conquest.Attacker, conquest.Defender, Action.Destruction);
                        break;
                    case SiteConqueredType.Pillaging:
                        builder.Apply(site, conquest.Attacker, conquest.Defender, Action.Plunder);
                        break;
                }
            }
        }

        foreach (WorldEvent worldEvent in events.Distinct())
        {
            if (handled.Add(worldEvent))
            {
                builder.ApplyEvent(worldEvent);
            }
        }

        return builder.Result;
    }

    private static List<EventCollection> CollectCollections(EventCollection root)
    {
        List<EventCollection> result = [];
        Stack<EventCollection> pending = new();
        HashSet<EventCollection> visited = [];
        pending.Push(root);
        while (pending.Count > 0)
        {
            EventCollection current = pending.Pop();
            if (!visited.Add(current)) continue;
            result.Add(current);
            foreach (EventCollection child in current.EventCollections)
            {
                pending.Push(child);
            }
        }
        return result;
    }

    private static Site? GetSite(WorldEvent e) => e switch
    {
        SiteTakenOver change => change.Site,
        NewSiteLeader leader => leader.Site,
        SiteSurrendered surrender => surrender.Site,
        ReclaimSite reclaim => reclaim.Site,
        DestroyedSite destroyed => destroyed.Site,
        HfDestroyedSite hfDestroyed => hfDestroyed.Site,
        PlunderedSite plunder => plunder.Site,
        SiteAbandoned abandoned => abandoned.Site,
        SiteDied died => died.Site,
        AttackedSite attacked => attacked.Site,
        _ => null
    };

    private sealed class Builder(Entity? attacker, Entity? defender)
    {
        public WarfareSiteOutcomesDto Result { get; } = new();
        private readonly HashSet<int> _attackerGained = [];
        private readonly HashSet<int> _attackerLost = [];
        private readonly HashSet<int> _attackerPlundered = [];
        private readonly HashSet<int> _defenderGained = [];
        private readonly HashSet<int> _defenderLost = [];
        private readonly HashSet<int> _defenderPlundered = [];
        private readonly HashSet<int> _unattributed = [];

        // Return true only when this is an actual territorial event.
        // AttackedSite by itself does not prove a victory or ownership change.
        public bool ApplyEvent(WorldEvent e, SiteConquered? collection = null)
        {
            Entity? fallbackActor = collection?.Attacker;
            Entity? fallbackVictim = collection?.Defender;
            Site? fallbackSite = collection?.Site;
            switch (e)
            {
                case SiteTakenOver take:
                    Apply(take.Site ?? fallbackSite, take.Attacker ?? fallbackActor,
                        take.Defender ?? fallbackVictim, Action.Conquest);
                    return true;
                case NewSiteLeader leader:
                    Apply(leader.Site ?? fallbackSite, leader.Attacker ?? fallbackActor,
                        leader.Defender ?? fallbackVictim, Action.Conquest);
                    return true;
                case SiteSurrendered surrender:
                    Apply(surrender.Site ?? fallbackSite, surrender.Attacker ?? fallbackActor,
                        surrender.Defender ?? fallbackVictim, Action.Conquest);
                    return true;
                case DestroyedSite destroyed:
                    Apply(destroyed.Site ?? fallbackSite, destroyed.Attacker ?? fallbackActor,
                        destroyed.Defender ?? destroyed.SiteEntity ?? fallbackVictim, Action.Destruction);
                    return true;
                case HfDestroyedSite hfDestroyed:
                    Apply(hfDestroyed.Site ?? fallbackSite, fallbackActor,
                        hfDestroyed.DefenderCiv ?? hfDestroyed.SiteCiv ?? fallbackVictim, Action.Destruction);
                    return true;
                case PlunderedSite plunder:
                    Apply(plunder.Site ?? fallbackSite, plunder.Attacker ?? fallbackActor,
                        plunder.Defender ?? plunder.SiteEntity ?? fallbackVictim, Action.Plunder);
                    return true;
                case ReclaimSite reclaim:
                    Apply(reclaim.Site ?? fallbackSite, reclaim.Civ ?? reclaim.SiteEntity ?? fallbackActor,
                        null, Action.Gain);
                    return true;
                case SiteAbandoned abandoned:
                    Apply(abandoned.Site ?? fallbackSite, null,
                        abandoned.Civ ?? abandoned.SiteEntity ?? fallbackVictim, Action.Loss);
                    return true;
                case SiteDied died:
                    Apply(died.Site ?? fallbackSite, null,
                        died.Civ ?? died.SiteEntity ?? fallbackVictim, Action.Loss);
                    return true;
                default:
                    return false;
            }
        }

        public void Apply(Site? site, Entity? actor, Entity? victim, Action action)
        {
            if (site == null) return;

            int actorSide = SideOf(actor);
            int victimSide = SideOf(victim);
            bool recorded = false;

            // An explicitly named third-party attacker is not automatically
            // a victory for either war side, even when its victim is one of
            // the two belligerents.
            if (actor != null && actorSide == 0 && action != Action.Loss)
            {
                Add(Result.Unattributed, _unattributed, site);
                return;
            }

            // Two subordinate factions of the same war side exchanging a
            // leader or site are not territorial gains between war sides.
            if (actorSide != 0 && actorSide == victimSide &&
                (action == Action.Conquest || action == Action.Gain))
            {
                return;
            }

            if (action is Action.Conquest or Action.Gain)
            {
                if (actorSide == 1)
                {
                    Add(Result.AttackerGained, _attackerGained, site);
                    recorded = true;
                }
                else if (actorSide == 2)
                {
                    Add(Result.DefenderGained, _defenderGained, site);
                    recorded = true;
                }
            }

            if (action is Action.Conquest or Action.Destruction or Action.Loss)
            {
                if (victimSide == 1)
                {
                    Add(Result.AttackerLost, _attackerLost, site);
                    recorded = true;
                }
                else if (victimSide == 2)
                {
                    Add(Result.DefenderLost, _defenderLost, site);
                    recorded = true;
                }
            }

            if (action == Action.Plunder)
            {
                if (actorSide == 1)
                {
                    Add(Result.AttackerPlundered, _attackerPlundered, site);
                    recorded = true;
                }
                else if (actorSide == 2)
                {
                    Add(Result.DefenderPlundered, _defenderPlundered, site);
                    recorded = true;
                }
            }

            // Explicitly surface unresolved affiliations rather than hiding
            // otherwise linked site events from the war detail page.
            if (!recorded) Add(Result.Unattributed, _unattributed, site);
        }

        private int SideOf(Entity? participant)
        {
            if (participant == null) return 0;
            if (participant == attacker) return 1;
            if (participant == defender) return 2;

            bool attackSide = Matches(participant, attacker);
            bool defenseSide = Matches(participant, defender);
            return attackSide == defenseSide ? 0 : attackSide ? 1 : 2;
        }

        private static bool Matches(Entity participant, Entity? side) =>
            side != null && (participant.EqualsOrParentEquals(side) ||
                             participant.MapNation == side.MapNation);

        private static void Add(List<string> links, HashSet<int> ids, Site site)
        {
            if (ids.Add(site.Id)) links.Add(site.ToLink());
        }
    }

    private sealed class LinkedEventIndex
    {
        public Dictionary<War, List<SiteConquered>> WarConquests { get; } = [];
        public Dictionary<War, List<WorldEvent>> WarEvents { get; } = [];
        public Dictionary<Battle, List<SiteConquered>> BattleConquests { get; } = [];
        public Dictionary<Battle, List<WorldEvent>> BattleEvents { get; } = [];

        public LinkedEventIndex(IWorld world)
        {
            foreach (SiteConquered conquest in world.SiteConquerings)
            {
                War? war = FindAncestor<War>(conquest.ParentCollection);
                Battle? battle = conquest.Battle ?? FindAncestor<Battle>(conquest.ParentCollection);
                if (war != null) Add(WarConquests, war, conquest);
                if (battle != null) Add(BattleConquests, battle, conquest);
            }

            // Some legends exports link an event by ParentCollection but omit
            // it from the parent's Events array. Include those references too.
            foreach (WorldEvent e in world.Events)
            {
                if (!IsTerritorialEvent(e)) continue;
                War? war = FindAncestor<War>(e.ParentCollection);
                Battle? battle = FindAncestor<Battle>(e.ParentCollection);
                if (war != null) Add(WarEvents, war, e);
                if (battle != null) Add(BattleEvents, battle, e);
            }
        }

        private static T? FindAncestor<T>(EventCollection? collection) where T : EventCollection
        {
            HashSet<EventCollection> seen = [];
            while (collection != null && seen.Add(collection))
            {
                if (collection is T match) return match;
                collection = collection.ParentCollection;
            }
            return null;
        }

        private static bool IsTerritorialEvent(WorldEvent e) =>
            e is SiteTakenOver or NewSiteLeader or SiteSurrendered or
                ReclaimSite or DestroyedSite or HfDestroyedSite or
                PlunderedSite or SiteAbandoned or SiteDied;

        private static void Add<T, TValue>(Dictionary<T, List<TValue>> groups, T key, TValue value)
            where T : notnull
        {
            if (!groups.TryGetValue(key, out List<TValue>? values))
            {
                values = [];
                groups.Add(key, values);
            }
            values.Add(value);
        }
    }
}

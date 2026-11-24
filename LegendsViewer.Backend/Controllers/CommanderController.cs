using LegendsViewer.Backend.Legends.Enums;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.Legends.Events;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Controllers;

[ApiController]
[Route("api/commanders")]
public class CommanderController(IWorld world) : ControllerBase
{
    [HttpGet]
    public IEnumerable<CommanderRecord> Get()
    {
        var records = new Dictionary<int, CommanderRecord>();
        var commands = new Dictionary<(int FigureId, Battle Battle), bool?>();

        foreach (var entry in world.Events)
        {
            (HistoricalFigure? attacker, HistoricalFigure? defender) = entry switch
            {
                FieldBattle field => (field.AttackerGeneral, field.DefenderGeneral),
                AttackedSite site => (site.AttackerGeneral, site.DefenderGeneral),
                _ => (null, null)
            };
            if (attacker == null && defender == null) continue;

            var parent = entry.ParentCollection;
            while (parent != null && parent is not Battle)
            {
                if (parent is SiteConquered { Battle: { } linkedBattle })
                {
                    parent = linkedBattle;
                    break;
                }
                parent = parent.ParentCollection;
            }
            AddCommand(attacker, parent as Battle, true);
            AddCommand(defender, parent as Battle, false);
        }

        foreach (var ((figureId, battle), attacking) in commands.OrderBy(c => c.Key.Battle.StartYear).ThenBy(c => c.Key.Battle.Id))
        {
            if (attacking == null) continue;
            var record = records[figureId];
            record.AddBattle(battle, attacking.Value);
        }
        return records.Values.OrderBy(r => r.Name).ThenBy(r => r.Id);

        void AddCommand(HistoricalFigure? figure, Battle? battle, bool attacking)
        {
            if (figure == null) return;
            if (!records.ContainsKey(figure.Id))
                records.Add(figure.Id, new CommanderRecord(figure.Id, figure.Name));
            if (battle == null) return;

            var key = (figure.Id, battle);
            if (!commands.TryAdd(key, attacking) && commands[key] != attacking)
                commands[key] = null; // The export assigns this figure to both sides.
        }
    }
}

public record CommanderRecord(int Id, string Name)
{
    public int Victories { get; set; }
    public int Losses { get; set; }
    public long EnemiesKilled { get; set; }
    public double? WinPercentage => Victories + Losses == 0 ? null : 100d * Victories / (Victories + Losses);
    public long OwnDeaths { get; private set; }
    public MatchupRecord Outnumbered { get; } = new();
    public MatchupRecord NearPeer { get; } = new();
    public MatchupRecord Favored { get; } = new();
    public List<CommandedBattle> Battles { get; } = [];
    public int RatedBattles { get; private set; }
    private double _weightedWinsAboveExpected;
    private double _totalBattleWeight;
    public int Experience => Battles.Count;
    public double? BattlePerformance => RatedBattles == 0 ? null : 100 * _weightedWinsAboveExpected / (_totalBattleWeight + 3d);
    public double? CommandScore => BattlePerformance * (RatedBattles / (RatedBattles + 10d));

    public void AddBattle(Battle battle, bool attacking)
    {
        int enemyDeaths = attacking ? battle.DefenderDeathCount : battle.AttackerDeathCount;
        int ownDeaths = attacking ? battle.AttackerDeathCount : battle.DefenderDeathCount;

        EnemiesKilled += enemyDeaths;
        OwnDeaths += ownDeaths;

        bool decided = battle.Outcome != BattleOutcome.Unknown;
        bool won = decided && attacking == (battle.Outcome == BattleOutcome.AttackerWon);
        if (won) Victories++;
        else if (decided) Losses++;

        int own = attacking ? battle.AttackerCount : battle.DefenderCount;
        int enemy = attacking ? battle.DefenderCount : battle.AttackerCount;
        Battles.Add(new CommandedBattle(battle.Id, battle.Name, battle.StartYear,
            attacking ? "Attacker" : "Defender", decided ? (won ? "Victory" : "Loss") : "Unknown",
            own, enemy, enemyDeaths, ownDeaths,
            DescribeTroops(attacking ? battle.Attackers : battle.Defenders),
            DescribeTroops(attacking ? battle.Defenders : battle.Attackers)));
        if (own <= 0 || enemy <= 0) return;

        var matchup = enemy >= 2L * own ? Outnumbered : own >= 2L * enemy ? Favored : NearPeer;
        matchup.Battles++;
        if (won) matchup.Wins++;
        if (!decided) return;

        RatedBattles++;
        double totalTroops = (double)own + enemy;
        double weight = Math.Sqrt(totalTroops / 100d);
        _weightedWinsAboveExpected += weight * ((won ? 1 : 0) - own / totalTroops);
        _totalBattleWeight += weight;
    }

    private static string DescribeTroops(List<Battle.Squad> squads) =>
        string.Join(", ", squads.Where(s => s.Numbers > 0).OrderByDescending(s => s.Numbers)
            .Select(s => $"{s.Numbers} {(s.Animated ? "undead " : "")}{(s.Numbers == 1 ? s.Race.NameSingular : s.Race.NamePlural)}"));
}

public record CommandedBattle(int Id, string Name, int Year, string Side, string Outcome,
    int OwnTroops, int EnemyTroops, int OwnKills, int EnemyKills, string OwnComposition, string EnemyComposition);

public class MatchupRecord
{
    public int Battles { get; set; }
    public int Wins { get; set; }
}

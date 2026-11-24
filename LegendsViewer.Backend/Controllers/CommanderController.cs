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

        foreach (var ((figureId, battle), attacking) in commands)
        {
            if (attacking == null) continue;
            var record = records[figureId];
            record.EnemiesKilled += attacking.Value ? battle.DefenderDeathCount : battle.AttackerDeathCount;
            if (battle.Outcome == BattleOutcome.Unknown) continue;

            if (attacking.Value == (battle.Outcome == BattleOutcome.AttackerWon))
                record.Victories++;
            else
                record.Losses++;
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
}

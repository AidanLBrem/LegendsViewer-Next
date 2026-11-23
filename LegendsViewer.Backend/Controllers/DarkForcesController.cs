using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Various;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Controllers;

[ApiController]
[Route("api/dark-forces")]
public class DarkForcesController(IWorld world) : ControllerBase
{
    private IReadOnlyList<DarkForceDto> All =>
        world is World loaded ? loaded.GetOrComputeDarkForces() :
        DarkForcesAnalysis.Calculate(world);

    [HttpGet]
    public ActionResult<DarkForcesPageDto> Get(
        string? search = null, string? status = null, string? sort = null,
        string? direction = null, int page = 1, int pageSize = 25,
        int? minApprentices = null, int? maxApprentices = null)
    {
        if (page < 1 || pageSize is < 1 or > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue)
            return BadRequest("Use a positive page and page size from 1 to 100.");
        if (status is not (null or "fallen" or "surviving" or "deceased" or "realm-lost"))
            return BadRequest("Unknown status filter.");
        if (minApprentices < 0 || maxApprentices < 0 ||
            (minApprentices.HasValue && maxApprentices.HasValue &&
             minApprentices.Value > maxApprentices.Value))
            return BadRequest("Apprentice filters must be nonnegative and minimum cannot exceed maximum.");
        if (sort is not (null or "power" or "territory" or "invasions" or "fallen" or "name" or "apprentices"))
            return BadRequest("Unknown sort field.");
        if (direction is not (null or "asc" or "desc"))
            return BadRequest("Unknown sort direction.");

        var all = All;
        var matching = all.Where(n =>
            (string.IsNullOrWhiteSpace(search) ||
                n.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                n.Factions.Any(f => f.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                n.Groups.Any(g => g.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                n.Apprentices.Any(a => a.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))) &&
            (!minApprentices.HasValue || n.ApprenticeCount >= minApprentices.Value) &&
            (!maxApprentices.HasValue || n.ApprenticeCount <= maxApprentices.Value) &&
            (status switch
            {
                "fallen" => n.Defeated,
                "surviving" => !n.Defeated,
                "deceased" => n.DefeatYear.HasValue,
                "realm-lost" => n.Status == "Realm lost",
                _ => true
            })).ToList();

        IEnumerable<DarkForceDto> sorted = sort switch
        {
            "territory" => direction == "asc"
                ? matching.OrderBy(n => n.PeakSettlements).ThenBy(n => n.FigureId)
                : matching.OrderByDescending(n => n.PeakSettlements)
                    .ThenByDescending(n => n.Conquests).ThenBy(n => n.FigureId),
            "apprentices" => direction == "asc"
                ? matching.OrderBy(n => n.ApprenticeCount).ThenBy(n => n.FigureId)
                : matching.OrderByDescending(n => n.ApprenticeCount)
                    .ThenByDescending(n => n.PowerIndex).ThenBy(n => n.FigureId),
            "invasions" => direction == "asc"
                ? matching.OrderBy(n => n.Invasions).ThenBy(n => n.FigureId)
                : matching.OrderByDescending(n => n.Invasions)
                    .ThenByDescending(n => n.Conquests).ThenBy(n => n.FigureId),
            "fallen" => matching.OrderByDescending(n => n.Defeated)
                .ThenByDescending(n => n.PowerIndex).ThenBy(n => n.FigureId),
            "name" => direction == "desc"
                ? matching.OrderByDescending(n => n.Name).ThenBy(n => n.FigureId)
                : matching.OrderBy(n => n.Name).ThenBy(n => n.FigureId),
            _ => direction == "asc"
                ? matching.OrderBy(n => n.PowerIndex).ThenBy(n => n.FigureId)
                : matching.OrderByDescending(n => n.PowerIndex)
                    .ThenByDescending(n => n.PeakSettlements).ThenBy(n => n.FigureId)
        };
        // The entire chronicle is fetched on demand when a figure is selected.
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => n with { Timeline = [], Apprentices = [] }).ToList();
        return Ok(new DarkForcesPageDto(
            items, all.Count, matching.Count, all.Count(n => n.Defeated),
            all.Sum(n => n.Invasions), all.Count == 0 ? 0 : all.Max(n => n.PeakSettlements)));
    }

    [HttpGet("{figureId:int}")]
    public ActionResult<DarkForceDto> GetFigure(int figureId)
    {
        DarkForceDto? figure = All.FirstOrDefault(n => n.FigureId == figureId);
        return figure == null ? NotFound() : Ok(figure);
    }
}

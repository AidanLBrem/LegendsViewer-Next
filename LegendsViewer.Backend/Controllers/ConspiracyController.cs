using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Various;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Controllers;

[ApiController]
[Route("api/conspiracies")]
public class ConspiracyController(IWorld world) : ControllerBase
{
    [HttpGet]
    public ActionResult<ConspiracyPageDto> Get(int? civilizationId = null, string? search = null,
        string? status = null, int page = 1, int pageSize = 25,
        string? sort = null, string? direction = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return BadRequest("Use a positive page and a page size between 1 and 100.");
        if (status != null && status is not ("on-hold" or "not-on-hold")) return BadRequest("Unknown status filter.");
        if (sort is not (null or "actor-count")) return BadRequest("Unknown sort field.");
        if (direction is not (null or "asc" or "desc")) return BadRequest("Unknown sort direction.");
        var all = ConspiracyAnalysis.Calculate(world);
        var filtered = all.Where(p => (!civilizationId.HasValue || p.Civilizations.Any(c => c.Id == civilizationId)) &&
            (status == null || (status == "on-hold") == (p.Status == "On hold")) &&
            (string.IsNullOrWhiteSpace(search) ||
             new[] { p.Key, p.Objective, p.Owner.Name, p.Entity?.Name, p.Artifact?.Name }
                 .Concat(p.Participants.SelectMany(a => new[] { a.Figure?.Name, a.Role, a.Strategy }))
                 .Any(s => s?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true))).ToList();
        // Sort BEFORE pagination so the largest plots can appear on the first page.
        // Resolve equal counts by owner/local plot IDs for stable page boundaries.
        IEnumerable<ConspiracyDto> sorted = sort == "actor-count"
            ? direction == "asc"
                ? filtered.OrderBy(p => p.ActorCount).ThenBy(p => p.Owner.Id).ThenBy(p => p.LocalId)
                : filtered.OrderByDescending(p => p.ActorCount).ThenBy(p => p.Owner.Id).ThenBy(p => p.LocalId)
            : filtered;
        return Ok(new ConspiracyPageDto(sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            all.Count, filtered.Count, all.SelectMany(p => p.Civilizations).DistinctBy(c => c.Id).OrderBy(c => c.Name).ToList()));
    }

    [HttpGet("{figureId:int}/{localId:int}")]
    public ActionResult<ConspiracyDto> GetPlot(int figureId, int localId)
    {
        var plot = ConspiracyAnalysis.Calculate(world).FirstOrDefault(p => p.Owner.Id == figureId && p.LocalId == localId);
        return plot == null ? NotFound() : Ok(plot);
    }
}

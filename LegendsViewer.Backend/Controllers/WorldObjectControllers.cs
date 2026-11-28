using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Legends.WorldObjects;
using Microsoft.AspNetCore.Mvc;
using LegendsViewer.Backend.Legends.EventCollections;
using LegendsViewer.Backend.DataAccess.Repositories.Interfaces;

namespace LegendsViewer.Backend.Controllers;


public class DanceFormController(IWorldObjectRepository<DanceForm> repository) : WorldObjectGenericController<DanceForm>(repository)
{
}

public class MusicalFormController(IWorldObjectRepository<MusicalForm> repository) : WorldObjectGenericController<MusicalForm>(repository)
{
}

public class PoeticFormController(IWorldObjectRepository<PoeticForm> repository) : WorldObjectGenericController<PoeticForm>(repository)
{
}

public class WrittenContentController(IWorldObjectRepository<WrittenContent> repository) : WorldObjectGenericController<WrittenContent>(repository)
{
}

public class LandmassController(IWorldObjectRepository<Landmass> repository) : WorldObjectGenericController<Landmass>(repository)
{
}

public class RiverController(IWorldObjectRepository<River> repository) : WorldObjectGenericController<River>(repository)
{
}

public class SiteController(IWorldObjectRepository<Site> repository) : WorldObjectGenericController<Site>(repository)
{
}

public class RegionController(IWorldObjectRepository<WorldRegion> repository) : WorldObjectGenericController<WorldRegion>(repository)
{
}

public class UndergroundRegionController(IWorldObjectRepository<UndergroundRegion> repository) : WorldObjectGenericController<UndergroundRegion>(repository)
{
}

public class ArtifactController(IWorldObjectRepository<Artifact> repository) : WorldObjectGenericController<Artifact>(repository)
{
}

public class EntityController(IWorldObjectRepository<Entity> repository) : WorldObjectGenericController<Entity>(repository)
{
    [HttpGet("civs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<Entity>> GetMainCivilizations()
    {
        return Ok(Repository.GetAllElements().Where(x => x.IsCiv || (x.EntityType == Legends.Enums.EntityType.Civilization && x.SiteHistory.Count > 0)));
    }

    [HttpGet("ethics")]
    public IActionResult GetCivilizationEthics()
    {
        return Ok(Repository.GetAllElements()
            .Where(e => e.IsCiv || e.EntityType == Legends.Enums.EntityType.Civilization)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, Race = e.Race.Id, Responses = e.ExportedEthics }));
    }

    [HttpGet("cultures")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<NationCultureSummaryDto>> GetCivilizationCultures()
    {
        var profiles = Repository.GetAllElements()
            .Where(entity => entity.IsCiv || entity.EntityType == Legends.Enums.EntityType.Civilization)
            .Select(entity =>
            {
                var profile = NationCultureProfileDto.FromEntity(entity, includeCustoms: false);
                return new NationCultureSummaryDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Race = entity.Race?.NamePlural ?? "",
                    CurrentSites = entity.CurrentSites.Count,
                    PopulationCount = entity.PopulationCount,
                    Character = profile.Character,
                    Periods = profile.Periods
                };
            })
            .OrderBy(entity => entity.Name)
            .ToList();

        return Ok(profiles);
    }

    [HttpGet("{id}/culture")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<NationCultureProfileDto> GetCivilizationCulture([FromRoute] int id)
    {
        Entity? entity = Repository.GetById(id);
        if (entity == null)
        {
            return NotFound();
        }
        return Ok(NationCultureProfileDto.FromEntity(entity));
    }

    [HttpGet("{id}/diplomacy/{otherId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<NationDiplomacyFactorsDto> GetDiplomaticFactors(
        [FromRoute] int id, [FromRoute] int otherId)
    {
        if (id == otherId)
        {
            return BadRequest("Select two different civilizations.");
        }

        Entity? source = Repository.GetById(id);
        Entity? target = Repository.GetById(otherId);
        if (source == null || target == null)
        {
            return NotFound();
        }

        return Ok(NationDiplomacyFactorsDto.FromEntities(source, target));
    }

    [HttpGet("{id}/monarchs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<List<LeaderTimelineDto>> GetMonarchTimelines([FromRoute] int id)
    {
        Entity? entity = Repository.GetById(id);
        if (entity == null)
        {
            return NotFound();
        }
        return Ok(entity.GetLeaderTimelines());
    }
}

public class HistoricalFigureController(IWorldObjectRepository<HistoricalFigure> repository) : WorldObjectGenericController<HistoricalFigure>(repository)
{
}

public class MountainPeakController(IWorldObjectRepository<MountainPeak> repository) : WorldObjectGenericController<MountainPeak>(repository)
{
}

public class StructureController(IWorldObjectRepository<Structure> repository) : WorldObjectGenericController<Structure>(repository)
{
}

public class ConstructionController(IWorldObjectRepository<WorldConstruction> repository) : WorldObjectGenericController<WorldConstruction>(repository)
{
}

public class EraController(IWorldObjectRepository<Era> repository) : WorldObjectGenericController<Era>(repository)
{
}

// Warfare

public class WarController(IWorldObjectRepository<War> repository) : WorldObjectGenericController<War>(repository)
{
}

public class BattleController(IWorldObjectRepository<Battle> repository) : WorldObjectGenericController<Battle>(repository)
{
}

public class DuelController(IWorldObjectRepository<Duel> repository) : WorldObjectGenericController<Duel>(repository)
{
}

public class RaidController(IWorldObjectRepository<Raid> repository) : WorldObjectGenericController<Raid>(repository)
{
}

public class SiteConqueredController(IWorldObjectRepository<SiteConquered> repository) : WorldObjectGenericController<SiteConquered>(repository)
{
}

// Politcal Conflicts 

public class InsurrectionController(IWorldObjectRepository<Insurrection> repository) : WorldObjectGenericController<Insurrection>(repository)
{
}

public class PersecutionController(IWorldObjectRepository<Persecution> repository) : WorldObjectGenericController<Persecution>(repository)
{
}

public class PurgeController(IWorldObjectRepository<Purge> repository) : WorldObjectGenericController<Purge>(repository)
{
}

public class CoupController(IWorldObjectRepository<EntityOverthrownCollection> repository) : WorldObjectGenericController<EntityOverthrownCollection>(repository)
{
}

public class BeastAttackController(IWorldObjectRepository<BeastAttack> repository) : WorldObjectGenericController<BeastAttack>(repository)
{
}

public class AbductionController(IWorldObjectRepository<Abduction> repository) : WorldObjectGenericController<Abduction>(repository)
{
}

public class TheftController(IWorldObjectRepository<Theft> repository) : WorldObjectGenericController<Theft>(repository)
{
}

// Rituals 

public class ProcessionController(IWorldObjectRepository<ProcessionCollection> repository) : WorldObjectGenericController<ProcessionCollection>(repository)
{
}

public class PerformanceController(IWorldObjectRepository<PerformanceCollection> repository) : WorldObjectGenericController<PerformanceCollection>(repository)
{
}

public class JourneyController(IWorldObjectRepository<Journey> repository) : WorldObjectGenericController<Journey>(repository)
{
}

public class CompetitionController(IWorldObjectRepository<CompetitionCollection> repository) : WorldObjectGenericController<CompetitionCollection>(repository)
{
}

public class CeremonyController(IWorldObjectRepository<CeremonyCollection> repository) : WorldObjectGenericController<CeremonyCollection>(repository)
{
}

public class OccasionController(IWorldObjectRepository<Occasion> repository) : WorldObjectGenericController<Occasion>(repository)
{
}

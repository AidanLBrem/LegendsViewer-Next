// A civilization can have population data from two different exports:
// * Sites and Populations: current residents of its sites, aggregated by race
// * legends_plus entity_population: a separate entity-level record
// The same race can occur in both reports. Only combine counts for
// different races; use the larger count when their race records overlap.
export interface PopulationRecord {
  race?: { id?: string | null; namePlural?: string | null } | null;
  count?: number | null;
  isMainRace?: boolean;
  isOutcasts?: boolean;
  isPrisoners?: boolean;
  isSlaves?: boolean;
  isVisitors?: boolean;
  isAnimalPeople?: boolean;
}

export interface EntityPopulationRecord {
  race?: { id?: string | null; namePlural?: string | null } | null;
  count?: number | null;
}

export interface PopulationRaceRow {
  key: string;
  race: string;
  count: number;
  percentage: number;
  classifications: string[];
}

export interface CombinedPopulationRaceRow extends PopulationRaceRow {
  siteCount: number | null;
  legendsPlusCount: number | null;
}

export interface NationPopulationSummary {
  sitePopulationTotal: number;
  races: PopulationRaceRow[];
  combinedPopulationTotal: number;
  combinedRaces: CombinedPopulationRaceRow[];
  overlappingRaceCount: number;
  entityPopulationCount: number | null;
  entityPopulationRace: string | null;
}

const countOrZero = (value: number | null | undefined): number =>
  typeof value === 'number' && Number.isFinite(value) && value > 0
    ? Math.floor(value)
    : 0;

export function summarizeNationPopulation(
  populations: readonly PopulationRecord[] | null | undefined,
  entityPopulation?: EntityPopulationRecord | null
): NationPopulationSummary {
  const byRace = new Map<string, Omit<PopulationRaceRow, 'percentage'>>();
  for (const population of populations ?? []) {
    const count = countOrZero(population.count);
    if (!count) continue;

    const race = population.race?.namePlural?.trim() || 'Unknown';
    const key = (population.race?.id?.trim() || race).toLowerCase();
    const tags = [
      population.isMainRace ? 'Civilized race' : null,
      population.isAnimalPeople ? 'Animal people' : null,
      population.isSlaves ? 'Slaves' : null,
      population.isPrisoners ? 'Prisoners' : null,
      population.isOutcasts ? 'Outcasts' : null,
      population.isVisitors ? 'Visitors' : null,
    ].filter((tag): tag is string => tag != null);

    const existing = byRace.get(key);
    if (existing) {
      existing.count += count;
      existing.classifications = [...new Set([...existing.classifications, ...tags])];
    } else {
      byRace.set(key, { key, race, count, classifications: tags });
    }
  }

  const sitePopulationTotal = [...byRace.values()].reduce((sum, row) => sum + row.count, 0);
  const races: PopulationRaceRow[] = [...byRace.values()]
    .sort((a, b) => b.count - a.count || a.race.localeCompare(b.race))
    .map(row => ({
      ...row,
      percentage: sitePopulationTotal > 0 ? row.count / sitePopulationTotal * 100 : 0,
    }));

  const entityCount = countOrZero(entityPopulation?.count);
  const entityRace = entityCount
    ? entityPopulation?.race?.namePlural?.trim() || 'Unknown'
    : null;
  const combinedRaces: CombinedPopulationRaceRow[] = races.map(row => ({
    ...row,
    siteCount: row.count,
    legendsPlusCount: null,
  }));

  if (entityCount && entityRace) {
    const entityKey = entityPopulation?.race?.id?.trim().toLowerCase();
    // In older exports, the race identifier may be absent. Fall back to
    // matching the formatted race name before assuming distinct populations.
    const match = combinedRaces.find(row =>
      (entityKey && row.key === entityKey) ||
      row.race.toLowerCase() === entityRace.toLowerCase());

    if (match) {
      match.legendsPlusCount = entityCount;
      match.count = Math.max(match.siteCount ?? 0, entityCount);
    } else {
      combinedRaces.push({
        key: `entity:${entityKey || entityRace.toLowerCase()}`,
        race: entityRace,
        count: entityCount,
        percentage: 0,
        classifications: [],
        siteCount: null,
        legendsPlusCount: entityCount,
      });
    }
  }

  combinedRaces.sort((a, b) => b.count - a.count || a.race.localeCompare(b.race));
  const combinedPopulationTotal = combinedRaces.reduce((sum, row) => sum + row.count, 0);
  for (const row of combinedRaces) {
    row.percentage = combinedPopulationTotal > 0
      ? row.count / combinedPopulationTotal * 100 : 0;
  }
  return {
    sitePopulationTotal,
    races,
    combinedPopulationTotal,
    combinedRaces,
    overlappingRaceCount: combinedRaces.filter(row =>
      row.siteCount !== null && row.legendsPlusCount !== null).length,
    entityPopulationCount: entityCount || null,
    entityPopulationRace: entityRace,
  };
}

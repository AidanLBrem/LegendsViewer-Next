// A civilization can have population data from two different exports:
// * Sites and Populations: current residents of its sites, aggregated by race
// * legends_plus entity_population: a separate entity-level record
// These sources must never be added together or treated as a historical series.
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

export interface NationPopulationSummary {
  sitePopulationTotal: number;
  races: PopulationRaceRow[];
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
  return {
    sitePopulationTotal,
    races,
    entityPopulationCount: entityCount || null,
    entityPopulationRace: entityCount
      ? entityPopulation?.race?.namePlural?.trim() || 'Unknown'
      : null,
  };
}

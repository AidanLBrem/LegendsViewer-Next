import {
  getOwnershipChangeYears,
  getSiteOwnerAtYear,
  type SiteWithOwnershipHistory,
} from './siteHistory';

export interface NationOption {
  id: number;
  name: string;
  color: string;
  currentSites: number;
  currentPopulation: number | null;
}

export type NationSortKey = 'sites-desc' | 'population-desc' | 'population-asc' | 'name-asc';

export function sortNationOptions(options: readonly NationOption[], sortKey: NationSortKey): NationOption[] {
  return [...options].sort((a, b) => {
    if (sortKey === 'name-asc') return a.name.localeCompare(b.name) || a.id - b.id;
    if (sortKey === 'population-desc' || sortKey === 'population-asc') {
      if (a.currentPopulation == null) return b.currentPopulation == null ? a.name.localeCompare(b.name) : 1;
      if (b.currentPopulation == null) return -1;
      const delta = a.currentPopulation - b.currentPopulation;
      return (sortKey === 'population-desc' ? -delta : delta) || a.name.localeCompare(b.name) || a.id - b.id;
    }
    return b.currentSites - a.currentSites || a.name.localeCompare(b.name) || a.id - b.id;
  });
}

export interface NationExpansionPoint {
  year: number;
  sites: number;
  gained: number;
  lost: number;
  totalGained: number;
  totalLost: number;
}

export interface NationExpansionHistory {
  points: NationExpansionPoint[];
  peakSites: number;
  peakYear: number;
}

function hasOwnerAtYear(site: SiteWithOwnershipHistory, nationId: number, year: number, presentYear: number): boolean {
  // An undated contemporary owner is not evidence of ancient ownership.
  if (year < presentYear && !site.ownerHistory?.length) return false;
  const owner = getSiteOwnerAtYear(site, year, presentYear);
  return owner.visible && owner.ownerId === nationId;
}

export function listHistoricalNations(
  sites: readonly SiteWithOwnershipHistory[], presentYear: number
): NationOption[] {
  const options = new Map<number, NationOption>();
  const add = (id: number | null | undefined, name: string | null | undefined, color: string | null | undefined) => {
    if (id == null || !Number.isFinite(id)) return;
    if (!options.has(id)) options.set(id, { id, name: name || `Faction #${id}`, color: color || '#777777', currentSites: 0, currentPopulation: null });
  };
  for (const site of sites) {
    add(site.ownerId, site.ownerText ?? site.owner, site.color);
    for (const period of site.ownerHistory ?? []) {
      add(period.ownerId, period.ownerText ?? period.owner, period.color);
    }
    const current = getSiteOwnerAtYear(site, presentYear, presentYear);
    if (current.visible && current.ownerId != null) {
      add(current.ownerId, current.ownerText, current.color);
      const option = options.get(current.ownerId)!;
      option.name = current.ownerText || option.name;
      option.color = current.color || option.color;
      option.currentSites++;
      if (typeof site.populationCount === 'number' && Number.isFinite(site.populationCount) && site.populationCount >= 0) {
        option.currentPopulation = (option.currentPopulation ?? 0) + Math.round(site.populationCount);
      }
    }
  }
  return [...options.values()].sort((a, b) =>
    b.currentSites - a.currentSites || a.name.localeCompare(b.name));
}

export function buildNationExpansionHistory(
  sites: readonly SiteWithOwnershipHistory[],
  nationId: number,
  presentYear: number
): NationExpansionHistory {
  const candidates = sites.filter(site =>
    site.ownerId === nationId || (site.ownerHistory ?? []).some(period => period.ownerId === nationId));
  const years = getOwnershipChangeYears(candidates, presentYear);
  const points: NationExpansionPoint[] = [];
  const previous = new Uint8Array(candidates.length);
  let totalGained = 0;
  let totalLost = 0;
  let peakSites = 0;
  let peakYear = 0;

  for (const year of years) {
    let sitesOwned = 0;
    let gained = 0;
    let lost = 0;
    candidates.forEach((site, index) => {
      const owned = hasOwnerAtYear(site, nationId, year, presentYear);
      if (owned) sitesOwned++;
      if (points.length > 0) {
        if (owned && !previous[index]) gained++;
        if (!owned && previous[index]) lost++;
      }
      previous[index] = owned ? 1 : 0;
    });
    totalGained += gained;
    totalLost += lost;
    if (sitesOwned > peakSites) {
      peakSites = sitesOwned;
      peakYear = year;
    }
    if (!points.length || gained || lost || year === presentYear) {
      points.push({ year, sites: sitesOwned, gained, lost, totalGained, totalLost });
    }
  }

  return { points, peakSites, peakYear };
}

export function nationExpansionAtYear(
  history: NationExpansionHistory, year: number
): NationExpansionPoint {
  const points = history.points;
  if (!points.length) return { year: 0, sites: 0, gained: 0, lost: 0, totalGained: 0, totalLost: 0 };
  let low = 0;
  let high = points.length;
  while (low < high) {
    const mid = (low + high) >>> 1;
    if (points[mid].year <= year) low = mid + 1;
    else high = mid;
  }
  return points[Math.max(0, low - 1)];
}

// Step chart: changes occur at the actual recorded year; nothing is interpolated.
export function nationExpansionChartPath(
  history: NationExpansionHistory, maxYear: number
): string {
  if (!history.points.length) return '';
  const x = (year: number) => 10 + 300 * year / Math.max(1, maxYear);
  const y = (sites: number) => 100 - 86 * sites / Math.max(1, history.peakSites);
  const first = history.points[0];
  let path = `M${x(first.year).toFixed(2)},${y(first.sites).toFixed(2)}`;
  for (let i = 1; i < history.points.length; i++) {
    const point = history.points[i];
    path += ` H${x(point.year).toFixed(2)} V${y(point.sites).toFixed(2)}`;
  }
  return path + ` H${x(maxYear).toFixed(2)}`;
}

// Historical ownership snapshot helpers. These are pure so the map can step
// years without re-fetching world data or recreating its Leaflet layers.
export interface SiteOwnershipPeriod {
  startYear?: number;
  endYear?: number;
  owner?: string | null;
  ownerText?: string | null;
  ownerId?: number | null;
  currentOwnerId?: number | null;
  actualOwner?: string | null;
  color?: string | null;
  startCause?: string | null;
}

export interface SiteWithOwnershipHistory {
  ownerHistory?: SiteOwnershipPeriod[] | null;
  owner?: string | null;
  ownerText?: string | null;
  ownerId?: number | null;
  currentOwnerId?: number | null;
  color?: string | null;
}

export interface HistoricalSiteOwner {
  visible: boolean;
  owner: string;
  ownerText: string;
  ownerId: number | null;
  currentOwnerId: number | null;
  actualOwner: string | null;
  color: string;
  startYear: number | null;
  startCause: string | null;
}

export function getSiteOwnerAtYear(
  site: SiteWithOwnershipHistory,
  year: number,
  presentYear: number
): HistoricalSiteOwner {
  const current: HistoricalSiteOwner = {
    visible: true,
    owner: site.owner ?? 'Others',
    ownerText: site.ownerText ?? 'Others',
    ownerId: site.ownerId ?? null,
    currentOwnerId: site.currentOwnerId ?? null,
    actualOwner: null,
    color: site.color ?? '#666',
    startYear: null,
    startCause: null,
  };

  // Honor the import's current-owner resolution in the latest year. Sites
  // without usable ownership history cannot be reconstructed retrospectively.
  const periods = site.ownerHistory ?? [];
  if (year >= presentYear || periods.length === 0) return current;

  let active: SiteOwnershipPeriod | null = null;
  let activeStart = -1;
  for (const period of periods) {
    const start = period.startYear == null || period.startYear < 0 ? 0 : period.startYear;
    const end = period.endYear ?? -1;
    // A site's ended period stops at the start of the next ownership year.
    if (year < start || (end >= 0 && year >= end)) continue;
    if (active === null || start >= activeStart) {
      active = period;
      activeStart = start;
    }
  }

  if (!active) {
    return {
      visible: false,
      owner: 'Unclaimed',
      ownerText: 'Unclaimed',
      ownerId: null,
      currentOwnerId: null,
      actualOwner: null,
      color: '#666',
      startYear: null,
      startCause: null,
    };
  }

  return {
    visible: true,
    owner: active.owner ?? 'Unclaimed',
    ownerText: active.ownerText ?? 'Unclaimed',
    ownerId: active.ownerId ?? null,
    currentOwnerId: active.currentOwnerId ?? null,
    actualOwner: active.actualOwner ?? null,
    color: active.color ?? '#666',
    startYear: active.startYear ?? null,
    startCause: active.startCause ?? null,
  };
}

export function getOwnershipChangeYears(
  sites: readonly SiteWithOwnershipHistory[],
  presentYear: number
): number[] {
  const endYear = Math.max(0, Math.floor(presentYear));
  const years = new Set<number>([0, endYear]);
  for (const site of sites) {
    for (const period of site.ownerHistory ?? []) {
      for (const year of [period.startYear, period.endYear]) {
        if (year != null && Number.isInteger(year) && year >= 0 && year <= endYear) {
          years.add(year);
        }
      }
    }
  }
  return Array.from(years).sort((a, b) => a - b);
}

export function stepOwnershipChange(
  year: number,
  direction: -1 | 1,
  changeYears: readonly number[]
): number {
  if (direction === 1) {
    const next = changeYears.find(candidate => candidate > year);
    return next ?? year;
  }
  for (let i = changeYears.length - 1; i >= 0; i--) {
    if (changeYears[i] < year) return changeYears[i];
  }
  return year;
}

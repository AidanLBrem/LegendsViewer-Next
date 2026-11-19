// Historical sites have two separate timelines: existence and ownership.
// Losing an owner never implies that the physical site has disappeared.
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
  endCause?: string | null;
}

export interface SiteWithOwnershipHistory {
  ownerHistory?: SiteOwnershipPeriod[] | null;
  createdYear?: number | null;
  destroyedYears?: number[] | null;
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

function validYear(value: number | null | undefined): value is number {
  return value != null && Number.isInteger(value) && value >= 0;
}

function isCreation(cause?: string | null): boolean {
  return /found|construct/i.test(cause ?? '');
}

function isDestruction(cause?: string | null): boolean {
  return /destroy/i.test(cause ?? '');
}

function isNewSiteAfterDestruction(period: SiteOwnershipPeriod, destroyedYear: number): boolean {
  if (!validYear(period.startYear)) return false;
  return period.startYear > destroyedYear ||
    (period.startYear === destroyedYear &&
      /reclaim|unretir|found|construct/i.test(period.startCause ?? ''));
}

function getSiteCreationYear(site: SiteWithOwnershipHistory): number | null {
  // Prefer the explicit construction date from the backend. If an older
  // backend omitted it, only actual founding/constructing ownership events
  // establish a first-appearance date. A conquest does not.
  if (validYear(site.createdYear)) return site.createdYear;
  const starts = (site.ownerHistory ?? [])
    .filter(period => validYear(period.startYear) && isCreation(period.startCause))
    .map(period => period.startYear!);
  return starts.length ? Math.min(...starts) : null;
}

function getSiteDestructionYears(site: SiteWithOwnershipHistory): number[] {
  const years = new Set<number>();
  for (const year of site.destroyedYears ?? []) {
    if (validYear(year)) years.add(year);
  }
  // Older backends may not export destroyedYears; endCause is enough to
  // identify a destroyed site but never treat abandonment as destruction.
  for (const period of site.ownerHistory ?? []) {
    if (validYear(period.endYear) && isDestruction(period.endCause)) {
      years.add(period.endYear);
    }
  }
  return [...years].sort((a, b) => a - b);
}

export function isSitePresentAtYear(site: SiteWithOwnershipHistory, year: number): boolean {
  const createdYear = getSiteCreationYear(site);
  if (createdYear !== null && year < createdYear) return false;

  let lastDestruction = -1;
  for (const destroyed of getSiteDestructionYears(site)) {
    if (destroyed > year) break;
    lastDestruction = destroyed;
  }
  if (lastDestruction < 0) return true;

  // A later reclamation or construction restores the site. Owner changes
  // before destruction cannot revive it.
  return (site.ownerHistory ?? []).some(period =>
    validYear(period.startYear) &&
    period.startYear <= year &&
    isNewSiteAfterDestruction(period, lastDestruction)
  );
}

export function getSiteOwnerAtYear(
  site: SiteWithOwnershipHistory,
  year: number,
  _presentYear: number
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

  if (!isSitePresentAtYear(site, year)) {
    return { ...current, visible: false };
  }

  const periods = site.ownerHistory ?? [];
  if (periods.length === 0) {
    // Without historical records we cannot safely infer past ownership.
    return current;
  }

  let active: SiteOwnershipPeriod | null = null;
  let activeStart = -1;
  for (const period of periods) {
    const start = validYear(period.startYear) ? period.startYear : 0;
    if (year < start || (validYear(period.endYear) && year >= period.endYear)) continue;
    if (active === null || start >= activeStart) {
      active = period;
      activeStart = start;
    }
  }

  // An abandoned, withered, or otherwise unoccupied site still exists.
  // Its owner group changes to Unclaimed; the marker must stay selectable.
  if (!active) {
    return {
      visible: true,
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

export function getTimelineMaxYear(
  currentYear: number | null | undefined,
  sites: readonly SiteWithOwnershipHistory[]
): number {
  let latest = validYear(currentYear) ? currentYear : 0;
  for (const site of sites) {
    if (validYear(site.createdYear)) latest = Math.max(latest, site.createdYear);
    for (const destroyed of getSiteDestructionYears(site)) {
      latest = Math.max(latest, destroyed);
    }
    for (const period of site.ownerHistory ?? []) {
      if (validYear(period.startYear)) latest = Math.max(latest, period.startYear);
      if (validYear(period.endYear)) latest = Math.max(latest, period.endYear);
    }
  }
  return latest;
}

export function getOwnershipChangeYears(
  sites: readonly SiteWithOwnershipHistory[],
  presentYear: number
): number[] {
  const endYear = Math.max(0, Math.floor(presentYear));
  const years = new Set<number>([0, endYear]);
  for (const site of sites) {
    const dates = [
      site.createdYear,
      ...getSiteDestructionYears(site),
      ...(site.ownerHistory ?? []).flatMap(period => [period.startYear, period.endYear]),
    ];
    for (const year of dates) {
      if (validYear(year) && year <= endYear) years.add(year);
    }
  }
  return [...years].sort((a, b) => a - b);
}

export function stepOwnershipChange(
  year: number,
  direction: -1 | 1,
  changeYears: readonly number[]
): number {
  let low = 0;
  let high = changeYears.length;
  if (direction === 1) {
    while (low < high) {
      const mid = Math.floor((low + high) / 2);
      if (changeYears[mid] <= year) low = mid + 1;
      else high = mid;
    }
    return changeYears[low] ?? year;
  }

  while (low < high) {
    const mid = Math.floor((low + high) / 2);
    if (changeYears[mid] < year) low = mid + 1;
    else high = mid;
  }
  return low > 0 ? changeYears[low - 1] : year;
}

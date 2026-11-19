import {
  getSiteOwnerAtYear,
  type SiteWithOwnershipHistory,
} from './siteHistory';

export interface AtlasCoordinate { x?: number; y?: number }
export interface AtlasSite extends SiteWithOwnershipHistory {
  coordinates?: AtlasCoordinate[] | null;
  type?: string | null;
  // From the optional sites-and-pops export; a current snapshot rather
  // than a reconstructed population for each historical year.
  populationCount?: number | null;
}
export interface TerritorySeed {
  x: number;
  y: number;
  nationId: number;
  color: string;
  population?: number;
  coordinateCount?: number;
}

export interface TerritorySeedOptions {
  filterMinorPowers?: boolean;
  excludedNationIds?: ReadonlySet<number>;
}

// Necromancer factions are identified by their association with a tower,
// rather than by their name. Do not classify civilizations that later
// captured a tower as necromancers.
export function getNecromancerNationIds(sites: readonly AtlasSite[]): Set<number> {
  const necromancers = new Set<number>();
  const include = (id: number | null | undefined, type: string | null | undefined) => {
    if (id != null && type != null && type !== 'Civilization') {
      necromancers.add(id);
    }
  };

  for (const site of sites) {
    if (site.type !== 'Tower') continue;
    include(site.ownerId, site.ownerEntityType);
    for (const period of site.ownerHistory ?? []) {
      include(period.ownerId, period.ownerEntityType);
    }
  }
  return necromancers;
}

export function getTerritorySeeds(
  sites: readonly AtlasSite[],
  year: number,
  presentYear: number,
  includeSite: (site: AtlasSite) => boolean = () => true,
  options: TerritorySeedOptions = {}
): TerritorySeed[] {
  const seeds: TerritorySeed[] = [];
  const ownedSites: Array<{
    site: AtlasSite;
    owner: ReturnType<typeof getSiteOwnerAtYear>;
  }> = [];
  const siteCounts = new Map<number, number>();

  // Count actual sites for every owner at the selected historical year,
  // independently of which site types or owner layers are currently shown.
  // Counting seeds instead would mistake multiple coordinates for sites.
  for (const site of sites) {
    // Do not project today's owner into a year without ownership records.
    if (year < presentYear && !(site.ownerHistory?.length)) continue;
    const owner = getSiteOwnerAtYear(site, year, presentYear);
    if (!owner.visible || owner.ownerId == null || owner.ownerText === 'Unclaimed') continue;
    ownedSites.push({ site, owner });
    siteCounts.set(owner.ownerId, (siteCounts.get(owner.ownerId) ?? 0) + 1);
  }

  for (const { site, owner } of ownedSites) {
    // A lone nomadic camp is still a selectable, owned site; it just
    // cannot project a political border onto neighboring map tiles.
    if (owner.ownerEntityType === 'NomadicGroup' && siteCounts.get(owner.ownerId!) === 1) continue;
    if (options.filterMinorPowers && (siteCounts.get(owner.ownerId!) ?? 0) <= 1) continue;
    if (options.excludedNationIds?.has(owner.ownerId!)) continue;
    if (!includeSite(site)) continue;
    const locations = (site.coordinates ?? []).filter(location =>
      location.x != null && location.y != null &&
      Number.isFinite(location.x) && Number.isFinite(location.y));
    const population = site.populationCount;
    for (const location of locations) {
      seeds.push({
        x: location.x!, y: location.y!, nationId: owner.ownerId!, color: owner.color,
        ...(population != null && Number.isFinite(population) && population > 0
          ? { population } : {}),
        ...(locations.length > 1 ? { coordinateCount: locations.length } : {}),
      });
    }
  }
  return seeds;
}

export interface TerritoryGrid {
  width: number;
  height: number;
  nation: Int32Array;
  colors: Map<number, string>;
}

// One bit per world tile, in row-major order. Oceans are transparent in
// territory overlays even when a settlement's influence reaches the coast.
export function isOceanTile(
  oceanMask: Uint8Array | null | undefined,
  worldWidth: number,
  x: number,
  y: number
): boolean {
  if (!oceanMask) return false;
  const index = y * worldWidth + x;
  return (oceanMask[index >> 3] & (1 << (index & 7))) !== 0;
}

// Resolve a click to the same bounded-resolution political estimate used by
// the rendered overlay, with ocean tiles explicitly unclaimed.
export function getTerritoryNationAtTile(
  grid: TerritoryGrid,
  worldWidth: number,
  worldHeight: number,
  x: number,
  y: number,
  oceanMask?: Uint8Array | null
): number | null {
  if (!Number.isInteger(x) || !Number.isInteger(y) ||
      x < 0 || y < 0 || x >= worldWidth || y >= worldHeight ||
      isOceanTile(oceanMask, worldWidth, x, y)) {
    return null;
  }
  const cellX = Math.min(grid.width - 1, Math.floor(x * grid.width / worldWidth));
  const cellY = Math.min(grid.height - 1, Math.floor(y * grid.height / worldHeight));
  const nationId = grid.nation[cellY * grid.width + cellX];
  return nationId >= 0 ? nationId : null;
}

export type YieldToBrowser = () => Promise<void>;

// Bounded, population-weighted approximation of territorial influence.
// Each settlement contributes a smooth, limited-radius influence field.
// Fields belonging to the same nation add together, so adjacent population
// centers reinforce each other instead of being treated as isolated points.
// The world export does not contain political borders or historical
// populations; these fields are estimates using the imported site snapshot.
export async function createTerritoryGrid(
  width: number,
  height: number,
  seeds: readonly TerritorySeed[],
  yieldToBrowser: YieldToBrowser = async () => {},
  cancelled: () => boolean = () => false,
  oceanMask?: Uint8Array
): Promise<TerritoryGrid | null> {
  const rasterWidth = Math.max(1, Math.min(320, Math.ceil(width)));
  const rasterHeight = Math.max(1, Math.min(320, Math.ceil(height)));
  const nation = new Int32Array(rasterWidth * rasterHeight);
  nation.fill(-1);
  const colors = new Map<number, string>();
  if (width <= 0 || height <= 0) return { width: rasterWidth, height: rasterHeight, nation, colors };

  const baseRadius = Math.max(7, Math.min(140, Math.hypot(width, height) * 0.045));
  const groups = new Map<number, TerritorySeed[]>();
  for (const seed of seeds) {
    if (!Number.isFinite(seed.x) || !Number.isFinite(seed.y) ||
        seed.x < 0 || seed.x >= width || seed.y < 0 || seed.y >= height) continue;
    if (isOceanTile(oceanMask, width, Math.floor(seed.x), Math.floor(seed.y))) continue;
    const group = groups.get(seed.nationId);
    if (group) group.push(seed);
    else groups.set(seed.nationId, [seed]);
    colors.set(seed.nationId, seed.color);
  }
  if (groups.size === 0) return { width: rasterWidth, height: rasterHeight, nation, colors };

  // Reuse a single accumulator for every nation. A generation-stamp array
  // and list of touched cells avoids both a map per pixel and a full-grid
  // scan for every faction (important for worlds with many minor powers).
  const bestStrength = new Float32Array(nation.length);
  const accumulated = new Float32Array(nation.length);
  const stamps = new Uint32Array(nation.length);
  const touched: number[] = [];
  let stamp = 0;
  let groupsProcessed = 0;
  const pixelWidth = width / rasterWidth;
  const pixelHeight = height / rasterHeight;

  for (const [nationId, nationSeeds] of groups) {
    if (cancelled()) return null;
    stamp++;
    touched.length = 0;

    for (let i = 0; i < nationSeeds.length; i++) {
      if (cancelled()) return null;
      const seed = nationSeeds[i];
      // Log scaling gives large cities more reach without allowing one
      // enormous settlement to overpower an entire surrounding region.
      // Unknown population has the same weight as a single ordinary site.
      const population = Number.isFinite(seed.population) ? Math.max(0, seed.population!) : 0;
      const populationScale = Math.log10(1 + population / 100);
      const weight = (1 + Math.min(2.4, populationScale * 0.55)) /
        Math.max(1, seed.coordinateCount ?? 1);
      const radius = baseRadius * (1 + Math.min(0.45, populationScale * 0.12));
      const radiusSquared = radius * radius;
      const minX = Math.max(0, Math.ceil((seed.x - radius) / pixelWidth - 0.5));
      const maxX = Math.min(rasterWidth - 1, Math.floor((seed.x + radius) / pixelWidth - 0.5));
      const minY = Math.max(0, Math.ceil((seed.y - radius) / pixelHeight - 0.5));
      const maxY = Math.min(rasterHeight - 1, Math.floor((seed.y + radius) / pixelHeight - 0.5));

      for (let py = minY; py <= maxY; py++) {
        const dy = (py + 0.5) * pixelHeight - seed.y;
        const dySquared = dy * dy;
        if (dySquared >= radiusSquared) continue;
        for (let px = minX; px <= maxX; px++) {
          const dx = (px + 0.5) * pixelWidth - seed.x;
          const distanceSquared = dx * dx + dySquared;
          if (distanceSquared >= radiusSquared) continue;
          const falloff = 1 - distanceSquared / radiusSquared;
          const contribution = weight * falloff * falloff;
          const cell = py * rasterWidth + px;
          if (stamps[cell] !== stamp) {
            stamps[cell] = stamp;
            accumulated[cell] = contribution;
            touched.push(cell);
          } else {
            accumulated[cell] += contribution;
          }
        }
      }
      if (i % 32 === 31) await yieldToBrowser();
    }

    for (const cell of touched) {
      const strength = accumulated[cell];
      if (strength > bestStrength[cell] + 1e-6 ||
          (Math.abs(strength - bestStrength[cell]) <= 1e-6 &&
           (nation[cell] < 0 || nationId < nation[cell]))) {
        bestStrength[cell] = strength;
        nation[cell] = nationId;
      }
    }
    if (++groupsProcessed % 16 === 0) await yieldToBrowser();
  }

  // When rival fields overlap a settlement's exact world tile, preserve
  // recorded ownership at that location instead of obscuring the site
  // beneath a stronger neighbor's estimated surrounding territory.
  for (const [nationId, nationSeeds] of groups) {
    for (const seed of nationSeeds) {
      const px = Math.min(rasterWidth - 1, Math.floor(seed.x / pixelWidth));
      const py = Math.min(rasterHeight - 1, Math.floor(seed.y / pixelHeight));
      nation[py * rasterWidth + px] = nationId;
    }
  }
  return { width: rasterWidth, height: rasterHeight, nation, colors };
}

function rgb(color: string): [number, number, number] {
  const hex = /^#([0-9a-f]{6})/i.exec(color);
  if (hex) {
    const value = parseInt(hex[1], 16);
    return [(value >>> 16) & 255, (value >>> 8) & 255, value & 255];
  }
  const css = /rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i.exec(color);
  if (css) return [Number(css[1]), Number(css[2]), Number(css[3])];
  return [112, 112, 112];
}

export function territoryGridToDataUrl(
  grid: TerritoryGrid,
  oceanMask?: Uint8Array,
  worldWidth = grid.width,
  worldHeight = grid.height,
  showBorderLines = true
): string {
  const canvas = document.createElement('canvas');
  // The influence calculation stays capped at 320² cells, but the rendered
  // bitmap uses world-tile resolution to clip the coastline exactly.
  canvas.width = oceanMask ? worldWidth : grid.width;
  canvas.height = oceanMask ? worldHeight : grid.height;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas 2D rendering unavailable');
  const pixels = ctx.createImageData(canvas.width, canvas.height);
  const cache = new Map<number, [number, number, number]>();
  const previousRow = new Int32Array(canvas.width);
  previousRow.fill(-1);

  for (let y = 0; y < canvas.height; y++) {
    const sourceY = Math.min(grid.height - 1, Math.floor(y * grid.height / canvas.height));
    let leftId = -1;
    for (let x = 0; x < canvas.width; x++) {
      const sourceX = Math.min(grid.width - 1, Math.floor(x * grid.width / canvas.width));
      const offset = sourceY * grid.width + sourceX;
      const id = isOceanTile(oceanMask, canvas.width, x, y) ? -1 : grid.nation[offset];
      if (id >= 0) {
        if (!cache.has(id)) cache.set(id, rgb(grid.colors.get(id) ?? '#777777'));
        const [r, g, b] = cache.get(id)!;
        const pos = (y * canvas.width + x) * 4;
        const border = showBorderLines &&
          ((x > 0 && leftId !== id) || (y > 0 && previousRow[x] !== id));
        pixels.data[pos] = border ? 22 : r;
        pixels.data[pos + 1] = border ? 26 : g;
        pixels.data[pos + 2] = border ? 28 : b;
        pixels.data[pos + 3] = border ? 190 : 142;
      }
      previousRow[x] = id;
      leftId = id;
    }
  }
  ctx.putImageData(pixels, 0, 0);
  return canvas.toDataURL('image/png');
}

export interface HistoricalWar {
  startYear?: number;
  endYear?: number;
}
export interface HistoricalBattle {
  startYear?: number;
}
export interface HistoricalSiteConquest {
  year?: number;
}
export interface HistoricalWarfare {
  wars?: HistoricalWar[] | null;
  battles?: HistoricalBattle[] | null;
  siteConquests?: HistoricalSiteConquest[] | null;
}

export function isWarInYear(war: HistoricalWar, year: number): boolean {
  return war.startYear != null && war.startYear >= 0 && war.startYear <= year &&
    (war.endYear == null || war.endYear < 0 || year <= war.endYear);
}
export function isBattleInYear(battle: HistoricalBattle, year: number): boolean {
  return battle.startYear != null && battle.startYear >= 0 && battle.startYear === year;
}
export function isSiteConquestInYear(event: HistoricalSiteConquest, year: number): boolean {
  return event.year != null && event.year >= 0 && event.year === year;
}

export function getWarfareChangeYears(data: HistoricalWarfare, maxYear: number): number[] {
  const result = new Set<number>();
  const include = (year: number | null | undefined) => {
    if (year != null && year >= 0 && year <= maxYear) result.add(year);
  };
  for (const war of data.wars ?? []) {
    include(war.startYear);
    include(war.endYear);
  }
  for (const battle of data.battles ?? []) include(battle.startYear);
  for (const event of data.siteConquests ?? []) include(event.year);
  return [...result].sort((a, b) => a - b);
}

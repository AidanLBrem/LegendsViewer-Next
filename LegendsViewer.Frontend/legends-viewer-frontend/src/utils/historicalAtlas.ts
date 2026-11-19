import TinyQueue from 'tinyqueue';
import {
  getSiteOwnerAtYear,
  type SiteWithOwnershipHistory,
} from './siteHistory';

export interface AtlasCoordinate { x?: number; y?: number }
export interface AtlasSite extends SiteWithOwnershipHistory {
  id?: number;
  name?: string | null;
  coordinates?: AtlasCoordinate[] | null;
  type?: string | null;
  // From the optional sites-and-pops export; a current snapshot rather
  // than a reconstructed population for each historical year.
  residentRace?: string | null;
  populationCount?: number | null;
}
export interface TerritorySeed {
  x: number;
  y: number;
  nationId: number;
  color: string;
  race?: string;
  population?: number;
  coordinateCount?: number;
  siteId?: number;
  siteName?: string;
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
    const race = (year >= presentYear ? site.residentRace : null) || owner.ownerRace;
    for (const location of locations) {
      seeds.push({
        x: location.x!, y: location.y!, nationId: owner.ownerId!, color: owner.color,
        ...(site.id != null ? { siteId: site.id } : {}),
        ...(site.name ? { siteName: site.name } : {}),
        ...(race ? { race } : {}),
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
  // Which geographic land component produced each winner. Checked at exact
  // world-tile resolution to keep islands separate on downsampled grids.
  evidence?: {
    seeds: readonly TerritorySeed[];
    strongestSeed: Int32Array;
    strength: Float64Array;
    runner: Int32Array;
    runnerStrength: Float64Array;
    anchors: Uint8Array;
  };
  sourceComponents?: Int32Array;
  landComponents?: Int32Array;
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

// Cardinal land adjacency; diagonally touching shores are not connected.
// A packed water mask keeps the calculation small even on very large worlds.
export function buildLandComponents(width: number, height: number, waterMask: Uint8Array): Int32Array {
  const count = width * height;
  const components = new Int32Array(count);
  const queue = new Int32Array(count);
  let nextId = 0;
  const enqueue = (tile: number, id: number, tail: number): number => {
    if (components[tile] || (waterMask[tile >> 3] & (1 << (tile & 7)))) return tail;
    components[tile] = id;
    queue[tail] = tile;
    return tail + 1;
  };

  for (let start = 0; start < count; start++) {
    if (components[start] || (waterMask[start >> 3] & (1 << (start & 7)))) continue;
    const id = ++nextId;
    let head = 0;
    let tail = enqueue(start, id, 0);
    while (head < tail) {
      const tile = queue[head++];
      const x = tile % width;
      if (x > 0) tail = enqueue(tile - 1, id, tail);
      if (x + 1 < width) tail = enqueue(tile + 1, id, tail);
      if (tile >= width) tail = enqueue(tile - width, id, tail);
      if (tile + width < count) tail = enqueue(tile + width, id, tail);
    }
  }
  return components;
}

// A claimed region must have a continuous path through its own territory
// to an actual site on the same land component. This removes unattached
// political islands caused by competing overlapping influence fields.
export function pruneUnanchoredClaims(grid: TerritoryGrid, siteAnchors: Uint8Array): void {
  const { width, height, nation, sourceComponents } = grid;
  const visited = new Uint8Array(nation.length);
  const queue = new Int32Array(nation.length);

  for (let start = 0; start < nation.length; start++) {
    if (visited[start] || nation[start] < 0) continue;
    const owner = nation[start];
    const component = sourceComponents?.[start] ?? 1;
    let head = 0;
    let tail = 1;
    let anchored = false;
    queue[0] = start;
    visited[start] = 1;

    while (head < tail) {
      const cell = queue[head++];
      if (siteAnchors[cell]) anchored = true;
      const x = cell % width;
      const neighbors = [
        x > 0 ? cell - 1 : -1,
        x + 1 < width ? cell + 1 : -1,
        cell >= width ? cell - width : -1,
        cell + width < width * height ? cell + width : -1,
      ];
      for (const next of neighbors) {
        if (next < 0 || visited[next] || nation[next] !== owner ||
            (sourceComponents && sourceComponents[next] !== component)) continue;
        visited[next] = 1;
        queue[tail++] = next;
      }
    }

    if (!anchored) {
      for (let i = 0; i < tail; i++) {
        const cell = queue[i];
        nation[cell] = -1;
        if (sourceComponents) sourceComponents[cell] = 0;
      }
    }
  }
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
  const cell = cellY * grid.width + cellX;
  const nationId = grid.nation[cell];
  if (nationId < 0) return null;
  // Tile-level check matters when multiple islands occupy a coarse cell.
  if (grid.landComponents && grid.sourceComponents &&
      grid.landComponents[y * worldWidth + x] !== grid.sourceComponents[cell]) {
    return null;
  }
  return nationId;
}

export type YieldToBrowser = () => Promise<void>;

// Bounded, population-weighted approximation of territorial influence.
// Each settlement contributes a smooth, limited-radius influence field.
// Fields belonging to the same nation add together, so adjacent population
// centers reinforce each other instead of being treated as isolated points.
// The world export does not contain political borders or historical
// populations; these fields are estimates using the imported site snapshot.
// Indices follow the backend RegionType values. Costs are world-distance multipliers.
const raceAliases: Record<string, string> = { dwarves: 'dwarf', elves: 'elf', humans: 'human', goblins: 'goblin', kobolds: 'kobold' };
const terrainCosts: Record<string, readonly number[]> = {
  //         other  mtn  forest desert sea wetland tundra ice grass hills lake
  dwarf:    [1, 1,   2.2, 1.5, Infinity, 2,   1.5, 2.2, 1.15, 1,   Infinity],
  elf:      [1, 2.4, 1,   1.8, Infinity, 1.2, 1.6, 2.4, 1.1,  1.3, Infinity],
  human:    [1, 2,   1.4, 1.6, Infinity, 1.6, 1.6, 2.3, 1,    1.1, Infinity],
  goblin:   [1, 1.3, 1.4, 1.5, Infinity, 1.0, 1.5, 2.1, 1.15, 1,   Infinity],
  kobold:   [1, 1.8, 1.1, 1.6, Infinity, 1.2, 1.7, 2.3, 1.3,  1.1, Infinity],
  other:    [1, 1.8, 1.4, 1.6, Infinity, 1.5, 1.5, 2.2, 1,    1.2, Infinity],
};

// Search actual world tiles, even when the displayed influence grid is downsampled.
async function routeDistances(seed: TerritorySeed, radius: number, width: number, height: number,
  terrain: Uint8Array | undefined, water: Uint8Array | undefined, costs: readonly number[],
  yieldToBrowser: YieldToBrowser, cancelled: () => boolean) {
  const sx = Math.floor(seed.x), sy = Math.floor(seed.y);
  const left = Math.max(0, sx - Math.ceil(radius)), top = Math.max(0, sy - Math.ceil(radius));
  const right = Math.min(width - 1, sx + Math.ceil(radius)), bottom = Math.min(height - 1, sy + Math.ceil(radius));
  const stride = right - left + 1;
  const distances = new Float64Array(stride * (bottom - top + 1)).fill(Infinity);
  const costAt = (x: number, y: number) => isOceanTile(water, width, x, y)
    ? Infinity : (costs[terrain?.[y * width + x] ?? 0] ?? 1);
  const queue = new TinyQueue<{ x: number; y: number; distance: number }>([], (a, b) => a.distance - b.distance);
  const index = (x: number, y: number) => (y - top) * stride + x - left;
  distances[index(sx, sy)] = 0;
  queue.push({ x: sx, y: sy, distance: 0 });
  let visited = 0;
  while (queue.length) {
    const { x, y, distance } = queue.pop()!;
    if (distance !== distances[index(x, y)]) continue;
    if (++visited % 4096 === 0) {
      await yieldToBrowser();
      if (cancelled()) return null;
    }
    const from = costAt(x, y);
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx, ny = y + dy;
        if ((!dx && !dy) || nx < left || nx > right || ny < top || ny > bottom) continue;
        if (dx && dy && (!Number.isFinite(costAt(nx, y)) || !Number.isFinite(costAt(x, ny)))) continue;
        const length = dx && dy ? Math.SQRT2 : 1;
        const next = distance + length * (from + costAt(nx, ny)) / 2;
        const i = index(nx, ny);
        if (next >= radius || next >= distances[i]) continue;
        distances[i] = next;
        queue.push({ x: nx, y: ny, distance: next });
      }
    }
  }
  return (x: number, y: number) => x < left || x > right || y < top || y > bottom
    ? Infinity : distances[index(x, y)];
}

export async function createTerritoryGrid(
  width: number,
  height: number,
  seeds: readonly TerritorySeed[],
  yieldToBrowser: YieldToBrowser = async () => {},
  cancelled: () => boolean = () => false,
  oceanMask?: Uint8Array,
  cachedLandComponents?: Int32Array,
  terrain?: Uint8Array
): Promise<TerritoryGrid | null> {
  const rasterWidth = Math.max(1, Math.min(320, Math.ceil(width)));
  const rasterHeight = Math.max(1, Math.min(320, Math.ceil(height)));
  const nation = new Int32Array(rasterWidth * rasterHeight);
  nation.fill(-1);
  const colors = new Map<number, string>();
  if (width <= 0 || height <= 0) return { width: rasterWidth, height: rasterHeight, nation, colors };

  const baseRadius = Math.max(7, Math.min(140, Math.hypot(width, height) * 0.045));
  const landComponents = oceanMask
    ? (cachedLandComponents?.length === width * height
        ? cachedLandComponents : buildLandComponents(width, height, oceanMask))
    : undefined;
  const sourceComponents = landComponents ? new Int32Array(nation.length) : undefined;
  const rasterLandComponents = landComponents ? new Int32Array(nation.length) : undefined;
  if (landComponents && rasterLandComponents) {
    for (let py = 0; py < rasterHeight; py++) {
      const worldY = Math.min(height - 1, Math.floor((py + 0.5) * height / rasterHeight));
      for (let px = 0; px < rasterWidth; px++) {
        const worldX = Math.min(width - 1, Math.floor((px + 0.5) * width / rasterWidth));
        rasterLandComponents[py * rasterWidth + px] = landComponents[worldY * width + worldX];
      }
    }
  }
  const result = (): TerritoryGrid => ({
    width: rasterWidth, height: rasterHeight, nation, colors,
    ...(landComponents ? { landComponents, sourceComponents } : {}),
  });
  // Separate factions by land component so no field jumps the sea.
  const groups = new Map<string, {
    nationId: number; componentId: number; sites: TerritorySeed[];
  }>();
  for (const seed of seeds) {
    if (!Number.isFinite(seed.x) || !Number.isFinite(seed.y) ||
        seed.x < 0 || seed.x >= width || seed.y < 0 || seed.y >= height) continue;
    if (isOceanTile(oceanMask, width, Math.floor(seed.x), Math.floor(seed.y))) continue;
    const componentId = landComponents?.[Math.floor(seed.y) * width + Math.floor(seed.x)] ?? 1;
    if (!componentId) continue;
    const key = `${seed.nationId}:${componentId}`;
    const group = groups.get(key);
    if (group) group.sites.push(seed);
    else groups.set(key, { nationId: seed.nationId, componentId, sites: [seed] });
    colors.set(seed.nationId, seed.color);
  }
  if (groups.size === 0) return result();

  // Reuse a single accumulator for every nation. A generation-stamp array
  // and list of touched cells avoids both a map per pixel and a full-grid
  // scan for every faction (important for worlds with many minor powers).
  const bestStrength = new Float64Array(nation.length);
  const runner = new Int32Array(nation.length).fill(-1);
  const runnerStrength = new Float64Array(nation.length);
  const strongestSeed = new Int32Array(nation.length).fill(-1);
  const seedIndex = new Map(seeds.map((seed, index) => [seed, index]));
  const accumulated = new Float64Array(nation.length);
  const topContribution = new Float64Array(nation.length);
  const topSeed = new Int32Array(nation.length);
  const siteAnchors = new Uint8Array(nation.length);
  const anchorOwner = new Int32Array(nation.length).fill(-1);
  const anchorComponent = new Int32Array(nation.length);
  const stamps = new Uint32Array(nation.length);
  const touched: number[] = [];
  let stamp = 0;
  let groupsProcessed = 0;
  const pixelWidth = width / rasterWidth;
  const pixelHeight = height / rasterHeight;

  for (const { nationId, componentId, sites } of groups.values()) {
    for (const seed of sites) {
      const cell = Math.floor(seed.y / pixelHeight) * rasterWidth + Math.floor(seed.x / pixelWidth);
      siteAnchors[cell] = 1;
      anchorOwner[cell] = nationId;
      anchorComponent[cell] = componentId;
    }
  }
  for (const { nationId, componentId, sites: nationSeeds } of groups.values()) {
    if (cancelled()) return null;
    stamp++;
    touched.length = 0;

    // Sum all coordinates of a multi-tile settlement before ranking contributors.
    const bySite = new Map<number, TerritorySeed[]>();
    for (const seed of nationSeeds) {
      const id = seed.siteId ?? -(seedIndex.get(seed)! + 1);
      const existing = bySite.get(id);
      if (existing) existing.push(seed);
      else bySite.set(id, [seed]);
    }
    let processed = 0;
    for (const site of bySite.values()) {
      const contributions = new Map<number, number>();
      for (const seed of site) {
        if (cancelled()) return null;

        const label = seed.race?.trim().toLowerCase() ?? 'other';
        const race = Object.prototype.hasOwnProperty.call(raceAliases, label) ? raceAliases[label] : label;
        const costs = Object.prototype.hasOwnProperty.call(terrainCosts, race) ? terrainCosts[race] : terrainCosts.other;
        // Log scaling gives large cities more reach without allowing one
        // enormous settlement to overpower an entire surrounding region.
        // Unknown population has the same weight as a single ordinary site.
        const population = Number.isFinite(seed.population) ? Math.max(0, seed.population!) : 0;
        const populationScale = Math.log10(1 + population / 100);
        const weight = (1 + Math.min(4.8, populationScale * 1.10)) /
          Math.max(1, seed.coordinateCount ?? 1);
        const radius = baseRadius * (1 + Math.min(0.45, populationScale * 0.12));
        const radiusSquared = radius * radius;
        const routes = terrain || oceanMask
          ? await routeDistances(seed, radius, width, height, terrain, oceanMask, costs, yieldToBrowser, cancelled)
          : undefined;
        if (routes === null) return null;
        const extent = routes ? radius + 1 : radius;
        const minX = Math.max(0, Math.ceil((seed.x - extent) / pixelWidth - 0.5));
        const maxX = Math.min(rasterWidth - 1, Math.floor((seed.x + extent) / pixelWidth - 0.5));
        const minY = Math.max(0, Math.ceil((seed.y - extent) / pixelHeight - 0.5));
        const maxY = Math.min(rasterHeight - 1, Math.floor((seed.y + extent) / pixelHeight - 0.5));

        for (let py = minY; py <= maxY; py++) {
          const dy = (py + 0.5) * pixelHeight - seed.y;
          const dySquared = dy * dy;
          if (!routes && dySquared >= radiusSquared) continue;
          for (let px = minX; px <= maxX; px++) {
            const dx = (px + 0.5) * pixelWidth - seed.x;
            const distanceSquared = dx * dx + dySquared;
            if (!routes && distanceSquared >= radiusSquared) continue;
            const cell = py * rasterWidth + px;
            if (rasterLandComponents && rasterLandComponents[cell] !== componentId) continue;
            const distance = routes?.(Math.floor((px + 0.5) * pixelWidth), Math.floor((py + 0.5) * pixelHeight));
            const weightedDistance = distance === undefined ? distanceSquared : distance * distance;
            if (weightedDistance >= radiusSquared) continue;
            const falloff = 1 - weightedDistance / radiusSquared;
            const contribution = weight * falloff * falloff;
            contributions.set(cell, (contributions.get(cell) ?? 0) + contribution);
          }
        }
        const anchor = Math.floor(seed.y / pixelHeight) * rasterWidth + Math.floor(seed.x / pixelWidth);
        if (!contributions.has(anchor)) contributions.set(anchor, 0);
        if (++processed % 32 === 0) await yieldToBrowser();
      }
      for (const [cell, contribution] of contributions) {
        if (stamps[cell] !== stamp) {
          stamps[cell] = stamp;
          accumulated[cell] = 0;
          topContribution[cell] = -1;
          touched.push(cell);
        }
        accumulated[cell] += contribution;
        if (contribution > topContribution[cell]) {
          topContribution[cell] = contribution;
          topSeed[cell] = seedIndex.get(site[0])!;
        }
      }
    }

    for (const cell of touched) {
      if (siteAnchors[cell] && componentId !== anchorComponent[cell]) continue;
      const strength = accumulated[cell];
      const wins = nation[cell] < 0 || anchorOwner[cell] === nationId ||
        (anchorOwner[cell] !== nation[cell] && (strength > bestStrength[cell] ||
          (strength === bestStrength[cell] && nationId < nation[cell])));
      if (wins) {
        if (nation[cell] >= 0 && (bestStrength[cell] > runnerStrength[cell] || runner[cell] < 0 ||
            (bestStrength[cell] === runnerStrength[cell] && nation[cell] < runner[cell]))) {
          runner[cell] = nation[cell];
          runnerStrength[cell] = bestStrength[cell];
        }
        bestStrength[cell] = strength;
        nation[cell] = nationId;
        strongestSeed[cell] = topSeed[cell];
        if (sourceComponents) sourceComponents[cell] = componentId;
      } else if (runner[cell] < 0 || strength > runnerStrength[cell] ||
          (strength === runnerStrength[cell] && nationId < runner[cell])) {
        runner[cell] = nationId;
        runnerStrength[cell] = strength;
      }
    }
    groupsProcessed++;
    if (groupsProcessed % 16 === 0 || groupsProcessed === groups.size) {
      await yieldToBrowser();
      if (cancelled()) return null;
    }
  }

  const grid = result();
  grid.evidence = { seeds, strongestSeed, strength: bestStrength, runner, runnerStrength, anchors: siteAnchors };
  pruneUnanchoredClaims(grid, siteAnchors);
  return grid;
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
    let leftId = -1;
    for (let x = 0; x < canvas.width; x++) {
      const id = getTerritoryNationAtTile(grid, canvas.width, canvas.height, x, y, oceanMask) ?? -1;
      if (id >= 0) {
        if (!cache.has(id)) cache.set(id, rgb(grid.colors.get(id) ?? '#777777'));
        const [r, g, b] = cache.get(id)!;
        const pos = (y * canvas.width + x) * 4;
        const border = showBorderLines &&
          ((x > 0 && leftId !== id) || (y > 0 && previousRow[x] !== id));
        pixels.data[pos] = border ? 22 : r;
        pixels.data[pos + 1] = border ? 26 : g;
        pixels.data[pos + 2] = border ? 28 : b;
        pixels.data[pos + 3] = border ? 224 : 195;
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

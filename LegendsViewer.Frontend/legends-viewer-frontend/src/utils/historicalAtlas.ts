import {
  getSiteOwnerAtYear,
  type SiteWithOwnershipHistory,
} from './siteHistory';

export interface AtlasCoordinate { x?: number; y?: number }
export interface AtlasSite extends SiteWithOwnershipHistory {
  coordinates?: AtlasCoordinate[] | null;
  type?: string | null;
}
export interface TerritorySeed {
  x: number;
  y: number;
  nationId: number;
  color: string;
}

export function getTerritorySeeds(
  sites: readonly AtlasSite[],
  year: number,
  presentYear: number,
  includeSite: (site: AtlasSite) => boolean = () => true
): TerritorySeed[] {
  const seeds: TerritorySeed[] = [];
  for (const site of sites) {
    if (!includeSite(site)) continue;
    // Sites without any historical ownership can be shown at the current
    // year, but must not project modern control centuries into the past.
    if (year < presentYear && !(site.ownerHistory?.length)) continue;
    const owner = getSiteOwnerAtYear(site, year, presentYear);
    // Do not invent territory from unclaimed or undated ownership.
    if (!owner.visible || owner.ownerId == null || owner.ownerText === 'Unclaimed') continue;
    for (const location of site.coordinates ?? []) {
      if (location.x == null || location.y == null ||
        !Number.isFinite(location.x) || !Number.isFinite(location.y)) continue;
      seeds.push({ x: location.x, y: location.y, nationId: owner.ownerId, color: owner.color });
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

export type YieldToBrowser = () => Promise<void>;

// Bounded-resolution, bounded-influence *approximation* of settlement
// territory; the game does not provide political borders in legends exports.
// Spatial buckets prevent scanning every world site for every raster cell.
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

  const radius = Math.max(7, Math.min(140, Math.hypot(width, height) * 0.045));
  const radiusSquared = radius * radius;

  // Jump-flood the settlement seeds on a resolution-capped grid. Unlike
  // testing every site against every cell, this has bounded O(pixels log pixels)
  // cost even when thousands of settlements cluster in one province.
  const validSeeds: TerritorySeed[] = [];
  let nearest = new Int32Array(rasterWidth * rasterHeight);
  nearest.fill(-1);
  for (const seed of seeds) {
    if (seed.x < 0 || seed.x >= width || seed.y < 0 || seed.y >= height) continue;
    if (isOceanTile(oceanMask, width, Math.floor(seed.x), Math.floor(seed.y))) continue;
    colors.set(seed.nationId, seed.color);
    const px = Math.min(rasterWidth - 1, Math.floor(seed.x * rasterWidth / width));
    const py = Math.min(rasterHeight - 1, Math.floor(seed.y * rasterHeight / height));
    const cell = py * rasterWidth + px;
    const old = nearest[cell];
    const distance = (seed.x - (px + 0.5) * width / rasterWidth) ** 2 +
      (seed.y - (py + 0.5) * height / rasterHeight) ** 2;
    const oldDistance = old < 0 ? Infinity :
      (validSeeds[old].x - (px + 0.5) * width / rasterWidth) ** 2 +
      (validSeeds[old].y - (py + 0.5) * height / rasterHeight) ** 2;
    if (distance < oldDistance || (distance === oldDistance &&
      (old < 0 || seed.nationId < validSeeds[old].nationId))) {
      nearest[cell] = validSeeds.length;
    }
    validSeeds.push(seed);
  }
  if (validSeeds.length === 0) {
    return { width: rasterWidth, height: rasterHeight, nation, colors };
  }

  const maxDimension = Math.max(rasterWidth, rasterHeight);
  let step = 1;
  while (step < maxDimension) step *= 2;

  for (step = Math.floor(step / 2); step >= 1; step = Math.floor(step / 2)) {
    let next = new Int32Array(rasterWidth * rasterHeight);
    next.fill(-1);
    for (let py = 0; py < rasterHeight; py++) {
      if (cancelled()) return null;
      const y = (py + 0.5) * height / rasterHeight;
      for (let px = 0; px < rasterWidth; px++) {
        const x = (px + 0.5) * width / rasterWidth;
        const pos = py * rasterWidth + px;
        let nearestSeed = nearest[pos];
        let bestDistance = nearestSeed < 0 ? Infinity :
          (validSeeds[nearestSeed].x - x) ** 2 + (validSeeds[nearestSeed].y - y) ** 2;
        for (let dy = -step; dy <= step; dy += step) {
          const ny = py + dy;
          if (ny < 0 || ny >= rasterHeight) continue;
          for (let dx = -step; dx <= step; dx += step) {
            const nx = px + dx;
            if (nx < 0 || nx >= rasterWidth) continue;
            const candidate = nearest[ny * rasterWidth + nx];
            if (candidate < 0) continue;
            const seed = validSeeds[candidate];
            const distance = (seed.x - x) ** 2 + (seed.y - y) ** 2;
            if (distance < bestDistance ||
              (distance === bestDistance && nearestSeed >= 0 &&
                seed.nationId < validSeeds[nearestSeed].nationId)) {
              nearestSeed = candidate;
              bestDistance = distance;
            }
          }
        }
        next[pos] = nearestSeed;
      }
      if (py % 32 === 31) await yieldToBrowser();
    }
    nearest = next;
  }

  for (let py = 0; py < rasterHeight; py++) {
    if (cancelled()) return null;
    const y = (py + 0.5) * height / rasterHeight;
    for (let px = 0; px < rasterWidth; px++) {
      const x = (px + 0.5) * width / rasterWidth;
      const pos = py * rasterWidth + px;
      const seedIndex = nearest[pos];
      if (seedIndex < 0) continue;
      const seed = validSeeds[seedIndex];
      const distance = (seed.x - x) ** 2 + (seed.y - y) ** 2;
      if (distance <= radiusSquared) nation[pos] = seed.nationId;
    }
    if (py % 32 === 31) await yieldToBrowser();
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
  worldHeight = grid.height
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
        const border = (x > 0 && leftId !== id) || (y > 0 && previousRow[x] !== id);
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

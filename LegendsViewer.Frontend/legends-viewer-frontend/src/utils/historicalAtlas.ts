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

export type YieldToBrowser = () => Promise<void>;

// Bounded-resolution, bounded-influence *approximation* of settlement
// territory; the game does not provide political borders in legends exports.
// Spatial buckets prevent scanning every world site for every raster cell.
export async function createTerritoryGrid(
  width: number,
  height: number,
  seeds: readonly TerritorySeed[],
  yieldToBrowser: YieldToBrowser = async () => {},
  cancelled: () => boolean = () => false
): Promise<TerritoryGrid | null> {
  const rasterWidth = Math.max(1, Math.min(320, Math.ceil(width)));
  const rasterHeight = Math.max(1, Math.min(320, Math.ceil(height)));
  const nation = new Int32Array(rasterWidth * rasterHeight);
  nation.fill(-1);
  const colors = new Map<number, string>();
  if (width <= 0 || height <= 0) return { width: rasterWidth, height: rasterHeight, nation, colors };

  const radius = Math.max(7, Math.min(140, Math.hypot(width, height) * 0.045));
  const radiusSquared = radius * radius;
  const binSize = radius;
  const buckets = new Map<string, TerritorySeed[]>();
  for (const seed of seeds) {
    if (seed.x < 0 || seed.x >= width || seed.y < 0 || seed.y >= height) continue;
    colors.set(seed.nationId, seed.color);
    const key = `${Math.floor(seed.x / binSize)}:${Math.floor(seed.y / binSize)}`;
    const bucket = buckets.get(key) ?? [];
    bucket.push(seed);
    buckets.set(key, bucket);
  }

  for (let py = 0; py < rasterHeight; py++) {
    if (cancelled()) return null;
    const y = (py + 0.5) * height / rasterHeight;
    const baseY = Math.floor(y / binSize);
    for (let px = 0; px < rasterWidth; px++) {
      const x = (px + 0.5) * width / rasterWidth;
      const baseX = Math.floor(x / binSize);
      let nearest = -1;
      let bestDistance = radiusSquared;
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          for (const seed of buckets.get(`${baseX + dx}:${baseY + dy}`) ?? []) {
            const distance = (seed.x - x) ** 2 + (seed.y - y) ** 2;
            if (distance < bestDistance ||
              (distance === bestDistance && (nearest < 0 || seed.nationId < nearest))) {
              nearest = seed.nationId;
              bestDistance = distance;
            }
          }
        }
      }
      nation[py * rasterWidth + px] = nearest;
    }
    if (py % 24 === 23) await yieldToBrowser();
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

export function territoryGridToDataUrl(grid: TerritoryGrid): string {
  const canvas = document.createElement('canvas');
  canvas.width = grid.width;
  canvas.height = grid.height;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas 2D rendering unavailable');
  const pixels = ctx.createImageData(grid.width, grid.height);
  const cache = new Map<number, [number, number, number]>();
  for (let y = 0; y < grid.height; y++) {
    for (let x = 0; x < grid.width; x++) {
      const offset = y * grid.width + x;
      const id = grid.nation[offset];
      if (id < 0) continue;
      if (!cache.has(id)) cache.set(id, rgb(grid.colors.get(id) ?? '#777777'));
      const [r, g, b] = cache.get(id)!;
      const pos = offset * 4;
      const border = (x > 0 && grid.nation[offset - 1] !== id) ||
        (y > 0 && grid.nation[offset - grid.width] !== id);
      pixels.data[pos] = border ? 22 : r;
      pixels.data[pos + 1] = border ? 26 : g;
      pixels.data[pos + 2] = border ? 28 : b;
      pixels.data[pos + 3] = border ? 190 : 142;
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

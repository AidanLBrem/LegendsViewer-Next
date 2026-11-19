import { describe, expect, it, vi } from 'vitest';
import {
  createTerritoryGrid, getTerritorySeeds, isOceanTile, territoryGridToDataUrl, isWarInYear,
  isBattleInYear, isSiteConquestInYear, getWarfareChangeYears
} from './historicalAtlas';

describe('historical atlas political influence', () => {
  it('colors nearby cells by the closest dated settlement, not global factions', async () => {
    const grid = await createTerritoryGrid(32, 32, [
      { x: 5, y: 5, nationId: 10, color: '#ff0000' },
      { x: 25, y: 25, nationId: 20, color: '#00ff00' },
    ]);
    expect(grid).not.toBeNull();
    expect(grid!.nation[5 * grid!.width + 5]).toBe(10);
    expect(grid!.nation[25 * grid!.width + 25]).toBe(20);
    // There is no recorded settlement nearby, so the overlay stays empty.
    expect(grid!.nation[0 * grid!.width + 31]).toBe(-1);
  });

  it('is bounded even for unusually large world maps', async () => {
    const grid = await createTerritoryGrid(1600, 1800, []);
    expect(grid!.width).toBe(320);
    expect(grid!.height).toBe(320);
    expect(grid!.nation.length).toBe(320 * 320);
  });

  it('does not invent ancient territory for sites without historical ownership', () => {
    const site = { coordinates: [{ x: 10, y: 10 }],
      ownerId: 42, ownerText: 'Current Empire', color: '#aa0000' };
    expect(getTerritorySeeds([site], 20, 100)).toEqual([]);
    expect(getTerritorySeeds([site], 100, 100)).toHaveLength(1);
  });

  it('uses the recorded historical parent faction rather than a subordinate site owner', () => {
    const site = {
      coordinates: [{ x: 5, y: 6 }],
      ownerHistory: [{ startYear: 40, endYear: -1,
        owner: 'Parent Necromancer', ownerText: 'Parent Necromancer',
        ownerId: 5, currentOwnerId: 21, color: '#445566' }],
    };
    const seeds = getTerritorySeeds([site], 50, 120);
    expect(seeds).toEqual([{ x: 5, y: 6, nationId: 5, color: '#445566' }]);
  });

  it('does not create territory from a nomadic group controlling only one site', () => {
    const loneCamp = {
      coordinates: [{ x: 5, y: 5 }, { x: 6, y: 5 }],
      ownerId: 10, ownerText: 'Wandering Band',
      ownerEntityType: 'NomadicGroup', color: '#aa4400',
    };
    const town = {
      coordinates: [{ x: 20, y: 20 }],
      ownerId: 20, ownerText: 'Settled Civilization',
      ownerEntityType: 'Civilization', color: '#0044aa',
    };

    expect(getTerritorySeeds([loneCamp, town], 100, 100)).toEqual([
      { x: 20, y: 20, nationId: 20, color: '#0044aa' },
    ]);
    // Even if one site has multiple coordinates, it is still one site.
    expect(getTerritorySeeds([loneCamp], 100, 100)).toEqual([]);
  });

  it('allows multi-site nomadic territory even when a site layer is hidden', () => {
    const camp = {
      coordinates: [{ x: 5, y: 5 }], ownerId: 10,
      ownerEntityType: 'NomadicGroup', ownerText: 'Wandering Band',
      color: '#aa4400',
    };
    const secondCamp = { ...camp, coordinates: [{ x: 12, y: 8 }] };
    expect(getTerritorySeeds([camp, secondCamp], 100, 100)).toHaveLength(2);
    expect(getTerritorySeeds(
      [camp, secondCamp], 100, 100, site => site === camp
    )).toEqual([{ x: 5, y: 5, nationId: 10, color: '#aa4400' }]);
  });

  it('recalculates nomadic territory from ownership in the selected year', () => {
    const owner = {
      ownerId: 10, ownerText: 'Wandering Band',
      ownerEntityType: 'NomadicGroup', color: '#aa4400',
    };
    const firstCamp = {
      coordinates: [{ x: 5, y: 5 }],
      ownerHistory: [{ ...owner, startYear: 10, endYear: -1, startCause: 'founded' }],
    };
    const secondCamp = {
      coordinates: [{ x: 12, y: 8 }],
      ownerHistory: [{ ...owner, startYear: 40, endYear: 70, startCause: 'founded' }],
    };
    expect(getTerritorySeeds([firstCamp, secondCamp], 30, 100)).toEqual([]);
    expect(getTerritorySeeds([firstCamp, secondCamp], 50, 100)).toHaveLength(2);
    expect(getTerritorySeeds([firstCamp, secondCamp], 80, 100)).toEqual([]);
  });

  it('excludes ocean settlements as influence seeds, but keeps inland sites', async () => {
    const width = 16;
    const height = 16;
    const oceanMask = new Uint8Array(Math.ceil(width * height / 8));
    const index = 3 * width + 3;
    oceanMask[index >> 3] |= 1 << (index & 7);
    expect(isOceanTile(oceanMask, width, 3, 3)).toBe(true);
    expect(isOceanTile(oceanMask, width, 4, 3)).toBe(false);

    const grid = await createTerritoryGrid(width, height, [
      { x: 3, y: 3, nationId: 1, color: '#ff0000' },
      { x: 12, y: 12, nationId: 2, color: '#00ff00' },
    ], undefined, undefined, oceanMask);
    expect(grid).not.toBeNull();
    expect(grid!.colors.has(1)).toBe(false);
    expect(grid!.nation[12 * grid!.width + 12]).toBe(2);
  });

  it('renders ocean tiles transparent at exact world resolution, not the coarse influence resolution', () => {
    const worldWidth = 4;
    const worldHeight = 2;
    const oceanMask = new Uint8Array(1);
    // Coastline at x=1 in the first row and x=3 in the second row.
    oceanMask[0] = (1 << 1) | (1 << 7);
    const imageData = { data: new Uint8ClampedArray(worldWidth * worldHeight * 4) };
    let output: Uint8ClampedArray | undefined;
    const ctx = {
      createImageData: () => imageData,
      putImageData: (data: typeof imageData) => { output = data.data; },
    };
    vi.stubGlobal('document', {
      createElement: () => ({
        width: 0, height: 0,
        getContext: () => ctx,
        toDataURL: () => 'data:image/png;base64,',
      }),
    });
    try {
      const grid = {
        width: 2, height: 1,
        nation: new Int32Array([7, 7]),
        colors: new Map([[7, '#112233']]),
      };
      territoryGridToDataUrl(grid, oceanMask, worldWidth, worldHeight);
      expect(output).toBeDefined();
      expect(output![0 * 4 + 3]).toBeGreaterThan(0);
      expect(output![1 * 4 + 3]).toBe(0);
      expect(output![2 * 4 + 3]).toBeGreaterThan(0);
      expect(output![7 * 4 + 3]).toBe(0);
      expect(output![6 * 4 + 3]).toBeGreaterThan(0);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it('cancels during a large territory raster pass', async () => {
    let called = 0;
    const grid = await createTerritoryGrid(
      1200, 1200, [{ x: 100, y: 100, nationId: 1, color: '#c00' }],
      async () => { called++; },
      () => called > 0
    );
    expect(grid).toBeNull();
  });
});

describe('historical war and battle events', () => {
  it('shows campaign intervals but battles and captures only in their recorded year', () => {
    expect(isWarInYear({ startYear: 20, endYear: 50 }, 19)).toBe(false);
    expect(isWarInYear({ startYear: 20, endYear: 50 }, 20)).toBe(true);
    expect(isWarInYear({ startYear: 20, endYear: 50 }, 50)).toBe(true);
    expect(isWarInYear({ startYear: 20, endYear: 50 }, 51)).toBe(false);
    expect(isBattleInYear({ startYear: 31 }, 31)).toBe(true);
    expect(isBattleInYear({ startYear: 31 }, 32)).toBe(false);
    expect(isSiteConquestInYear({ year: 41 }, 41)).toBe(true);
    expect(isSiteConquestInYear({ year: 41 }, 42)).toBe(false);
  });

  it('adds warfare years to timeline event-step navigation', () => {
    const years = getWarfareChangeYears({
      wars: [{ startYear: 20, endYear: 60 }],
      battles: [{ startYear: 30 }, { startYear: 42 }],
      siteConquests: [{ year: 42 }, { year: 55 }],
    }, 100);
    expect(years).toEqual([20, 30, 42, 55, 60]);
  });
});

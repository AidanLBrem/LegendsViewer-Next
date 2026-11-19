import { describe, expect, it } from 'vitest';
import {
  createTerritoryGrid, getTerritorySeeds, isWarInYear,
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

import { describe, expect, it } from 'vitest';
import type { SiteWithOwnershipHistory } from '../utils/siteHistory';
import { getTerritorySeeds, type AtlasSite } from '../utils/historicalAtlas';
import {
  buildNationExpansionHistory,
  listHistoricalNations,
  nationExpansionAtYear,
  nationExpansionChartPath,
} from '../utils/nationExpansion';

const sites: SiteWithOwnershipHistory[] = [
  {
    ownerHistory: [
      { startYear: 10, endYear: 30, ownerId: 1, ownerText: 'Dwarves', startCause: 'founded' },
      { startYear: 30, ownerId: 2, ownerText: 'Elves', startCause: 'conquest' },
    ],
  },
  {
    ownerHistory: [
      { startYear: 20, endYear: 50, ownerId: 1, ownerText: 'Dwarves', startCause: 'founded' },
    ],
  },
  { ownerId: 1, ownerText: 'Dwarves' }, // A present-day site without a dated history.
];

describe('nation expansion timeline', () => {
  it('treats a conquest as a loss for one nation and a gain for the other', () => {
    const dwarves = buildNationExpansionHistory(sites, 1, 100);
    expect(nationExpansionAtYear(dwarves, 9).sites).toBe(0);
    expect(nationExpansionAtYear(dwarves, 10).sites).toBe(1);
    expect(nationExpansionAtYear(dwarves, 20).sites).toBe(2);
    expect(nationExpansionAtYear(dwarves, 29).sites).toBe(2);
    expect(nationExpansionAtYear(dwarves, 30).sites).toBe(1);
    expect(nationExpansionAtYear(dwarves, 50).sites).toBe(0);
    expect(nationExpansionAtYear(dwarves, 100).sites).toBe(1);
    expect(dwarves.peakSites).toBe(2);
    expect(dwarves.peakYear).toBe(20);
    expect(nationExpansionAtYear(dwarves, 100).totalGained).toBe(3);
    expect(nationExpansionAtYear(dwarves, 100).totalLost).toBe(2);

    const elves = buildNationExpansionHistory(sites, 2, 100);
    expect(nationExpansionAtYear(elves, 29).sites).toBe(0);
    expect(nationExpansionAtYear(elves, 30).sites).toBe(1);
    expect(nationExpansionAtYear(elves, 100).sites).toBe(1);
  });

  it('does not backdate sites with no history, and lists extinct owners', () => {
    const nations = listHistoricalNations(sites, 100);
    expect(nations.find(nation => nation.id === 1)?.currentSites).toBe(1);
    expect(nations.find(nation => nation.id === 2)?.currentSites).toBe(1);
    const dwarves = buildNationExpansionHistory(sites, 1, 100);
    expect(nationExpansionAtYear(dwarves, 0).sites).toBe(0);
    expect(nationExpansionAtYear(dwarves, 99).sites).toBe(0);
  });

  it('draws a stepped rather than interpolated ownership curve', () => {
    const path = nationExpansionChartPath(buildNationExpansionHistory(sites, 1, 100), 100);
    expect(path).toContain(' H');
    expect(path).toContain(' V');
    expect(path).not.toContain(' L');
  });

  it('handles a nation without dated acquisitions', () => {
    const history = buildNationExpansionHistory([{ ownerId: 7, ownerText: 'Others' }], 7, 0);
    expect(nationExpansionAtYear(history, 0).sites).toBe(1);
    expect(history.peakSites).toBe(1);
    expect(nationExpansionChartPath(history, 0)).toBeTruthy();
  });
});

describe('historical territory sizing', () => {
  it('uses present population only for the current map, not earlier estimates', () => {
    const site: AtlasSite = {
      ownerId: 1, owner: 'Dwarves', ownerText: 'Dwarves', color: '#333333',
      populationCount: 15000,
      residentRace: 'Dwarf',
      coordinates: [{ x: 5, y: 5 }],
      ownerHistory: [{
        startYear: 10, ownerId: 1, owner: 'Dwarves', ownerText: 'Dwarves',
        color: '#333333', startCause: 'founded',
      }],
    };
    expect(getTerritorySeeds([site], 50, 100)[0].population).toBeUndefined();
    expect(getTerritorySeeds([site], 100, 100)[0].population).toBe(15000);
  });
});

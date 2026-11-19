import { describe, expect, it } from 'vitest';
import {
  getOwnershipChangeYears,
  getSiteOwnerAtYear,
  getTimelineMaxYear,
  isSitePresentAtYear,
  stepOwnershipChange,
  type SiteWithOwnershipHistory,
} from './siteHistory';

const contestedSite: SiteWithOwnershipHistory = {
  owner: 'Current Empire',
  ownerText: 'Current Empire',
  ownerId: 3,
  color: '#333333',
  ownerHistory: [
    { startYear: 10, endYear: 30, owner: 'Founders', ownerText: 'Founders',
      ownerId: 1, currentOwnerId: 100, color: '#111111', startCause: 'founded' },
    { startYear: 30, endYear: 70, owner: 'Conquerors', ownerText: 'Conquerors',
      ownerId: 2, currentOwnerId: 200, color: '#222222', startCause: 'conquest' },
    { startYear: 70, endYear: -1, owner: 'Current Empire', ownerText: 'Current Empire',
      ownerId: 3, currentOwnerId: 300, color: '#333333' },
  ],
};

describe('map history stepping', () => {
  it('uses the active owner and a half-open range at a conquest year', () => {
    expect(getSiteOwnerAtYear(contestedSite, 29, 100).ownerId).toBe(1);
    expect(getSiteOwnerAtYear(contestedSite, 30, 100).ownerId).toBe(2);
    expect(getSiteOwnerAtYear(contestedSite, 69, 100).ownerId).toBe(2);
    expect(getSiteOwnerAtYear(contestedSite, 70, 100).ownerId).toBe(3);
  });

  it('hides sites before their recorded foundation', () => {
    expect(getSiteOwnerAtYear(contestedSite, 9, 100).visible).toBe(false);
    expect(getSiteOwnerAtYear(contestedSite, 10, 100).visible).toBe(true);
  });

  it('keeps abandoned and ownerless sites on the map after an ownership period ends', () => {
    const abandoned: SiteWithOwnershipHistory = {
      ownerHistory: [{
        startYear: 10, endYear: 30, endCause: 'abandoned',
        owner: 'Founders', ownerText: 'Founders', ownerId: 1,
      }],
    };
    expect(getSiteOwnerAtYear(abandoned, 29, 100).ownerId).toBe(1);
    expect(getSiteOwnerAtYear(abandoned, 30, 100).visible).toBe(true);
    expect(getSiteOwnerAtYear(abandoned, 30, 100).ownerText).toBe('Unclaimed');
    expect(getSiteOwnerAtYear(abandoned, 100, 100).visible).toBe(true);
  });

  it('hides a destroyed site until it is explicitly reclaimed', () => {
    const destroyed: SiteWithOwnershipHistory = {
      createdYear: 10,
      destroyedYears: [30],
      ownerHistory: [
        { startYear: 10, endYear: 30, endCause: 'destroyed', startCause: 'founded',
          ownerText: 'Founders', ownerId: 1 },
        { startYear: 60, endYear: -1, startCause: 'reclaimed',
          ownerText: 'Reclaimers', ownerId: 2 },
      ],
    };
    expect(isSitePresentAtYear(destroyed, 9)).toBe(false);
    expect(isSitePresentAtYear(destroyed, 29)).toBe(true);
    expect(isSitePresentAtYear(destroyed, 30)).toBe(false);
    expect(isSitePresentAtYear(destroyed, 59)).toBe(false);
    expect(getSiteOwnerAtYear(destroyed, 60, 100).ownerText).toBe('Reclaimers');
    expect(getSiteOwnerAtYear(destroyed, 100, 100).visible).toBe(true);
    expect(getOwnershipChangeYears([destroyed], 100)).toEqual([0, 10, 30, 60, 100]);
  });

  it('keeps a site destroyed through the present year hidden', () => {
    const razed: SiteWithOwnershipHistory = {
      owner: 'Old Kingdom',
      ownerHistory: [{
        startYear: 10, endYear: 30,
        owner: 'Old Kingdom', ownerText: 'Old Kingdom',
        endCause: 'destroyed',
      }],
    };
    expect(getSiteOwnerAtYear(razed, 29, 100).visible).toBe(true);
    expect(getSiteOwnerAtYear(razed, 30, 100).visible).toBe(false);
    expect(getSiteOwnerAtYear(razed, 100, 100).visible).toBe(false);
  });

  it('does not treat first recorded conquest as the founding of an older site', () => {
    const site: SiteWithOwnershipHistory = {
      ownerHistory: [{ startYear: 80, endYear: -1,
        startCause: 'took over', ownerText: 'Conqueror' }],
    };
    expect(getSiteOwnerAtYear(site, 20, 100).visible).toBe(true);
    expect(getSiteOwnerAtYear(site, 20, 100).ownerText).toBe('Unclaimed');
    expect(getSiteOwnerAtYear(site, 80, 100).ownerText).toBe('Conqueror');
  });

  it('shows a site continuously through takeover and sparse owner gaps', () => {
    const site: SiteWithOwnershipHistory = {
      ownerHistory: [
        { startYear: 10, endYear: 25, startCause: 'constructed', endCause: 'taken over',
          ownerText: 'Original' },
        { startYear: 27, endYear: 50, startCause: 'took over', endCause: 'abandoned',
          ownerText: 'Invaders' },
        { startYear: 80, endYear: -1, startCause: 'reclaimed', ownerText: 'Returned' },
      ],
    };
    expect(getSiteOwnerAtYear(site, 9, 100).visible).toBe(false);
    for (let year = 10; year <= 100; year++) {
      expect(getSiteOwnerAtYear(site, year, 100).visible).toBe(true);
    }
    expect(getSiteOwnerAtYear(site, 26, 100).ownerText).toBe('Unclaimed');
    expect(getSiteOwnerAtYear(site, 51, 100).ownerText).toBe('Unclaimed');
  });

  it('preserves current owner data when the history is unknown', () => {
    const undated = { owner: 'Unrecorded Holder', ownerText: 'Unrecorded Holder' };
    expect(getSiteOwnerAtYear(undated, 1, 100).owner).toBe('Unrecorded Holder');
    expect(getSiteOwnerAtYear(undated, 1, 100).visible).toBe(true);
    expect(getSiteOwnerAtYear(contestedSite, 100, 100).owner).toBe('Current Empire');
  });

  it('uses the shared parent faction for necromancer subsidiary sites', () => {
    const necromancer: SiteWithOwnershipHistory = {
      ownerHistory: [{
        startYear: 55, endYear: -1, owner: 'Necromancer', ownerText: 'Necromancer',
        ownerId: 42, currentOwnerId: 4201, actualOwner: 'Tower Garrison',
        color: '#aabbcc',
      }],
    };
    const otherTower: SiteWithOwnershipHistory = {
      ownerHistory: [{
        startYear: 57, endYear: -1, owner: 'Necromancer', ownerText: 'Necromancer',
        ownerId: 42, currentOwnerId: 4202, actualOwner: 'Crypt Garrison',
        color: '#aabbcc',
      }],
    };
    expect(getSiteOwnerAtYear(necromancer, 60, 100).ownerId).toBe(42);
    expect(getSiteOwnerAtYear(otherTower, 60, 100).ownerId).toBe(42);
    expect(getSiteOwnerAtYear(necromancer, 60, 100).currentOwnerId).toBe(4201);
    expect(getSiteOwnerAtYear(otherTower, 60, 100).currentOwnerId).toBe(4202);
  });

  it('recovers year 0/0 from historical ownership dates in old world responses', () => {
    // Imported worlds previously sent currentYear=0, which disabled Play
    // and incorrectly discarded every owner change after year zero.
    const maximum = getTimelineMaxYear(0, [contestedSite]);
    expect(maximum).toBe(70);
    expect(getOwnershipChangeYears([contestedSite], maximum)).toEqual([0, 10, 30, 70]);
    expect(stepOwnershipChange(0, 1, getOwnershipChangeYears([contestedSite], maximum))).toBe(10);
  });

  it('respects a newer world year and ignores unknown or negative dates', () => {
    const site: SiteWithOwnershipHistory = {
      ownerHistory: [{ startYear: -1, endYear: -1 }],
    };
    expect(getTimelineMaxYear(250, [contestedSite])).toBe(250);
    expect(getTimelineMaxYear(undefined, [site])).toBe(0);
    expect(getTimelineMaxYear(Number.NaN, [site])).toBe(0);
    expect(getTimelineMaxYear(0, [{ ownerHistory: [{ startYear: 100, endYear: 150 }] }])).toBe(150);
    expect(getTimelineMaxYear(0, [{ createdYear: 120, destroyedYears: [180] }])).toBe(180);
  });

  it('deduplicates and orders ownership starts and ends for event stepping', () => {
    const years = getOwnershipChangeYears([contestedSite, contestedSite], 100);
    expect(years).toEqual([0, 10, 30, 70, 100]);
    expect(stepOwnershipChange(11, -1, years)).toBe(10);
    expect(stepOwnershipChange(11, 1, years)).toBe(30);
    expect(stepOwnershipChange(100, 1, years)).toBe(100);
    expect(stepOwnershipChange(0, -1, years)).toBe(0);
  });
});

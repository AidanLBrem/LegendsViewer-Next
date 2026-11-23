import { expect, it } from 'vitest';
import { diplomaticStatus, mapCategory, type Relation } from '../utils/nationMapColors';

const event = (kind: Relation['kind'], year: number, endYear = -1): Relation => ({ a: 1, b: 2, kind, year, endYear });

it('reconstructs relations at the selected year without reviving prewar alliances', () => {
    const history = [event('contact', 5), event('alliance', 10), event('war', 20, 25), event('alliance', 30)];
    expect([0, 5, 10, 20, 25, 30].map(year => diplomaticStatus(history, year)))
        .toEqual(['Unmet', 'Neutral', 'Allied', 'War', 'Neutral', 'Allied']);
    expect(diplomaticStatus([event('war', 2), event('peace', 8)], 8)).toBe('Neutral');
    expect(diplomaticStatus([event('war', 2, 8), event('war', 6, 12)], 9)).toBe('War');
});

it('uses the faction race, gives necromancers priority, and honors exact policies', () => {
    const nation = { id: 2, race: 'DWARF', raceName: 'Dwarf', necromancer: false, civilization: true, responses: {} };
    expect(mapCategory('Race', nation, false, '', '').label).toBe('Dwarf');
    expect(mapCategory('Race', nation, true, '', '').label).toBe('Necromancer');
    expect(mapCategory('Practice', nation, false, 'SLAVERY', '').label).toBe('Capital offense');
    expect(mapCategory('Practice', nation, true, 'SLAVERY', '').label).toBe('Unknown');
    expect(mapCategory('Practice', { ...nation, responses: { SLAVERY: 'ACCEPTABLE' } }, true, 'SLAVERY', '').label).toBe('Permitted');
});

it('keeps built-in race colors while assigning stable, distinct colors to modded races', () => {
    const base = { id: 1, race: 'DWARF', raceName: 'Dwarf', necromancer: false, civilization: true, responses: {} };
    expect(mapCategory('Race', base, false, '', '').color).toBe('#bd7739');
    const first = { ...base, race: 'DRAGONKIN', raceName: 'Dragonkin' };
    const second = { ...base, id: 2, race: 'MOTH PEOPLE', raceName: 'Moth People' };
    const firstColor = mapCategory('Race', first, false, '', '').color;
    expect(firstColor).toMatch(/^#[0-9a-f]{6}$/);
    expect(mapCategory('Race', { ...first, race: ' dragonkin ' }, false, '', '').color).toBe(firstColor);
    expect(mapCategory('Race', second, false, '', '').color).not.toBe(firstColor);
    expect(mapCategory('Race', first, true, '', '').color).toBe('#733fb0');
});

it('gives missing or unknown race data a stable per-nation fallback instead of gray', () => {
    const unknown = { id: 42, race: 'UNKNOWN', raceName: 'Unknown', necromancer: false, civilization: true, responses: {} };
    const first = mapCategory('Race', unknown, false, '', '');
    const next = mapCategory('Race', { ...unknown, id: 43 }, false, '', '');
    expect(first.label).toBe('Unknown race (#42)');
    expect(first.color).toMatch(/^#[0-9a-f]{6}$/);
    expect(first.color).not.toBe('#808080');
    expect(next.color).not.toBe(first.color);
    expect(mapCategory('Race', undefined, false, '', '', 42)).toEqual(first);
    expect(mapCategory('Race', unknown, false, '', '')).toEqual(first);
});

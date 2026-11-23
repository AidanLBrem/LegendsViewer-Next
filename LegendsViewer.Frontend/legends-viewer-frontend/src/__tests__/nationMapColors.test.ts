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

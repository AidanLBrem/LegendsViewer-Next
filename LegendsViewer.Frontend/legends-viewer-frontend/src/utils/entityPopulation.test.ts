import { describe, expect, it } from 'vitest';
import { summarizeNationPopulation } from './entityPopulation';

describe('nation population summaries', () => {
    it('sums and sorts recorded site residents by race', () => {
        const summary = summarizeNationPopulation([
            { race: { id: 'DWARF', namePlural: 'Dwarves' }, count: 120, isMainRace: true },
            { race: { id: 'HUMAN', namePlural: 'Humans' }, count: 70 },
            { race: { id: 'DWARF', namePlural: 'Dwarves' }, count: 50, isMainRace: true },
        ]);

        expect(summary.sitePopulationTotal).toBe(240);
        expect(summary.races).toHaveLength(2);
        expect(summary.races[0].race).toBe('Dwarves');
        expect(summary.races[0].count).toBe(170);
        expect(summary.races[0].percentage).toBeCloseTo(170 / 240 * 100);
        expect(summary.races[0].classifications).toEqual(['Civilized race']);
        expect(summary.races[1].count).toBe(70);
    });

    it('keeps additional entity census data separate from site totals', () => {
        const summary = summarizeNationPopulation(
            [{ race: { namePlural: 'Elves' }, count: 400 }],
            { race: { namePlural: 'Elves' }, count: 500 }
        );
        expect(summary.sitePopulationTotal).toBe(400);
        expect(summary.entityPopulationCount).toBe(500);
        expect(summary.entityPopulationRace).toBe('Elves');
    });

    it('reports when no site census exists but an entity-level record does', () => {
        const summary = summarizeNationPopulation(null, {
            race: { namePlural: 'Goblins' },
            count: 215,
        });
        expect(summary.races).toEqual([]);
        expect(summary.sitePopulationTotal).toBe(0);
        expect(summary.entityPopulationCount).toBe(215);
    });

    it('ignores invalid and negative counts without losing other residents', () => {
        const summary = summarizeNationPopulation([
            { race: { namePlural: 'Dwarves' }, count: 15 },
            { race: { namePlural: 'Dwarves' }, count: Number.NaN },
            { race: { namePlural: 'Humans' }, count: -2 },
            { race: { namePlural: 'Visitors' }, count: 3, isVisitors: true },
            { race: null, count: 2 },
        ], { race: { namePlural: 'Humans' }, count: -5 });
        expect(summary.sitePopulationTotal).toBe(20);
        expect(summary.entityPopulationCount).toBeNull();
        expect(summary.races.find(row => row.race === 'Visitors')?.classifications).toEqual(['Visitors']);
        expect(summary.races.find(row => row.race === 'Unknown')?.count).toBe(2);
    });

    it('does not create one record per resident for large populations', () => {
        const summary = summarizeNationPopulation([
            { race: { namePlural: 'Humans' }, count: 1_500_000 },
        ]);
        expect(summary.races).toHaveLength(1);
        expect(summary.sitePopulationTotal).toBe(1_500_000);
    });
});

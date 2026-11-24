import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, expect, it, vi } from 'vitest';
import Comparison from '../components/NationCultureComparison.vue';

afterEach(() => vi.unstubAllGlobals());

it('sorts both ways from every comparison header, using numeric rates', async () => {
    const rates = ['settlementExpansionPerCentury', 'conquestPerCentury', 'sitesLostInWarPerCentury',
        'offensiveOperationsPerCentury', 'plundersPerCentury', 'diplomacyPerCentury', 'upheavalsPerCentury'];
    const nations = ['Beryl', 'Amber'].map((name, id) => ({
        name, id, race: 'dwarves', currentSites: id ? 12 : 2, populationCount: id ? 120 : 2,
        periods: [{ windowYears: 100, character: id ? 'Mixed' : 'Defensive', sitesLostInWar: 0,
            ...Object.fromEntries(rates.map((key, index) => [key, (index + id) % 2 ? 12 : 2])) }]
    }));
    vi.stubGlobal('fetch', async () => ({ ok: true, json: async () => nations }));
    const wrapper = mount(Comparison, { global: {
        stubs: {
            RouterLink: { template: '<a><slot /></a>' },
            VTable: { template: '<table><slot /></table>' },
            VTooltip: { template: '<slot name="activator" :props="{}" />' },
            VIcon: true, VTextField: true, VCol: true, VSelect: true,
            VRow: true, VProgressLinear: true, VAlert: true
        }
    } });
    await flushPromises();
    const buttons = wrapper.findAll('th button');
    expect(buttons).toHaveLength(11);
    for (const [index, button] of buttons.entries()) {
        await button.trigger('click');
        const firstAscending = ['Amber', 'Beryl', 'Beryl', 'Beryl', 'Beryl', 'Amber', 'Beryl', 'Amber', 'Beryl', 'Amber', 'Beryl'][index];
        const firstDescending = firstAscending === 'Amber' ? 'Beryl' : 'Amber';
        const firstDirection = index < 2 ? 'ascending' : 'descending';
        expect(button.element.parentElement?.getAttribute('aria-sort')).toBe(firstDirection);
        expect(wrapper.find('tbody tr td a').text()).toBe(index < 2 ? firstAscending : firstDescending);
        await button.trigger('click');
        expect(button.element.parentElement?.getAttribute('aria-sort')).toBe(index < 2 ? 'descending' : 'ascending');
        expect(wrapper.find('tbody tr td a').text()).toBe(index < 2 ? firstDescending : firstAscending);
    }
});

import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, expect, it, vi } from 'vitest';
import Comparison from '../components/NationCultureComparison.vue';

afterEach(() => vi.unstubAllGlobals());

it('defaults to complete recorded history and displays event totals rather than per-century rates', async () => {
    const nations = [
        { name: 'Beryl', id: 1, race: 'dwarves', currentSites: 30,
          periods: [
              { windowYears: 0, character: 'Zulu', settlementsFounded: 12, conquests: 9,
                sitesLostInWar: 7, offensiveOperations: 16, sitesPlundered: 11,
                peaceAgreements: 4, alliances: 3, internalUpheavals: 5,
                settlementExpansionPerCentury: 0.3, conquestPerCentury: 0.2 },
              { windowYears: 100, character: 'Zulu', settlementsFounded: 1, conquests: 0,
                sitesLostInWar: 0, offensiveOperations: 1, sitesPlundered: 0,
                peaceAgreements: 0, alliances: 0, internalUpheavals: 0 }
          ] },
        { name: 'Amber', id: 2, race: 'dwarves', currentSites: 2,
          periods: [
              { windowYears: 0, character: 'Alpha', settlementsFounded: 2, conquests: 1,
                sitesLostInWar: 0, offensiveOperations: 3, sitesPlundered: 1,
                peaceAgreements: 1, alliances: 0, internalUpheavals: 0,
                settlementExpansionPerCentury: 900, conquestPerCentury: 900 },
              { windowYears: 100, character: 'Alpha', settlementsFounded: 50, conquests: 40,
                sitesLostInWar: 20, offensiveOperations: 80, sitesPlundered: 50,
                peaceAgreements: 30, alliances: 10, internalUpheavals: 15 }
          ] },
    ];
    vi.stubGlobal('fetch', async () => ({ ok: true, json: async () => nations }));
    const wrapper = mount(Comparison, { global: {
        stubs: {
            RouterLink: { template: '<a><slot /></a>' },
            VTable: { template: '<table><slot /></table>' },
            VIcon: true, VTextField: true, VCol: true, VSelect: true,
            VRow: true, VProgressLinear: true, VAlert: true
        }
    } });
    await flushPromises();

    // The full-history totals, including combined diplomacy, must be displayed
    // even when the API also supplies unrelated normalized rates.
    const firstRow = wrapper.findAll('tbody tr')[0].findAll('td');
    expect(firstRow.slice(3).map(cell => cell.text())).toEqual(['12', '9', '7', '16', '11', '7', '5']);
    expect(wrapper.text()).toContain('total recorded events');
    expect(wrapper.text()).not.toContain('per 100 observed years');

    const buttons = wrapper.findAll('th button');
    expect(buttons).toHaveLength(10);
    for (const [index, button] of buttons.entries()) {
        await button.trigger('click');
        const first = index < 2 ? 'Amber' : 'Beryl';
        const second = index < 2 ? 'Beryl' : 'Amber';
        expect(button.element.parentElement?.getAttribute('aria-sort')).toBe(index < 2 ? 'ascending' : 'descending');
        expect(wrapper.find('tbody tr td a').text()).toBe(first);
        await button.trigger('click');
        expect(button.element.parentElement?.getAttribute('aria-sort')).toBe(index < 2 ? 'descending' : 'ascending');
        expect(wrapper.find('tbody tr td a').text()).toBe(second);
    }
});

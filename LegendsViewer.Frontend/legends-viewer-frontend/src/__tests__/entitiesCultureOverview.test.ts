import { describe, expect, it, vi } from 'vitest';
import { defineComponent, h } from 'vue';
import { mount } from '@vue/test-utils';
import Entities from '../views/Entities.vue';

vi.mock('../stores/worldObjectStores', () => ({
  useEntityStore: () => ({})
}));

// The same slot that WorldObjectsPage renders inside the Overview card
// must carry the culture comparison. The former before-table slot would
// instead make a separate panel above Overview.
const WorldObjectsPageStub = defineComponent({
  name: 'WorldObjectsPage',
  setup(_, { slots }) {
    return () => h('div', [
      h('section', { 'data-area': 'before-overview' },
        slots['type-specific-before-table']?.()),
      h('section', { 'data-area': 'inside-overview' },
        slots['overview-content-after-table']?.())
    ]);
  }
});

describe('Factions and Groups Overview', () => {
  it('renders civilization culture comparison in Overview, not as a separate panel', () => {
    const wrapper = mount(Entities, {
      global: {
        stubs: {
          WorldObjectsPage: WorldObjectsPageStub,
          NationCultureComparison: {
            name: 'NationCultureComparison',
            template: '<div data-test="culture-comparison">Culture comparison</div>'
          },
          EntityFilter: true
        }
      }
    });

    expect(wrapper.find('[data-area="inside-overview"] [data-test="culture-comparison"]').exists()).toBe(true);
    expect(wrapper.find('[data-area="before-overview"] [data-test="culture-comparison"]').exists()).toBe(false);
    wrapper.unmount();
  });
});

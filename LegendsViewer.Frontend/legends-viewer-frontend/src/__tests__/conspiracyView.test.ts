import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, shallowMount } from '@vue/test-utils';
import { useRoute } from 'vue-router';
import ConspiracyAnalysis from '../views/ConspiracyAnalysis.vue';

vi.mock('../apiClient', () => ({ API_BASE_URL: 'http://localhost:15421/' }));
vi.mock('../stores/worldStore', () => ({ useWorldStore: () => ({ world: '' }) }));
vi.mock('vue-router', async () => {
  const { reactive } = await import('vue');
  const route = reactive({ params: { figureId: '', localId: '' } });
  return { useRoute: () => route };
});
afterEach(() => vi.unstubAllGlobals());

describe('conspiracy view requests', () => {
  it('does not fetch a detail for empty optional route parameters', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ items: [], totalCount: 0, totalFilteredCount: 0, civilizations: [] }) });
    vi.stubGlobal('fetch', fetch);
    const wrapper = shallowMount(ConspiracyAnalysis);
    await flushPromises();
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch.mock.calls[0]![0]).toContain('api/conspiracies?page=1');
    wrapper.unmount();
  });
  it('fetches a detail when a local plot ID is zero', async () => {
    const route = useRoute(); route.params.figureId = '10'; route.params.localId = '0';
    const fetch = vi.fn().mockResolvedValue({ ok: false, status: 404 });
    vi.stubGlobal('fetch', fetch);
    const wrapper = shallowMount(ConspiracyAnalysis);
    await flushPromises();
    expect(fetch.mock.calls.some(call => call[0].endsWith('/api/conspiracies/10/0'))).toBe(true);
    wrapper.unmount(); route.params.figureId = ''; route.params.localId = '';
  });
});

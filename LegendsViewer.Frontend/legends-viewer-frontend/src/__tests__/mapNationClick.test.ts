import { afterEach, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import L from 'leaflet';
import MapView from '../views/Map.vue';

const { sites } = vi.hoisted(() => ({
  sites: [0, 1].map(id => {
    const owner = { ownerId: id, ownerText: 'Nation ' + id, owner: 'Nation ' + id,
      ownerEntityType: 'Civilization', color: id ? '#00aaff' : '#dd6611' };
    return { ...owner, id, name: 'Town ' + id, type: 'Town', coordinates: [{ x: 4 + 6 * id, y: 4 }],
      originOwnerId: 1 - id, originOwnerText: 'Nation ' + (1 - id), createdYear: 0,
      ownerHistory: [{ ...owner, startYear: 0, endYear: -1 }] };
  })
}));
vi.mock('../stores/worldStore', () => ({ useWorldStore: () => ({
  world: { width: 16, height: 16, currentYear: 10, siteMarkers: sites }, loadWorld: async () => {}
}) }));
vi.mock('../stores/mapStore', () => ({ useWorldMapStore: () => ({
  worldMapMax: 'data:image/png;base64,AA==', loadWorldMap: async () => {},
  loadWarfareMap: async () => ({ wars: [], battles: [], siteConquests: [] })
}) }));
vi.mock('vue-router', () => ({ useRoute: () => ({ query: {} }), useRouter: () => ({ push: vi.fn() }) }));

afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it('selects territory and site owners by map click with borders shown or hidden and in every color mode', async () => {
  const context = new Proxy({
    createImageData: (w: number, h: number) => ({ data: new Uint8ClampedArray(w * h * 4) }),
    measureText: () => ({ width: 100 }),
  }, { get: (target, prop) => Reflect.get(target, prop) ?? (() => {}) });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(context as unknown as CanvasRenderingContext2D);
  vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,AA==');
  vi.stubGlobal('fetch', async (url: string) => url.endsWith('terrain') ?
    { ok: true, arrayBuffer: async () => new ArrayBuffer(256) } :
    { ok: true, json: async () => ({ nations: [], relations: [] }) });
  let map!: L.Map;
  const initialize = L.Map.prototype.setView;
  vi.spyOn(L.Map.prototype, 'setView').mockImplementation(function(this: L.Map, ...args) {
    map = this;
    return initialize.apply(this, args);
  });
  const view = mount(MapView, { attachTo: document.body });
  try {
    const selected = () => (view.get('#nation-expansion-selector').element as HTMLSelectElement).value;
    const click = (x: number, y: number) => map.fire('click', { latlng: L.latLng((16 - y - 0.5) * 8, (x + 0.5) * 8) });
    await vi.waitFor(() => expect(view.find('.leaflet-territory-pane img').exists()).toBe(true));
    map.setZoom(2);
    for (const mode of ['Race', 'Practice', 'Diplomacy', 'Nation']) {
      await view.get('.nation-color-buttons').findAll('button').find(b => b.text() === mode)!.trigger('click');
      click(11, 4);
      await vi.waitFor(() => expect(selected()).toBe('1'));
      click(4, 4);
      await vi.waitFor(() => expect(selected()).toBe('0'));
    }
    const borders = view.findAll('label').find(label => label.text() === 'Estimated territories')!;
    await borders.get('input').setValue(false);
    await vi.waitFor(() => expect(view.find('.leaflet-territory-pane img').exists()).toBe(false));
    click(11, 4);
    await vi.waitFor(() => expect(selected()).toBe('1'));
    const marker = L.marker([(16 - 4.5) * 8, 4.5 * 8]).bindPopup('Battle').addTo(map);
    marker.fire('click', { latlng: marker.getLatLng() });
    await vi.waitFor(() => expect(selected()).toBe('0'));
    expect(marker.isPopupOpen()).toBe(true);
    click(10, 4);
    await vi.waitFor(() => expect(selected()).toBe('1'));
    await view.get('#site-color-mode').setValue('origin');
    click(10, 4);
    await vi.waitFor(() => expect(selected()).toBe('0'));
  } finally { view.unmount(); }
});

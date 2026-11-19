<template>
  <div class="map-container">
    <div id="map" style="height: 100%;"></div>
    <div v-if="mapError" class="map-error" role="alert">{{ mapError }}</div>
    <div v-if="mapProgress" class="map-progress" role="status">{{ mapProgress }}</div>
    <div v-if="ownerLayerNotice" class="map-owner-notice" role="status">{{ ownerLayerNotice }}</div>
    <div class="site-color-selector">
      <label for="site-color-mode">Site colors</label>
      <select id="site-color-mode" v-model="siteColorMode">
        <option value="current">Current nation</option>
        <option value="origin">Origin nation</option>
      </select>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, onMounted, watch, ref, shallowRef, toRaw, onBeforeUnmount, type Ref } from 'vue';
import { useRouter, useRoute, type Router } from 'vue-router';
import { useWorldStore } from '../stores/worldStore';
import { useWorldMapStore } from '../stores/mapStore';
import L, { Map as LeafletMap, ImageOverlay, Layer, LayerGroup } from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { components } from '../generated/api-schema'; // Import from the OpenAPI schema

import type { WarfareMapDto, WarMapOverlayDto, BattleMapMarkerDto } from '../types/legends';

export type SiteType = components['schemas']['SiteType'];
type SiteMarker = components['schemas']['SiteMarkerDto'];

export type MapTileItemType = 'site' | 'war' | 'battle';

export interface MapTileItem {
  type: MapTileItemType;
  x: number;
  y: number;
  layerGroup: LayerGroup;
  marker: L.Layer;
  data: SiteAtCoordinate | WarMapOverlayDto | BattleMapMarkerDto;
}

function formatEntityLinkWithBadge(id: number, name?: string, color?: string): string {
  const entityName = name || 'Unknown';
  const words = entityName.trim().split(/\s+/);
  const initials = words.length === 1 ? words[0].substring(0, 2).toUpperCase() : (words[0][0] + words[1][0]).toUpperCase();
  const bgColor = color && color !== '#666666' ? color : '#5c6bc0';
  const chipHtml = `<span style="background-color:${bgColor};color:#ffffff;margin-right:4px;" class="v-chip v-chip--label v-theme--dark v-chip--density-compact v-chip--size-default v-chip--variant-tonal soc" draggable="false"><span class="v-chip__underlay"></span><div class="v-chip__content" data-no-activator="">${initials}</div></span>`;
  return `${chipHtml}<a href="/entity/${id}">${entityName}</a>`;
}

function createWarPopupElement(war: WarMapOverlayDto): HTMLElement {
  const container = document.createElement('div');
  container.className = 'warfare-popup-item';

  const titleDiv = document.createElement('div');
  titleDiv.style.cssText = 'font-weight: 600; font-size: 14px; margin-bottom: 6px; display: flex; align-items: center; flex-wrap: wrap; gap: 4px;';

  if (war.link) {
    appendRichContent(titleDiv, war.link);
  } else {
    const color = war.attackerColor && war.attackerColor !== '#666666' ? war.attackerColor : '#E53935';
    titleDiv.innerHTML = `<i class="mdi mdi-sword-cross" style="color: ${color}; margin-right: 4px; font-size: 16px;"></i><a href="/war/${war.id}">${war.name}</a>`;
  }

  if (war.isActive) {
    const badge = document.createElement('span');
    badge.style.cssText = 'background: #e53935; color: white; border-radius: 4px; padding: 2px 6px; font-size: 10px; margin-left: 6px;';
    badge.textContent = 'Active';
    titleDiv.appendChild(badge);
  }
  container.appendChild(titleDiv);

  const attackerDiv = document.createElement('div');
  attackerDiv.style.cssText = 'font-size: 12px; margin-bottom: 4px; display: flex; align-items: center; flex-wrap: wrap;';
  const attackerLabel = document.createElement('strong');
  attackerLabel.textContent = 'Attacker:\u00A0';
  attackerDiv.appendChild(attackerLabel);
  if (war.attackerLink) {
    appendRichContent(attackerDiv, war.attackerLink);
  } else if (war.attackerId) {
    appendRichContent(attackerDiv, formatEntityLinkWithBadge(war.attackerId, war.attackerName, war.attackerColor));
  } else {
    attackerDiv.append(war.attackerName || 'Unknown');
  }
  container.appendChild(attackerDiv);

  const defenderDiv = document.createElement('div');
  defenderDiv.style.cssText = 'font-size: 12px; margin-bottom: 4px; display: flex; align-items: center; flex-wrap: wrap;';
  const defenderLabel = document.createElement('strong');
  defenderLabel.textContent = 'Defender:\u00A0';
  defenderDiv.appendChild(defenderLabel);
  if (war.defenderLink) {
    appendRichContent(defenderDiv, war.defenderLink);
  } else if (war.defenderId) {
    appendRichContent(defenderDiv, formatEntityLinkWithBadge(war.defenderId, war.defenderName, undefined));
  } else {
    defenderDiv.append(war.defenderName || 'Unknown');
  }
  container.appendChild(defenderDiv);

  const statsDiv = document.createElement('div');
  statsDiv.style.cssText = 'font-size: 11px; opacity: 0.8; margin-top: 4px;';
  statsDiv.innerHTML = `Years: ${war.startYear} - ${war.endYear === -1 ? 'Present' : war.endYear}<br/>Total Deaths: ${(war.deathCount ?? 0).toLocaleString()}`;
  container.appendChild(statsDiv);

  return container;
}

function createWarPopup(war: WarMapOverlayDto, router: Router): HTMLElement {
  const container = createWarPopupElement(war);
  wireInternalLinks(container, router);
  return container;
}

function createBattlePopupElement(battle: BattleMapMarkerDto): HTMLElement {
  const container = document.createElement('div');
  container.className = 'warfare-popup-item';

  const titleDiv = document.createElement('div');
  titleDiv.style.cssText = 'font-weight: 600; font-size: 14px; margin-bottom: 6px; display: flex; align-items: center; flex-wrap: wrap; gap: 4px;';

  if (battle.link) {
    appendRichContent(titleDiv, battle.link);
  } else {
    const color = battle.attackerColor && battle.attackerColor !== '#666666' ? battle.attackerColor : '#E53935';
    titleDiv.innerHTML = `<i class="mdi mdi-sword-cross" style="color: ${color}; margin-right: 4px; font-size: 16px;"></i><a href="/battle/${battle.id}">${battle.name}</a>`;
  }

  if (battle.isActive) {
    const badge = document.createElement('span');
    badge.style.cssText = 'background: #e53935; color: white; border-radius: 4px; padding: 2px 6px; font-size: 10px; margin-left: 6px;';
    badge.textContent = 'Ongoing';
    titleDiv.appendChild(badge);
  }
  container.appendChild(titleDiv);

  if (battle.warId || battle.warName || battle.warLink) {
    const warDiv = document.createElement('div');
    warDiv.style.cssText = 'font-size: 12px; margin-bottom: 4px; display: flex; align-items: center; flex-wrap: wrap;';
    const warLabel = document.createElement('strong');
    warLabel.textContent = 'War:\u00A0';
    warDiv.appendChild(warLabel);
    if (battle.warLink) {
      appendRichContent(warDiv, battle.warLink);
    } else if (battle.warId) {
      warDiv.innerHTML += `<i class="mdi mdi-sword-cross" style="margin-right: 2px;"></i><a href="/war/${battle.warId}">${battle.warName}</a>`;
    } else {
      warDiv.append(battle.warName || '');
    }
    container.appendChild(warDiv);
  }

  const attackerDiv = document.createElement('div');
  attackerDiv.style.cssText = 'font-size: 12px; margin-bottom: 4px; display: flex; align-items: center; flex-wrap: wrap;';
  const attackerLabel = document.createElement('strong');
  attackerLabel.textContent = 'Attacker:\u00A0';
  attackerDiv.appendChild(attackerLabel);
  if (battle.attackerLink) {
    appendRichContent(attackerDiv, battle.attackerLink);
  } else if (battle.attackerId) {
    appendRichContent(attackerDiv, formatEntityLinkWithBadge(battle.attackerId, battle.attackerName, battle.attackerColor));
  } else {
    attackerDiv.append(battle.attackerName || 'Unknown');
  }
  if (battle.attackerDeathCount != null) {
    attackerDiv.append(`\u00A0(${battle.attackerDeathCount.toLocaleString()} dead)`);
  }
  container.appendChild(attackerDiv);

  const defenderDiv = document.createElement('div');
  defenderDiv.style.cssText = 'font-size: 12px; margin-bottom: 4px; display: flex; align-items: center; flex-wrap: wrap;';
  const defenderLabel = document.createElement('strong');
  defenderLabel.textContent = 'Defender:\u00A0';
  defenderDiv.appendChild(defenderLabel);
  if (battle.defenderLink) {
    appendRichContent(defenderDiv, battle.defenderLink);
  } else if (battle.defenderId) {
    appendRichContent(defenderDiv, formatEntityLinkWithBadge(battle.defenderId, battle.defenderName, undefined));
  } else {
    defenderDiv.append(battle.defenderName || 'Unknown');
  }
  if (battle.defenderDeathCount != null) {
    defenderDiv.append(`\u00A0(${battle.defenderDeathCount.toLocaleString()} dead)`);
  }
  container.appendChild(defenderDiv);

  if (battle.victorName || battle.victorId || battle.victorLink) {
    const victorDiv = document.createElement('div');
    victorDiv.style.cssText = 'font-size: 12px; margin-bottom: 4px; display: flex; align-items: center; flex-wrap: wrap;';
    const victorLabel = document.createElement('strong');
    victorLabel.textContent = 'Victor:\u00A0';
    victorDiv.appendChild(victorLabel);
    if (battle.victorLink) {
      appendRichContent(victorDiv, battle.victorLink);
    } else if (battle.victorId) {
      appendRichContent(victorDiv, formatEntityLinkWithBadge(battle.victorId, battle.victorName, undefined));
    } else {
      victorDiv.append(battle.victorName || '');
    }
    container.appendChild(victorDiv);
  }

  const statsDiv = document.createElement('div');
  statsDiv.style.cssText = 'font-size: 11px; opacity: 0.8; margin-top: 4px;';
  statsDiv.innerHTML = `Year: ${battle.startYear ?? ''}<br/>Total Deaths: ${(battle.deathCount ?? 0).toLocaleString()}`;
  container.appendChild(statsDiv);

  return container;
}

function createBattlePopup(battle: BattleMapMarkerDto, router: Router): HTMLElement {
  const container = createBattlePopupElement(battle);
  wireInternalLinks(container, router);
  return container;
}

function renderWarOverlay(
  war: WarMapOverlayDto,
  targetLayer: LayerGroup,
  router: Router,
  toLatLng: (x: number, y: number) => L.LatLngTuple,
  onRegisterItem?: (item: MapTileItem) => void
): { points: L.LatLngTuple[]; mainMarker?: L.Marker } {
  const points: L.LatLngTuple[] = [];
  const popup = createWarPopup(war, router);
  let mainMarker: L.Marker | undefined = undefined;

  if (war.attackerCoordinates && war.defenderCoordinates) {
    const start = toLatLng(war.attackerCoordinates.x, war.attackerCoordinates.y);
    const end = toLatLng(war.defenderCoordinates.x, war.defenderCoordinates.y);
    points.push(start, end);

    const color = war.attackerColor && war.attackerColor !== '#666666' ? war.attackerColor : '#E53935';

    const polyline = L.polyline([start, end], {
      color: color,
      weight: 7,
      opacity: 0.9,
      dashArray: war.isActive ? undefined : '10, 10',
      pane: 'overlayPane',
    });

    polyline.bindPopup(popup);
    targetLayer.addLayer(polyline as any);

    // Calculate correct rotation angle for UP-pointing (North = 0 deg) SVG arrowhead
    // dLat is Y (North/South), dLng is X (East/West)
    const dLat = end[0] - start[0];
    const dLng = end[1] - start[1];
    const angleDeg = Math.atan2(dLng, dLat) * (180 / Math.PI);

    const createArrowheadIcon = () => L.divIcon({
      className: 'war-arrow-head',
      html: `
        <div style="transform: rotate(${angleDeg}deg); color: ${color}; width: 38px; height: 38px; display: flex; align-items: center; justify-content: center; filter: drop-shadow(0 2px 6px rgba(0,0,0,0.85));">
          <svg viewBox="0 0 24 24" width="34" height="34" fill="currentColor" stroke="#000000" stroke-width="1.2">
            <path d="M12 2L2 22l2 1L12 18l8 5 2-1z"/>
          </svg>
        </div>
      `,
      iconSize: [38, 38],
      iconAnchor: [19, 19],
    });

    // End arrowhead marker at destination (defender capital)
    const arrowMarker = L.marker(end, {
      icon: createArrowheadIcon(),
      pane: 'markerPane',
    });
    arrowMarker.bindPopup(popup);
    targetLayer.addLayer(arrowMarker as any);
    mainMarker = arrowMarker;

    if (onRegisterItem && war.defenderCoordinates && war.defenderCoordinates.x != null && war.defenderCoordinates.y != null) {
      onRegisterItem({
        type: 'war',
        x: war.defenderCoordinates.x,
        y: war.defenderCoordinates.y,
        layerGroup: targetLayer,
        marker: arrowMarker,
        data: war,
      });
    }

    // If distance is large enough, add a midpoint arrowhead along the line for clarity
    const dist = Math.hypot(dLat, dLng);
    if (dist > 80) {
      const midPoint: L.LatLngTuple = [(start[0] + end[0]) / 2, (start[1] + end[1]) / 2];
      const midArrowMarker = L.marker(midPoint, {
        icon: createArrowheadIcon(),
        pane: 'markerPane',
      });
      midArrowMarker.bindPopup(popup);
      targetLayer.addLayer(midArrowMarker as any);
    }
  } else if (war.defenderCoordinates || war.attackerCoordinates) {
    const coord = war.defenderCoordinates ?? war.attackerCoordinates!;
    const latlng = toLatLng(coord.x, coord.y);
    points.push(latlng);
    const color = war.attackerColor && war.attackerColor !== '#666666' ? war.attackerColor : '#E53935';
    const icon = L.divIcon({
      className: 'war-single-marker',
      html: `
        <div style="background: rgba(18, 18, 18, 0.95); border: 3px solid ${color}; border-radius: 50%; width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; box-shadow: 0 3px 8px rgba(0,0,0,0.85);">
          <i class="mdi mdi-sword-cross" style="font-size: 20px; color: ${color}; line-height: 1;"></i>
        </div>
      `,
      iconSize: [32, 32],
      iconAnchor: [16, 16],
    });
    const marker = L.marker(latlng, { icon, pane: 'markerPane' });
    marker.bindPopup(popup);
    targetLayer.addLayer(marker as any);
    mainMarker = marker;
  }

  return { points, mainMarker };
}

function renderBattleMarker(
  battle: BattleMapMarkerDto,
  targetLayer: LayerGroup,
  router: Router,
  toLatLng: (x: number, y: number) => L.LatLngTuple,
  onRegisterItem?: (item: MapTileItem) => void
): { latlng: L.LatLngTuple; marker: L.Marker } | null {
  if (!battle.coordinates) return null;

  const latlng = toLatLng(battle.coordinates.x, battle.coordinates.y);
  const color = battle.attackerColor && battle.attackerColor !== '#666666' ? battle.attackerColor : '#E53935';

  const icon = L.divIcon({
    className: 'battle-marker-icon',
    html: `
      <div style="background: rgba(18, 18, 18, 0.95); border: 3px solid ${color}; border-radius: 50%; width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; box-shadow: 0 3px 8px rgba(0,0,0,0.85);">
        <i class="mdi mdi-sword-cross" style="font-size: 20px; color: #ffffff; line-height: 1;"></i>
      </div>
    `,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
  });

  const marker = L.marker(latlng, {
    icon: icon,
    pane: 'markerPane',
  });

  const popup = createBattlePopup(battle, router);
  marker.bindPopup(popup);
  targetLayer.addLayer(marker as any);

  if (onRegisterItem && battle.coordinates && battle.coordinates.x != null && battle.coordinates.y != null) {
    onRegisterItem({
      type: 'battle',
      x: battle.coordinates.x,
      y: battle.coordinates.y,
      layerGroup: targetLayer,
      marker: marker,
      data: battle,
    });
  }

  return { latlng, marker };
}

interface SiteAtCoordinate {
  marker: SiteMarker;
  x: number;
  y: number;
}

interface MarkerConfig {
  shape: 'circle' | 'triangle' | 'square' | 'pentagon' | 'hexagon' | 'star';
  size?: number;
  color?: string;
}

const siteTypeMarkers: Record<SiteType, MarkerConfig> = {
  Unknown: { shape: 'circle' },

  // Dwarves
  Hillocks: { shape: 'square', size: 2 },
  Fortress: { shape: 'pentagon' },
  MountainHalls: { shape: 'hexagon', size: 4 },

  // Elves
  ForestRetreat: { shape: 'pentagon' },

  // Human
  Hamlet: { shape: 'square', size: 2 },
  Town: { shape: 'pentagon' },
  Castle: { shape: 'hexagon', size: 4 },

  // Goblins
  DarkPits: { shape: 'pentagon' },
  DarkFortress: { shape: 'hexagon' },

  // Main Civilizations
  Monastery: { shape: 'triangle' },
  Fort: { shape: 'triangle' },
  Tomb: { shape: 'triangle' },

  // Mysterious
  MysteriousLair: { shape: 'square', size: 2, color: '#AAAAFF' },
  MysteriousDungeon: { shape: 'pentagon', color: '#AAAAFF' },
  MysteriousPalace: { shape: 'hexagon', size: 4, color: '#AAAAFF' },

  // Nature (Kobolds often start there)
  Cave: { shape: 'circle' },

  // Monsters
  Lair: { shape: 'circle', size: 2 },

  // Demons
  Vault: { shape: 'star' },

  // Minotaur
  Labyrinth: { shape: 'star' },

  // Titan and Colossus
  Shrine: { shape: 'star' },

  // Necromancer
  Tower: { shape: 'star', size: 4 },

  // Others
  Camp: { shape: 'circle' },
  ImportantLocation: { shape: 'star' },
};

function createMarker(siteType: SiteType, siteColor: string | null | undefined, latlng: L.LatLngExpression, renderer: L.Renderer): L.Path {
  const config = siteTypeMarkers[siteType];
  const color = siteColor ?? config?.color ?? "#666"
  const size = config?.size ?? 3;
  const options = { pane: 'overlayPane', color: color, renderer };
  switch (config?.shape) {
    case 'circle':
      return L.circle(latlng, { ...options, radius: size });
    case 'triangle':
      return createPolygon(latlng, 3, size, color, renderer);
    case 'square':
      return createPolygon(latlng, 4, size, color, renderer);
    case 'pentagon':
      return createPolygon(latlng, 5, size, color, renderer);
    case 'hexagon':
      return createPolygon(latlng, 6, size, color, renderer);
    case 'star':
      return createStar(latlng, 5, size, size / 2, color, renderer);
    default:
      return L.circle(latlng, { ...options, radius: size / 2 });
  }
}

function createPolygon(center: L.LatLngExpression, sides: number, size: number, color: string, renderer: L.Renderer): L.Polygon {
  const vertices: L.LatLngExpression[] = [];
  for (let i = 0; i < sides; i++) {
    const angle = (i / sides) * 2 * Math.PI + Math.PI / 2; // Add 90 degrees (π/2 radians)
    const vertex: L.LatLngExpression = [
      (center as number[])[0] + size * Math.sin(angle),
      (center as number[])[1] + size * Math.cos(angle)
    ]
    vertices.push(vertex);
  }
  return L.polygon(vertices, { pane: 'overlayPane', color, renderer });
}

function createStar(center: L.LatLngExpression, points: number, outer: number, inner: number, color: string, renderer: L.Renderer): L.Polygon {
  const vertices: L.LatLngExpression[] = [];
  for (let i = 0; i < points * 2; i++) {
    const angle = (i / (points * 2)) * 2 * Math.PI + Math.PI / 2; // Add 90 degrees (π/2 radians)
    const radius = i % 2 === 0 ? outer : inner;
    const vertex: L.LatLngExpression = [
      (center as number[])[0] + radius * Math.sin(angle),
      (center as number[])[1] + radius * Math.cos(angle)
    ]
    vertices.push(vertex);
  }
  return L.polygon(vertices, { pane: 'overlayPane', color, renderer });
}

function coordinateKey(x: number, y: number): string {
  return `${x},${y}`;
}

function appendRichContent(container: HTMLElement, html: string | null | undefined): void {
  if (!html) return;

  const parsedDocument = new DOMParser().parseFromString(html, 'text/html');
  for (const child of [...parsedDocument.body.childNodes]) {
    container.appendChild(document.importNode(child, true));
  }
}

function wireInternalLinks(container: HTMLElement, router: Router): void {
  for (const link of container.querySelectorAll<HTMLAnchorElement>('a[href]')) {
    const href = link.getAttribute('href');
    if (!href?.startsWith('/')) continue;

    link.addEventListener('click', event => {
      event.preventDefault();
      void router.push(href);
    });
  }
}



function createUnifiedPickerPopup(items: MapTileItem[], router: Router): HTMLElement {
  if (items.length === 1) {
    const single = items[0];
    if (single.type === 'battle') {
      return createBattlePopup(single.data as BattleMapMarkerDto, router);
    }
    if (single.type === 'war') {
      return createWarPopup(single.data as WarMapOverlayDto, router);
    }
  }

  const container = document.createElement('div');
  container.className = 'site-picker';

  if (items.length > 1) {
    container.classList.add('site-picker--multiple');
    const heading = document.createElement('strong');
    heading.className = 'site-picker__heading';
    heading.textContent = `${items.length} items at this location`;
    container.appendChild(heading);
  }

  for (const item of items) {
    const itemEl = document.createElement('div');
    itemEl.className = 'site-picker__item';

    if (item.type === 'site') {
      const siteData = item.data as SiteAtCoordinate;
      const marker = siteData.marker;
      if (marker.name) appendRichContent(itemEl, marker.name);
      else itemEl.append('Unknown site');
      itemEl.append(document.createElement('br'));
      itemEl.append(marker.typeAsString ?? 'Unknown');
      itemEl.append(document.createElement('br'), document.createElement('br'));
      appendRichContent(itemEl, marker.owner ?? 'Others');
      if (marker.originOwnerId != null && marker.originOwnerId !== marker.ownerId) {
        itemEl.append(document.createElement('br'), 'Origin: ');
        appendRichContent(itemEl, marker.originOwner ?? marker.originOwnerText);
      }
    } else if (item.type === 'battle') {
      const battle = item.data as BattleMapMarkerDto;
      itemEl.appendChild(createBattlePopupElement(battle));
    } else if (item.type === 'war') {
      const war = item.data as WarMapOverlayDto;
      itemEl.appendChild(createWarPopupElement(war));
    }

    wireInternalLinks(itemEl, router);
    container.appendChild(itemEl);
  }

  return container;
}

function addCustomControl(
  map: L.Map,
  ownerLayers: Record<string, L.LayerGroup>,
  warfareLayerRef?: Ref<L.LayerGroup | null>,
  onToggleAll?: () => void
) {
  const customControl = L.Control.extend({
    options: {
      position: 'topright', // Position of the control on the map
    },
    onAdd: function () {
      const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control leaflet-control-custom');

      // Activate All button
      const activateButton = L.DomUtil.create('a', '', container);
      activateButton.innerHTML = 'All';
      activateButton.style.width = '40px';
      activateButton.style.cursor = 'pointer';
      activateButton.style.padding = '0px';
      activateButton.style.background = '#333';
      activateButton.style.color = '#fff';
      activateButton.style.border = '2px solid #ccc';

      // Deactivate All button
      const deactivateButton = L.DomUtil.create('a', '', container);
      deactivateButton.innerHTML = 'None';
      deactivateButton.style.width = '40px';
      deactivateButton.style.cursor = 'pointer';
      deactivateButton.style.padding = '0px';
      deactivateButton.style.background = '#333';
      deactivateButton.style.color = '#fff';
      deactivateButton.style.border = '2px solid #ccc';
      deactivateButton.style.marginTop = '5px';
      deactivateButton.style.marginRight = '0px';

      // Activate all layers
      L.DomEvent.on(activateButton, 'click', () => {
        for (const owner in ownerLayers) {
          map.addLayer(ownerLayers[owner]); // Add each owner's layer to the map
        }
        if (warfareLayerRef?.value) {
          map.addLayer(warfareLayerRef.value as any);
        }
        onToggleAll?.();
      });

      // Deactivate all layers
      L.DomEvent.on(deactivateButton, 'click', () => {
        for (const owner in ownerLayers) {
          map.removeLayer(ownerLayers[owner]); // Remove each owner's layer from the map
        }
        if (warfareLayerRef?.value) {
          map.removeLayer(warfareLayerRef.value as any);
        }
        onToggleAll?.();
      });

      return container;
    },
  });

  // Add the custom control to the map
  map.addControl(new customControl());
}

export default defineComponent({
  setup() {
    const worldStore = useWorldStore();
    const mapStore = useWorldMapStore();
    const router = useRouter();
    const route = useRoute();
    // Leaflet mutates deeply nested objects internally; Vue proxies are expensive
    // and can break Leaflet's identity-sensitive layer and map bookkeeping.
    const leafletMap = shallowRef<LeafletMap>();
    const currentOverlay = shallowRef<ImageOverlay | null>(null);
    const siteCountLayer = shallowRef<LayerGroup | null>(null);
    const highlightLayer = shallowRef<LayerGroup | null>(null);
    const siteMarkersMap = new Map<number, { marker: L.Layer; popup: HTMLElement }>();
    const siteColorMode = ref<'current' | 'origin'>('current');
    const mapError = ref('');
    const mapProgress = ref('');
    const ownerLayerNotice = ref('');
    const siteLayers: Array<{ marker: L.Path; site: SiteMarker }> = [];
    const siteRenderer = L.canvas({ padding: 0.3 });
    let renderGeneration = 0;
    let recolorGeneration = 0;
    let isRenderingSites = false;
    let disposed = false;
    let warfareLoaded = false;
    let warfareLoading = false;

    const yieldToBrowser = () => new Promise<void>(resolve => setTimeout(resolve, 0));

    const colorForSite = (site: SiteMarker): string => {
      if (siteColorMode.value === 'origin') {
        return site.originColor || '#666';
      }
      return siteTypeMarkers[site.type ?? 'Unknown']?.color ?? site.color ?? '#666';
    };

    watch(siteColorMode, () => {
      const generation = ++recolorGeneration;
      void (async () => {
        for (let i = 0; i < siteLayers.length; i++) {
          if (disposed || generation !== recolorGeneration) return;
          const { marker, site } = siteLayers[i];
          marker.setStyle({ color: colorForSite(site) });
          if (i % 250 === 249) await yieldToBrowser();
        }
      })();
    });

    // This will hold the LayerGroups for different owners
    const ownerLayers: Record<string, LayerGroup> = {};
    const controlLayers = shallowRef<L.Control.Layers>();

    const warfareLayer = shallowRef<LayerGroup | null>(null);

    const tileItemsMap = new Map<string, MapTileItem[]>();

    const getActiveItemsAtTile = (x: number, y: number): MapTileItem[] => {
      if (!leafletMap.value) return [];
      const key = coordinateKey(x, y);
      const items = tileItemsMap.get(key) ?? [];
      return items.filter(item => leafletMap.value!.hasLayer(item.layerGroup));
    };

    const bindTileMarkerPopup = (marker: L.Layer, x: number, y: number) => {
      if ('bindPopup' in marker && typeof (marker as any).bindPopup === 'function') {
        (marker as any).bindPopup(() => {
          const activeItems = getActiveItemsAtTile(x, y);
          return createUnifiedPickerPopup(activeItems, router);
        }, { minWidth: 220 });
      }
    };

    const registerTileItem = (item: MapTileItem) => {
      const key = coordinateKey(item.x, item.y);
      const items = tileItemsMap.get(key) ?? [];
      items.push(item);
      tileItemsMap.set(key, items);
      bindTileMarkerPopup(item.marker, item.x, item.y);
    };

    const unregisterWarfareItems = () => {
      for (const [key, items] of tileItemsMap.entries()) {
        const filtered = items.filter(i => i.type === 'site');
        if (filtered.length > 0) {
          tileItemsMap.set(key, filtered);
        } else {
          tileItemsMap.delete(key);
        }
      }
    };

    const syncTileBadges = () => {
      if (!leafletMap.value || isRenderingSites) return;

      const map = leafletMap.value;
      if (!siteCountLayer.value) {
        siteCountLayer.value = new L.LayerGroup();
      }
      const countLayer = siteCountLayer.value;
      countLayer.clearLayers();

      const isMaxZoom = map.getZoom() === map.getMaxZoom();
      const scale = 8;
      const height = worldStore.world.height ?? 0;

      if (isMaxZoom) {
        for (const items of tileItemsMap.values()) {
          const activeItems = items.filter(item => map.hasLayer(item.layerGroup));

          if (activeItems.length > 1) {
            const { x, y } = activeItems[0];
            const countBadge = L.marker(
              [(height - y) * scale - 0.5 * scale, x * scale + 0.5 * scale],
              {
                icon: L.divIcon({
                  className: 'site-count-marker',
                  html: `<span>${activeItems.length}</span>`,
                  iconSize: [12, 12],
                  iconAnchor: [6, 6],
                }),
                interactive: false,
                keyboard: false,
                zIndexOffset: 1000,
              }
            );
            countLayer.addLayer(countBadge);
          }
        }

        if (!map.hasLayer(countLayer as any)) {
          map.addLayer(countLayer as any);
        }
      } else {
        if (map.hasLayer(countLayer as any)) {
          map.removeLayer(countLayer as any);
        }
      }
    };

    let resizeObserver: ResizeObserver | null = null;

    const initMap = async () => {
      if (!leafletMap.value) {
        leafletMap.value = L.map('map', {
          crs: L.CRS.Simple,
          zoom: 0,
          minZoom: -2,
          maxZoom: 2,
          preferCanvas: true
        });
        leafletMap.value.on('zoomend', syncTileBadges);
        leafletMap.value.on('overlayadd', event => {
          syncTileBadges();
          if ('layer' in event && event.layer === warfareLayer.value) void loadWarfareOverlay();
        });
        leafletMap.value.on('overlayremove', syncTileBadges);

        const mapContainerEl = document.getElementById('map');
        if (mapContainerEl && typeof window !== 'undefined' && 'ResizeObserver' in window) {
          resizeObserver = new ResizeObserver(() => {
            leafletMap.value?.invalidateSize();
          });
          resizeObserver.observe(mapContainerEl);
        }
      }

      try {
        mapProgress.value = 'Loading world data...';
        await worldStore.loadWorld();
        if (disposed) return;

        const width = worldStore.world?.width ?? 0;
        const height = worldStore.world?.height ?? 0;
        if (!width || !height) {
          mapError.value = 'World dimensions are unavailable. Try reloading the world.';
          return;
        }

        // The large map uses 10 px per world tile. Limit image dimensions
        // to avoid allocating hundreds of megabytes for large DF worlds.
        const useLargeImage = width * height * 100 <= 16_000_000;
        mapProgress.value = 'Loading map image...';
        await mapStore.loadWorldMap(useLargeImage ? 'Large' : 'Default');
        if (disposed) return;

        const image = useLargeImage ? mapStore.worldMapMax : mapStore.worldMapMid;
        if (image) {
          await loadImageToMap(image);
        } else {
          mapError.value = 'Unable to load the map image. Check the server console for details.';
        }
      } catch (error) {
        console.error('Unable to initialize world map:', error);
        mapError.value = error instanceof Error ? error.message : String(error);
      } finally {
        if (!disposed) {
          if (leafletMap.value) {
            // Add the all/none control only after map initialization.
            addCustomControl(leafletMap.value, ownerLayers, warfareLayer as any, syncTileBadges);
            leafletMap.value.invalidateSize();
          }
          if (!isRenderingSites) mapProgress.value = '';
        }
      }
    };

    const toLatLng = (x: number, y: number): L.LatLngTuple => {
      const scale = 8;
      const height = worldStore.world?.height ?? 0;
      return [(height - y) * scale - 0.5 * scale, x * scale + 0.5 * scale];
    };

    const handleObjectFocus = async (type: string, id: number) => {
      if (!leafletMap.value) return;

      if (highlightLayer.value) {
        leafletMap.value.removeLayer(highlightLayer.value as unknown as Layer);
      }
      highlightLayer.value = new L.LayerGroup().addTo(leafletMap.value);

      const siteMarkers = worldStore.world.siteMarkers ?? [];

      const addPulseCircle = (x: number, y: number, radius = 18) => {
        const latlng = toLatLng(x, y);
        const circle = L.circle(latlng, {
          pane: 'overlayPane',
          color: '#ffcc00',
          fillColor: '#ff3300',
          fillOpacity: 0.4,
          radius: radius,
          weight: 4,
          interactive: false,
          className: 'target-highlight-pulse',
        });
        highlightLayer.value?.addLayer(circle);
      };

      const lowerType = type.toLowerCase();

      // 1. SITE
      if (lowerType === 'site') {
        const site = siteMarkers.find(s => s.id === id);
        if (site && site.coordinates?.length) {
          const { x, y } = site.coordinates[0];
          if (x != null && y != null) {
            const latlng = toLatLng(x, y);
            leafletMap.value.setView(latlng, 1);
            addPulseCircle(x, y, 16);

            const siteEntry = siteMarkersMap.get(id);
            if (siteEntry) {
              siteEntry.marker.openPopup();
            }
            return;
          }
        }
        await fetchAndFocusCoordinates(type, id);
      }
      // 2. ENTITY (Civilization / Faction / Group owning sites)
      else if (lowerType === 'entity') {
        // Find sites owned by entity ID (using ownerId or currentOwnerId)
        // @ts-ignore
        const matchingSites = siteMarkers.filter(s => s.ownerId === id || s.currentOwnerId === id);

        if (matchingSites.length > 0) {
          const points: L.LatLngTuple[] = [];

          // Make sure matching owner layers are active on the map
          for (const site of matchingSites) {
            const ownerText = site.ownerText ?? 'Unknown';
            if (ownerLayers[ownerText] && !leafletMap.value.hasLayer(ownerLayers[ownerText])) {
              leafletMap.value.addLayer(ownerLayers[ownerText]);
            }

            site.coordinates?.forEach(coord => {
              if (coord.x != null && coord.y != null) {
                points.push(toLatLng(coord.x, coord.y));
                addPulseCircle(coord.x, coord.y, 16);
              }
            });
          }

          if (points.length === 1) {
            leafletMap.value.setView(points[0], 1);
            const firstSite = matchingSites[0];
            const siteEntry = firstSite.id ? siteMarkersMap.get(firstSite.id) : undefined;
            if (siteEntry) {
              siteEntry.marker.openPopup();
            }
          } else if (points.length > 1) {
            const bounds = L.latLngBounds(points);
            leafletMap.value.fitBounds(bounds, { padding: [80, 80] });
          }
        } else {
          // Entity has no current sites in siteMarkers list -> fetch center/coords from API
          await fetchAndFocusCoordinates(type, id);
        }
      }
      // 3. WAR
      else if (lowerType === 'war') {
        if (!warfareLayer.value) {
          warfareLayer.value = new L.LayerGroup();
        }
        if (!leafletMap.value.hasLayer(warfareLayer.value as any)) {
          leafletMap.value.addLayer(warfareLayer.value as any);
        }

        const warfareDto: WarfareMapDto | null = await mapStore.loadWarOverlay(id);
        if (warfareDto && warfareDto.wars && warfareDto.wars.length > 0) {
          const warData = warfareDto.wars[0];
          const warRes = renderWarOverlay(warData, warfareLayer.value as any, router, toLatLng, registerTileItem);
          const points: L.LatLngTuple[] = [...warRes.points];

          if (warData.attackerCoordinates) {
            addPulseCircle(warData.attackerCoordinates.x, warData.attackerCoordinates.y, 20);
          }
          if (warData.defenderCoordinates) {
            addPulseCircle(warData.defenderCoordinates.x, warData.defenderCoordinates.y, 20);
          }

          if (warfareDto.battles && warfareDto.battles.length > 0) {
            for (const bData of warfareDto.battles) {
              const res = renderBattleMarker(bData, warfareLayer.value as any, router, toLatLng, registerTileItem);
              if (res) {
                points.push(res.latlng);
                if (bData.coordinates) {
                  addPulseCircle(bData.coordinates.x, bData.coordinates.y, 16);
                }
              }
            }
          }

          syncTileBadges();

          if (points.length === 1) {
            leafletMap.value.setView(points[0], 1);
          } else if (points.length > 1) {
            const bounds = L.latLngBounds(points);
            leafletMap.value.fitBounds(bounds, { padding: [80, 80] });
          }

          if (warRes.mainMarker) {
            warRes.mainMarker.openPopup();
          }
        }
      }
      // 4. BATTLE
      else if (lowerType === 'battle') {
        if (!warfareLayer.value) {
          warfareLayer.value = new L.LayerGroup();
        }
        if (!leafletMap.value.hasLayer(warfareLayer.value as any)) {
          leafletMap.value.addLayer(warfareLayer.value as any);
        }

        const warfareDto: WarfareMapDto | null = await mapStore.loadBattleMarker(id);
        if (warfareDto && warfareDto.battles && warfareDto.battles.length > 0) {
          const battleData = warfareDto.battles[0];
          const points: L.LatLngTuple[] = [];

          if (warfareDto.wars && warfareDto.wars.length > 0) {
            const warData = warfareDto.wars[0];
            const warRes = renderWarOverlay(warData, warfareLayer.value as any, router, toLatLng, registerTileItem);
            points.push(...warRes.points);
          }

          const res = renderBattleMarker(battleData, warfareLayer.value as any, router, toLatLng, registerTileItem);
          if (res) {
            points.push(res.latlng);
            if (battleData.coordinates) {
              addPulseCircle(battleData.coordinates.x, battleData.coordinates.y, 22);
            }
            leafletMap.value.setView(res.latlng, 1);
            res.marker.openPopup();
          } else if (points.length > 0) {
            if (points.length === 1) {
              leafletMap.value.setView(points[0], 1);
            } else {
              const bounds = L.latLngBounds(points);
              leafletMap.value.fitBounds(bounds, { padding: [80, 80] });
            }
          }

          syncTileBadges();
        }
      }
      // 5. OTHER OBJECT TYPES (Region, Landmass, River, Construction, Structure, MountainPeak, Artifact, etc.)
      else {
        await fetchAndFocusCoordinates(type, id);
      }
    };

    const fetchAndFocusCoordinates = async (type: string, id: number) => {
      if (!leafletMap.value) return;

      const scale = 8;
      const addPulseCircle = (x: number, y: number, radius = 18) => {
        const latlng = toLatLng(x, y);
        const circle = L.circle(latlng, {
          pane: 'overlayPane',
          color: '#ffcc00',
          fillColor: '#ff3300',
          fillOpacity: 0.4,
          radius: radius,
          weight: 4,
          interactive: false,
          className: 'target-highlight-pulse',
        });
        highlightLayer.value?.addLayer(circle);
      };

      try {
        const response = await fetch(`http://localhost:15421/api/WorldMap/coordinates/${type}/${id}`);
        if (!response.ok) return;
        const data = await response.json();

        if (data && data.coordinates && data.coordinates.length > 0) {
          const { minX, maxX, minY, maxY, centerX, centerY } = data;
          if (minX != null && maxX != null && minY != null && maxY != null) {
            if (minX === maxX && minY === maxY) {
              const latlng = toLatLng(centerX, centerY);
              leafletMap.value.setView(latlng, 1);
            } else {
              const southWest = toLatLng(minX, maxY);
              const northEast = toLatLng(maxX, minY);
              const bounds = L.latLngBounds(southWest, northEast);
              leafletMap.value.fitBounds(bounds, { padding: [80, 80] });
            }
          } else if (centerX != null && centerY != null) {
            const latlng = toLatLng(centerX, centerY);
            leafletMap.value.setView(latlng, 1);
          }

          if (centerX != null && centerY != null) {
            const radius = (maxX != null && minX != null) ? Math.max(16, (maxX - minX + 1) * scale * 0.6) : 18;
            addPulseCircle(centerX, centerY, isNaN(radius) ? 18 : radius);
          }
        }
      } catch (err) {
        console.error('Failed to fetch object coordinates for map focus:', err);
      }
    };

    const loadWarfareOverlay = async () => {
      if (warfareLoaded || warfareLoading || disposed || !warfareLayer.value) return;
      warfareLoading = true;
      const generation = renderGeneration;
      try {
        const data: WarfareMapDto | null = await mapStore.loadWarfareMap(true);
        if (!data || disposed || renderGeneration !== generation || !warfareLayer.value) return;

        unregisterWarfareItems();
        warfareLayer.value.clearLayers();

        let processed = 0;
        for (const war of data.wars ?? []) {
          if (disposed || renderGeneration !== generation || !warfareLayer.value) return;
          renderWarOverlay(war, warfareLayer.value, router, toLatLng, registerTileItem);
          if (++processed % 150 === 0) await yieldToBrowser();
        }
        for (const battle of data.battles ?? []) {
          if (disposed || renderGeneration !== generation || !warfareLayer.value) return;
          renderBattleMarker(battle, warfareLayer.value, router, toLatLng, registerTileItem);
          if (++processed % 150 === 0) await yieldToBrowser();
        }

        warfareLoaded = true;
        syncTileBadges();
      } catch (error) {
        console.error('Failed to render the warfare overlay:', error);
      } finally {
        warfareLoading = false;
      }
    };

    const loadImageToMap = async (base64Image: string) => {
      if (!leafletMap.value) return;
      const width = worldStore.world?.width;
      const height = worldStore.world?.height;
      if (!width || !height || !base64Image) return;

      const generation = ++renderGeneration;
      isRenderingSites = true;
      mapProgress.value = 'Preparing map markers...';
      ownerLayerNotice.value = '';
      mapError.value = '';
      try {
        if (currentOverlay.value) {
          leafletMap.value.removeLayer(currentOverlay.value as unknown as Layer);
          currentOverlay.value = null;
        }
        if (siteCountLayer.value) {
          leafletMap.value.removeLayer(siteCountLayer.value as unknown as Layer);
          siteCountLayer.value = null;
        }
        if (warfareLayer.value && leafletMap.value.hasLayer(warfareLayer.value)) {
          leafletMap.value.removeLayer(warfareLayer.value);
        }
        warfareLayer.value?.clearLayers();
        warfareLoaded = false;

        for (const key in ownerLayers) {
          if (leafletMap.value.hasLayer(ownerLayers[key])) {
            leafletMap.value.removeLayer(ownerLayers[key]);
          }
          ownerLayers[key].clearLayers();
          delete ownerLayers[key];
        }
        if (controlLayers.value) {
          leafletMap.value.removeControl(controlLayers.value);
          controlLayers.value = undefined;
        }

        siteMarkersMap.clear();
        siteLayers.length = 0;
        tileItemsMap.clear();

        const scale = 8;
        const bounds: L.LatLngBoundsExpression = [[0, 0], [scale * height, scale * width]];
        const imageOverlay = L.imageOverlay(base64Image, bounds, {
          pane: 'tilePane',
          interactive: false,
        });

        imageOverlay.addTo(leafletMap.value);
        currentOverlay.value = imageOverlay;
        leafletMap.value.setMaxBounds(bounds);

        // Show the background immediately while markers are built incrementally.
        const queryType = route.query.type as string | undefined;
        const queryId = route.query.id ? parseInt(route.query.id as string, 10) : undefined;
        if (!queryType || !queryId || isNaN(queryId)) {
          leafletMap.value.fitBounds(bounds);
        }

        const layersControl: Record<string, LayerGroup> = {};
        warfareLayer.value = new L.LayerGroup();

        // Skip Vue's deep proxies while iterating over potentially tens of
        // thousands of exported sites and polygon coordinates.
        const markers = toRaw(worldStore.world.siteMarkers ?? []);
        let created = 0;
        for (const siteMarker of markers) {
          if (disposed || generation !== renderGeneration || !leafletMap.value) return;
          if (!siteMarker.coordinates || !siteMarker.owner) continue;

          const ownerText = siteMarker.ownerText ?? 'Unknown';
          if (!ownerLayers[ownerText]) {
            ownerLayers[ownerText] = new L.LayerGroup();
            layersControl[siteMarker.owner] = ownerLayers[ownerText];
            ownerLayers[ownerText].addTo(leafletMap.value);
          }

          for (const coordinate of siteMarker.coordinates) {
            if (coordinate.x == null || coordinate.y == null) continue;
            const marker = createMarker(
              siteMarker.type ?? 'Unknown',
              colorForSite(siteMarker),
              toLatLng(coordinate.x, coordinate.y),
              siteRenderer
            );
            siteLayers.push({ marker, site: siteMarker });

            registerTileItem({
              type: 'site',
              x: coordinate.x,
              y: coordinate.y,
              layerGroup: ownerLayers[ownerText],
              marker,
              data: { marker: siteMarker, x: coordinate.x, y: coordinate.y },
            });
            ownerLayers[ownerText].addLayer(marker);

            if (siteMarker.id != null) {
              siteMarkersMap.set(siteMarker.id, { marker, popup: null as any });
            }

            if (++created % 150 === 0) {
              mapProgress.value = `Rendering map sites: ${created.toLocaleString()} markers`;
              await yieldToBrowser();
              if (disposed || generation !== renderGeneration) return;
            }
          }
        }

        layersControl['Wars & Battles'] = warfareLayer.value;
        const layerEntries = Object.entries(layersControl);
        // Leaflet's standard control creates all labels synchronously. On
        // large worlds, thousands of labels can freeze the browser even
        // though the map markers themselves were rendered in batches.
        const maxListedOwners = 250;
        const visibleLayers: Record<string, LayerGroup> = {};
        for (const [name, layer] of layerEntries.slice(0, maxListedOwners)) {
          visibleLayers[name] = layer;
        }
        visibleLayers['Wars & Battles'] = warfareLayer.value;
        if (layerEntries.length > maxListedOwners + 1) {
          ownerLayerNotice.value =
            `Showing the first ${maxListedOwners} owner toggles. All/None still controls every site.`;
        }
        controlLayers.value = L.control.layers(undefined, visibleLayers).addTo(leafletMap.value);

        // The warfare layer starts unchecked. Load its data only when a user
        // enables it rather than making a potentially huge request on every map open.
        if (queryType && queryId && !isNaN(queryId)) {
          void handleObjectFocus(queryType, queryId);
        }
      } catch (error) {
        console.error('Failed to render world map markers:', error);
        mapError.value = error instanceof Error ? error.message : String(error);
      } finally {
        if (generation === renderGeneration) {
          isRenderingSites = false;
          mapProgress.value = '';
          syncTileBadges();
        }
      }
    };

    // The initial Promise.all loads the map once. Watching both asynchronous
    // responses used to build every marker twice when the view first opened.

    watch(() => [route.query.type, route.query.id], ([newType, newId]) => {
      if (newType && newId) {
        const typeStr = newType as string;
        const idNum = parseInt(newId as string, 10);
        if (!isNaN(idNum)) {
          void handleObjectFocus(typeStr, idNum);
        }
      }
    });

    onMounted(() => {
      initMap();
    });

    onBeforeUnmount(() => {
      disposed = true;
      renderGeneration++;
      recolorGeneration++;
      if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
      }
      if (leafletMap.value) {
        leafletMap.value.remove();
      }
    });

    return {
      mapStore,
      siteColorMode,
      mapError,
      mapProgress,
      ownerLayerNotice,
    };
  },
});
</script>

<style>
.map-container {
  height: 880px;
  width: 100%;
  position: relative;
  /* Ensure the map and controls are positioned properly */
}

.map-owner-notice {
  position: absolute;
  top: 140px;
  left: 10px;
  z-index: 1000;
  padding: 8px 12px;
  max-width: 320px;
  background: rgba(30, 30, 30, 0.85);
  color: white;
  border-radius: 4px;
  pointer-events: none;
}

.map-progress {
  position: absolute;
  bottom: 20px;
  left: 20px;
  z-index: 1100;
  padding: 8px 12px;
  background: rgba(30, 30, 30, 0.85);
  color: white;
  border-radius: 4px;
  pointer-events: none;
}

.map-error {
  position: absolute;
  top: 12px;
  left: 60px;
  z-index: 1100;
  padding: 10px 14px;
  color: #ffffff;
  background: #a12626;
  border-radius: 4px;
}

.site-color-selector {
  position: absolute;
  top: 84px;
  left: 10px;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 8px;
  border-radius: 4px;
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  box-shadow: 0 1px 5px rgba(0, 0, 0, 0.4);
  font-size: 12px;
}

.site-color-selector select {
  background: rgb(var(--v-theme-background));
  color: inherit;
  border: 1px solid currentColor;
  border-radius: 3px;
  padding: 4px;
}

.leaflet-image-layer {
  max-width: none !important;
  max-height: none !important;
  image-rendering: pixelated;
  image-rendering: crisp-edges;
}

.leaflet-control-layers,
.leaflet-container {
  background: rgb(var(--v-theme-background));
  color: rgb(var(--v-theme-foreground));
}

.leaflet-control-zoom-in,
.leaflet-control-zoom-out,
.leaflet-control-attribution,
.leaflet-popup {
  filter: invert(100%) hue-rotate(180deg) brightness(95%) contrast(90%);
}

.leaflet-control-layers-overlays label {
  margin-top: 4px;
}

.site-picker__heading {
  display: block;
  margin-bottom: 10px;
}

.site-picker--multiple .site-picker__item {
  padding: 8px 0;
}

.site-picker--multiple .site-picker__item + .site-picker__item {
  border-top: 1px solid rgba(127, 127, 127, 0.35);
}

.site-count-marker {
  pointer-events: none;
}

.site-count-marker span {
  display: flex;
  width: 12px;
  height: 12px;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(255, 255, 255, 0.8);
  border-radius: 50%;
  background: rgba(25, 25, 25, 0.72);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.45);
  color: rgba(255, 255, 255, 0.9);
  font-size: 8px;
  font-weight: 600;
  line-height: 1;
}

@keyframes map-target-pulse {
  0% {
    stroke-width: 3px;
    stroke-opacity: 1;
    fill-opacity: 0.5;
  }
  50% {
    stroke-width: 7px;
    stroke-opacity: 0.7;
    fill-opacity: 0.2;
  }
  100% {
    stroke-width: 3px;
    stroke-opacity: 1;
    fill-opacity: 0.5;
  }
}

.target-highlight-pulse {
  animation: map-target-pulse 1.5s infinite ease-in-out;
}
</style>


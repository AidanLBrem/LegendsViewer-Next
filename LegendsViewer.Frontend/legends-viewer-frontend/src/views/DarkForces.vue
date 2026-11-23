<template>
  <v-container fluid>
    <h1 class="text-h4 mb-3">Dark Forces</h1>
    <p class="mb-3">
      The rise, expeditions, invasions and decline of recorded necromancers.
      Each chronology distinguishes a necromancer's own actions from those of
      factions they founded or ruled.
    </p>
    <v-alert type="info" variant="tonal" class="mb-4">
      "Greatest territorial extent" means the maximum number of distinct settlements
      held at the same time, reconstructed from dated site ownership. It is
      <strong>not</strong> land area. The power index is an evidence-based ranking
      (10 × peak settlements + 8 × captures + 3 × invasions + up to 100 recorded kills),
      not a Dwarf Fortress combat statistic. Recorded deaths are distinct from
      violent defeat; territorial collapse requires evidence of lost holdings.
    </v-alert>

    <template v-if="!viewingDetail">
      <v-row>
        <v-col cols="12" sm="6" md="4">
          <v-text-field v-model="search" label="Search necromancers or factions" clearable density="comfortable" />
        </v-col>
        <v-col cols="12" sm="6" md="3">
          <v-select v-model="status" :items="statusOptions" label="Status" density="comfortable" />
        </v-col>
        <v-col cols="12" sm="6" md="5">
          <v-select v-model="sortOrder" :items="sortOptions" label="Rank or sort by" density="comfortable" />
        </v-col>
      </v-row>

      <v-progress-linear v-if="loading" indeterminate aria-label="Loading necromancers" />
      <v-alert v-if="error" type="error" class="mb-3">
        {{ error }}
        <v-btn size="small" variant="text" @click="retry++">Retry</v-btn>
      </v-alert>

      <v-row v-if="pageData" class="mb-3">
        <v-col cols="12" sm="4"><v-card variant="tonal"><v-card-text>
          <div class="text-h5">{{ pageData.totalCount.toLocaleString() }}</div>
          <div class="text-caption">Recorded necromancers</div>
        </v-card-text></v-card></v-col>
        <v-col cols="12" sm="4"><v-card variant="tonal"><v-card-text>
          <div class="text-h5">{{ pageData.fallenCount.toLocaleString() }}</div>
          <div class="text-caption">Killed or with lost realms</div>
        </v-card-text></v-card></v-col>
        <v-col cols="12" sm="4"><v-card variant="tonal"><v-card-text>
          <div class="text-h5">{{ pageData.maximumTerritory.toLocaleString() }}</div>
          <div class="text-caption">Largest peak settlement holding</div>
        </v-card-text></v-card></v-col>
      </v-row>
      <p v-if="pageData" class="text-caption mb-3">
        Showing {{ pageData.totalFilteredCount.toLocaleString() }} matching necromancers.
        The counts reflect recorded evidence, not total supernatural power.
      </p>
      <v-alert v-if="!loading && pageData?.totalCount === 0" type="info">
        No necromancers were identified in the loaded world. Necromancy is detected
        from recorded dark secrets and necromancer creature types.
      </v-alert>
      <v-alert v-else-if="!loading && pageData?.totalFilteredCount === 0" type="info">
        No necromancers match the selected filters.
      </v-alert>

      <v-table v-if="pageData?.items.length" density="comfortable">
        <thead>
          <tr>
            <th>Necromancer</th>
            <th>Status</th>
            <th class="text-end">Power index</th>
            <th class="text-end">Peak settlements</th>
            <th class="text-end">Held now</th>
            <th class="text-end">Invasions</th>
            <th class="text-end">Captures</th>
            <th class="text-end">Kills</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="figure in pageData.items" :key="figure.figureId">
            <td>
              <router-link :to="`/dark-forces/${figure.figureId}`">{{ figure.name }}</router-link>
              <div class="text-caption" v-if="figure.firstInvasionYear !== null">
                First recorded invasion: year {{ figure.firstInvasionYear }}
              </div>
            </td>
            <td><v-chip size="small" :color="figure.defeated ? 'secondary' : undefined">{{ figure.status }}</v-chip></td>
            <td class="text-end">{{ figure.powerIndex.toLocaleString() }}</td>
            <td class="text-end">{{ figure.peakSettlements }}<span class="text-caption" v-if="figure.peakYear !== null"> (year {{ figure.peakYear }})</span></td>
            <td class="text-end">{{ figure.currentSettlements }}</td>
            <td class="text-end">{{ figure.invasions }}</td>
            <td class="text-end">{{ figure.conquests }}</td>
            <td class="text-end">{{ figure.notableKills }}</td>
          </tr>
        </tbody>
      </v-table>
      <v-pagination
        v-if="pageData && pageData.totalFilteredCount > pageSize"
        v-model="page" :length="Math.ceil(pageData.totalFilteredCount / pageSize)"
        :total-visible="7" class="mt-3" />
    </template>

    <template v-else>
      <v-btn to="/dark-forces" variant="text" prepend-icon="mdi-arrow-left" class="mb-3">All dark forces</v-btn>
      <v-progress-linear v-if="detailLoading" indeterminate aria-label="Loading necromancer chronicle" />
      <v-alert v-if="detailError" type="error" class="mb-3">
        {{ detailError }} <v-btn size="small" @click="retry++">Retry</v-btn>
      </v-alert>
      <template v-if="selected">
        <v-card variant="outlined" class="mb-4">
          <v-card-title class="text-wrap">
            <router-link :to="`/hf/${selected.figureId}`">{{ selected.name }}</router-link>
          </v-card-title>
          <v-card-subtitle>{{ selected.status }} · {{ selected.factions.length }} recorded leadership / foundation links</v-card-subtitle>
          <v-card-text>
            <v-row>
              <v-col cols="6" md="3"><strong>{{ selected.powerIndex }}</strong><div class="text-caption">Evidence-based power index</div></v-col>
              <v-col cols="6" md="3"><strong>{{ selected.peakSettlements }}</strong><div class="text-caption">Peak settlements ({{ selected.peakYear === null ? 'date unknown' : 'year ' + selected.peakYear }})</div></v-col>
              <v-col cols="6" md="3"><strong>{{ selected.invasions }}</strong><div class="text-caption">Invasions</div></v-col>
              <v-col cols="6" md="3"><strong>{{ selected.conquests }}</strong><div class="text-caption">Settlements captured</div></v-col>
            </v-row>
            <p class="mt-3">
              Dark secret acquired: {{ selected.awakeningYear === null ? 'not dated in export' : 'year ' + selected.awakeningYear }}.
              First documented invasion: {{ selected.firstInvasionYear === null ? 'none recorded' : 'year ' + selected.firstInvasionYear }}.
            </p>
            <p v-if="selected.defeatYear !== null" class="text-caption">
              Death recorded in year {{ selected.defeatYear }}. This does not by itself establish a military defeat.
            </p>
          </v-card-text>
        </v-card>

        <v-card title="Faction Foundations and Leadership" prepend-icon="mdi-castle" variant="text" class="mb-4">
          <v-card-text>
            <p class="text-caption mb-2">
              Territory is attributed only during recorded leadership or a founded faction's lifetime
              while its founding necromancer survived. Ordinary membership does not establish command.
            </p>
            <v-list v-if="selected.factions.length">
              <v-list-item v-for="(faction, index) in selected.factions" :key="`${faction.id}-${index}`">
                <v-list-item-title><router-link :to="`/entity/${faction.id}`">{{ faction.name }}</router-link></v-list-item-title>
                <v-list-item-subtitle>
                  {{ faction.evidence }} · Years {{ faction.fromYear }}–{{ faction.throughYear ?? 'present' }}
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
            <p v-else>No founded or led factions can be identified from the available records.</p>
          </v-card-text>
        </v-card>

        <v-card title="Chronicle: From Necromancy to Invasion" prepend-icon="mdi-timeline-clock-outline" variant="text">
          <v-card-text>
            <p class="text-caption mb-3">
              Events before the first invasion are marked as prelude. Chronology does not prove
              that studying, settling, or founding a faction caused an attack.
            </p>
            <v-timeline v-if="selected.timeline.length" side="end" density="compact" truncate-line="both">
              <v-timeline-item
                v-for="(event, index) in selected.timeline" :key="`${event.year}-${index}`"
                size="small" :dot-color="event.isInvasion ? 'error' : (event.isPrelude ? 'info' : 'secondary')">
                <div class="text-caption">Year {{ event.year }} · {{ event.attribution }}</div>
                <div class="text-subtitle-2">
                  {{ event.kind }}
                  <v-chip v-if="event.isPrelude" size="x-small" variant="outlined">Prelude</v-chip>
                  <v-chip v-if="event.isInvasion" size="x-small" color="error" variant="tonal">Invasion</v-chip>
                </div>
                <div class="text-body-2" v-html="event.description"></div>
                <div class="text-caption mt-1">
                  <router-link v-if="event.siteId !== null" :to="`/site/${event.siteId}`">
                    {{ event.siteName || 'Site' }}
                  </router-link>
                  <template v-if="event.targetEntityId !== null">
                    <span v-if="event.siteId !== null"> · </span>
                    <router-link :to="`/entity/${event.targetEntityId}`">
                      {{ event.targetEntityName || 'Target faction' }}
                    </router-link>
                  </template>
                </div>
              </v-timeline-item>
            </v-timeline>
            <p v-else>No personal or attributable faction events appear in the loaded history.</p>
            <v-alert v-if="selected.recordedEvents > selected.timeline.length" type="info" variant="tonal">
              Displaying the first {{ selected.timeline.length }} of {{ selected.recordedEvents }} classified chronicle events.
            </v-alert>
          </v-card-text>
        </v-card>
      </template>
    </template>
  </v-container>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { API_BASE_URL } from '../apiClient';
import { useWorldStore } from '../stores/worldStore';
import type { DarkForce, DarkForcesPage } from '../types/darkForces';

const route = useRoute();
const world = useWorldStore();
// Vue Router returns an empty string for an omitted optional path parameter.
// An empty ID must show the listing, not trigger a request to the collection API.
const detailFigureId = computed(() => {
  const raw = route.params.figureId;
  if (typeof raw !== 'string' || !/^\d+$/.test(raw)) return null;
  const id = Number(raw);
  return Number.isSafeInteger(id) ? id : null;
});
const viewingDetail = computed(() => detailFigureId.value !== null);

function isDarkForce(value: unknown): value is DarkForce {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const figure = value as Partial<DarkForce>;
  return typeof figure.figureId === 'number'
    && typeof figure.name === 'string'
    && typeof figure.status === 'string'
    && typeof figure.powerIndex === 'number'
    && Array.isArray(figure.factions)
    && Array.isArray(figure.timeline);
}
const search = ref<string | null>('');
const status = ref('all');
const sortOrder = ref('power-desc');
const page = ref(1), retry = ref(0);
const pageSize = 25;
const pageData = ref<DarkForcesPage | null>(null);
const selected = ref<DarkForce | null>(null);
const loading = ref(false), detailLoading = ref(false);
const error = ref(''), detailError = ref('');
const statusOptions = [
  { title: 'All necromancers', value: 'all' },
  { title: 'Fallen / defeated', value: 'fallen' },
  { title: 'Not defeated', value: 'surviving' },
  { title: 'Deceased', value: 'deceased' },
  { title: 'Lost their realm', value: 'realm-lost' },
];
const sortOptions = [
  { title: 'Most powerful (evidence index)', value: 'power-desc' },
  { title: 'Least powerful', value: 'power-asc' },
  { title: 'Greatest territorial extent', value: 'territory-desc' },
  { title: 'Smallest territorial extent', value: 'territory-asc' },
  { title: 'Most invasions', value: 'invasions-desc' },
  { title: 'Fallen / defeated first', value: 'fallen-desc' },
  { title: 'Name', value: 'name-asc' },
];

watch([search, status, sortOrder], () => { page.value = 1; });
watch(() => world.world, () => { pageData.value = null; selected.value = null; page.value = 1; });
watch([search, status, sortOrder, page, retry, () => world.world, viewingDetail], async (_, __, cleanup) => {
  const controller = new AbortController(); cleanup(() => controller.abort());
  if (viewingDetail.value) return;
  loading.value = true; error.value = '';
  const [sort, direction] = sortOrder.value.split('-');
  const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize), sort, direction });
  if (search.value) params.set('search', search.value);
  if (status.value !== 'all') params.set('status', status.value);
  try {
    const response = await fetch(`${API_BASE_URL}api/dark-forces?${params}`, { signal: controller.signal });
    if (!response.ok) throw new Error(`Could not load necromancers (${response.status})`);
    pageData.value = await response.json();
  } catch (e) {
    if (!controller.signal.aborted) { error.value = e instanceof Error ? e.message : 'Could not load necromancers'; pageData.value = null; }
  } finally { if (!controller.signal.aborted) loading.value = false; }
}, { immediate: true });

watch([detailFigureId, () => world.world, retry], async (_, __, cleanup) => {
  const controller = new AbortController(); cleanup(() => controller.abort());
  selected.value = null; detailError.value = '';
  const id = detailFigureId.value;
  if (id === null) { detailLoading.value = false; return; }
  detailLoading.value = true;
  try {
    const response = await fetch(`${API_BASE_URL}api/dark-forces/${id}`, { signal: controller.signal });
    if (!response.ok) throw new Error(response.status === 404 ? 'Necromancer not found.' : `Could not load chronicle (${response.status})`);
    const payload: unknown = await response.json();
    if (!isDarkForce(payload) || payload.figureId !== id) {
      throw new Error('Unexpected necromancer response. Check the Dark Forces API.');
    }
    selected.value = payload;
  } catch (e) {
    if (!controller.signal.aborted) detailError.value = e instanceof Error ? e.message : 'Could not load chronicle';
  } finally { if (!controller.signal.aborted) detailLoading.value = false; }
}, { immediate: true });
</script>

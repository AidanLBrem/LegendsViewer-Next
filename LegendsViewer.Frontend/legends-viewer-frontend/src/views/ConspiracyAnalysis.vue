<template>
  <v-container fluid>
    <h1 class="text-h4 mb-3">Conspiracy Analysis</h1>
    <p class="mb-4">Explore recorded objectives, actors, targets and strategies. Plot records do not establish that an objective succeeded or caused a later event.</p>
    <template v-if="!route.params.figureId">
    <v-row>
      <v-col cols="12" md="5"><v-text-field v-model="search" label="Search objectives, figures, roles or strategies" clearable /></v-col>
      <v-col cols="12" md="4"><v-select v-model="civilizationId" :items="data?.civilizations ?? []" item-title="name" item-value="id" label="Civilization" clearable /></v-col>
      <v-col cols="12" md="3"><v-select v-model="status" :items="statuses" label="Recorded status" clearable /></v-col>
    </v-row>
    <p class="text-caption mb-3">Civilizations include plot entities and recorded actor affiliations, including former affiliations. This filter does not imply civilization sponsorship.</p>
    <v-row class="mb-2">
      <v-col cols="12" sm="6" md="4">
        <v-select v-model="sortOrder" :items="sortOptions" label="Sort conspiracies" density="compact" hide-details />
      </v-col>
    </v-row>
    <p class="text-caption mb-2">Actor counts include distinct recorded actor IDs, including targets; not all actors are willing conspirators.</p>
    <v-alert v-if="error" type="error" class="mb-3">{{ error }} <v-btn size="small" @click="retry++">Retry</v-btn></v-alert>
    <v-progress-linear v-if="loading" indeterminate aria-label="Loading plots" />
    <p v-if="data" class="my-3">{{ data.totalFilteredCount.toLocaleString() }} matching plots · {{ data.totalCount.toLocaleString() }} recorded in this world</p>
    <v-alert v-if="!loading && data?.totalCount === 0" type="info">No intrigue plots were found in the loaded exports. Load the standard Legends XML; Legends Plus alone may not contain these records.</v-alert>
    <v-alert v-else-if="!loading && data?.totalFilteredCount === 0" type="info">No plots match these filters.</v-alert>
    <v-table v-if="data?.items.length">
      <thead><tr><th>Objective / plot</th><th>Recorded under</th><th>Entity</th><th>Status</th><th>Actors</th></tr></thead>
      <tbody><tr v-for="plot in data.items" :key="plot.key">
        <td><router-link :to="`/conspiracies/${plot.owner.id}/${plot.localId}`">{{ plot.objective }}</router-link><div class="text-caption">{{ plot.key }}</div></td>
        <td><IntrigueLink :reference="plot.owner" /></td><td><IntrigueLink :reference="plot.entity" /></td>
        <td>{{ plot.status }}</td><td>{{ plot.actorCount }}</td>
      </tr></tbody>
    </v-table>
    <v-pagination v-if="data && data.totalFilteredCount > 25" v-model="page" :length="Math.ceil(data.totalFilteredCount / 25)" :total-visible="7" />
    </template>
    <v-btn v-else to="/conspiracies" variant="text" prepend-icon="mdi-arrow-left" class="mb-3">Back to plots</v-btn>

    <v-progress-linear v-if="detailLoading" indeterminate aria-label="Loading plot details" />
    <v-alert v-if="detailError" type="error">{{ detailError }} <v-btn size="small" @click="retry++">Retry</v-btn></v-alert>
    <v-card v-if="selected" class="mt-6" variant="outlined">
      <v-card-title class="text-wrap">{{ selected.objective }} · {{ selected.key }}</v-card-title>
      <v-card-text>
        <p class="mb-2">Recorded under <IntrigueLink :reference="selected.owner" /> · {{ selected.status }} · {{ selected.actorCount }} distinct recorded actors</p>
        <p class="mb-2">Associated entity: <IntrigueLink :reference="selected.entity" /></p>
        <p v-if="selected.artifact" class="mb-2">{{ selected.objective === 'acquire artifact' ? 'Target artifact' : 'Referenced artifact' }}: <IntrigueLink :reference="selected.artifact" /></p>
        <p class="text-caption mb-3">“Not marked on hold” is not proof of an active or successful plot. Actor IDs are local to the figure who holds the record.</p>
        <template v-for="related in relatedPlots" :key="related.label">
          <p v-if="related.plot" class="mb-2">{{ related.label }}:
            <router-link v-if="related.plot.resolved" :to="`/conspiracies/${related.plot.figureId}/${related.plot.localId}`">{{ related.plot.figureId }}:{{ related.plot.localId }}</router-link>
            <span v-else>{{ related.plot.figureId }}:{{ related.plot.localId }} (unresolved)</span>
          </p>
        </template>
        <ConspiracyNetwork :plot="selected" />
        <h2 class="text-h6 mt-5 mb-3">Conspirators, referenced actors and targets</h2>
        <p v-if="!selected.participants.length">No actors are explicitly attached to this plot.</p>
        <v-card v-for="(actor, index) in selected.participants" :key="`${actor.actorId}-${index}`" variant="tonal" class="mb-3">
          <v-card-text>
            <h3 class="text-subtitle-1"><IntrigueLink v-if="actor.figure" :reference="actor.figure" /><span v-else>Unresolved actor {{ actor.actorId }}</span> · {{ actor.role }}</h3>
            <p>Local actor {{ actor.actorId }} · Relationship role: {{ actor.actorRole || 'Not recorded' }} · Strategy: {{ actor.strategy || 'Not recorded' }}</p>
            <p v-if="actor.entity">Entity: <IntrigueLink :reference="actor.entity" /></p>
            <p v-if="actor.strategyEntity">Strategy entity: <IntrigueLink :reference="actor.strategyEntity" /> <span v-if="actor.strategyPositionId >= 0">· Position {{ actor.strategyPositionId }}</span></p>
            <p v-if="actor.handleActorId >= 0">Handler actor: {{ actor.handleActorId }} (local ID)</p>
            <p v-if="actor.agreementId >= 0">Agreement {{ actor.agreementId }}{{ actor.agreementHasMessenger ? ' · Has messenger' : '' }}</p>
            <p v-if="actor.promisedActorImmortality">Record holder promised this actor immortality.</p>
            <p v-if="actor.promisedMeImmortality">This actor promised the record holder immortality.</p>
            <details class="mt-2"><summary>Exported actor fields</summary><IntrigueFields :fields="actor.fields" /></details>
          </v-card-text>
        </v-card>
        <details class="mt-4"><summary>Exported plot fields</summary><IntrigueFields :fields="selected.fields" /></details>
      </v-card-text>
    </v-card>
  </v-container>
</template>
<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { API_BASE_URL } from '../apiClient';
import { useWorldStore } from '../stores/worldStore';
import type { Conspiracy, ConspiracyPage } from '../types/conspiracy';
import IntrigueLink from '../components/IntrigueLink.vue';
import IntrigueFields from '../components/IntrigueFields.vue';
import ConspiracyNetwork from '../components/ConspiracyNetwork.vue';
const route = useRoute();
const world = useWorldStore();
const search = ref<string | null>('');
const civilizationId = ref<number | null>(null);
const status = ref<string | null>(null);
const sortOrder = ref<'recorded' | 'actors-desc' | 'actors-asc'>('recorded');
const sortOptions = [
  { title: 'Recorded order', value: 'recorded' },
  { title: 'Number of actors (most first)', value: 'actors-desc' },
  { title: 'Number of actors (fewest first)', value: 'actors-asc' },
];
const page = ref(1), retry = ref(0);
const data = ref<ConspiracyPage | null>(null), selected = ref<Conspiracy | null>(null);
const loading = ref(false), detailLoading = ref(false), error = ref(''), detailError = ref('');
const statuses = [{ title: 'On hold', value: 'on-hold' }, { title: 'Not marked on hold', value: 'not-on-hold' }];
const relatedPlots = computed(() => [{ label: 'Parent plot', plot: selected.value?.parent }, { label: 'Delegated plot', plot: selected.value?.delegated }]);
watch([search, civilizationId, status, sortOrder], () => { page.value = 1; });
watch(() => world.world, () => { data.value = null; civilizationId.value = null; page.value = 1; });
watch([search, civilizationId, status, sortOrder, page, retry, () => world.world], async (_, __, cleanup) => {
  const controller = new AbortController(); cleanup(() => controller.abort());
  loading.value = true; error.value = '';
  const query = new URLSearchParams({ page: String(page.value), pageSize: '25' });
  if (search.value) query.set('search', search.value);
  if (civilizationId.value !== null) query.set('civilizationId', String(civilizationId.value));
  if (status.value) query.set('status', status.value);
  if (sortOrder.value !== 'recorded') {
    query.set('sort', 'actor-count');
    query.set('direction', sortOrder.value === 'actors-asc' ? 'asc' : 'desc');
  }
  try {
    const response = await fetch(`${API_BASE_URL}api/conspiracies?${query}`, { signal: controller.signal });
    if (!response.ok) throw new Error(`Could not load plots (${response.status}).`);
    data.value = await response.json();
  } catch (e) { if (!controller.signal.aborted) { error.value = e instanceof Error ? e.message : 'Could not load plots.'; data.value = null; } }
  finally { if (!controller.signal.aborted) loading.value = false; }
}, { immediate: true });
watch([() => route.params.figureId, () => route.params.localId, () => world.world, retry], async (_, __, cleanup) => {
  const controller = new AbortController(); cleanup(() => controller.abort());
  selected.value = null; detailError.value = ''; detailLoading.value = false;
  if (route.params.figureId === undefined || !route.params.localId) return;
  detailLoading.value = true;
  try {
    const response = await fetch(`${API_BASE_URL}api/conspiracies/${encodeURIComponent(String(route.params.figureId))}/${encodeURIComponent(String(route.params.localId))}`, { signal: controller.signal });
    if (!response.ok) throw new Error(response.status === 404 ? 'This plot was not found in the loaded world.' : `Could not load plot (${response.status}).`);
    selected.value = await response.json();
  } catch (e) { if (!controller.signal.aborted) detailError.value = e instanceof Error ? e.message : 'Could not load plot.'; }
  finally { if (!controller.signal.aborted) detailLoading.value = false; }
}, { immediate: true });
</script>

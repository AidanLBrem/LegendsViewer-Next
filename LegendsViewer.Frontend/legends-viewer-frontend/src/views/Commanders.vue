<template>
  <v-container fluid>
    <h1 class="text-h4 mb-3">Commanders</h1>
    <p class="mb-4">Win % uses battles with a recorded winner. Enemies killed counts deaths on the opposing side in battles the figure commanded.</p>
    <v-alert v-if="error" type="error">
      {{ error }} <v-btn variant="text" @click="retry++">Retry</v-btn>
    </v-alert>
    <v-data-table v-else :headers="headers" :items="commanders" :loading="loading"
      :sort-by="[{ key: 'victories', order: 'desc' }]" :items-per-page="25"
      no-data-text="No battle commanders were recorded in this world.">
      <template #item.name="{ item }">
        <router-link :to="`/hf/${item.id}`">{{ item.name || `Historical figure ${item.id}` }}</router-link>
      </template>
      <template #item.winPercentage="{ item }">
        {{ item.winPercentage == null ? '—' : item.winPercentage.toLocaleString(undefined, { maximumFractionDigits: 1 }) + '%' }}
      </template>
      <template #item.enemiesKilled="{ item }">{{ item.enemiesKilled.toLocaleString() }}</template>
    </v-data-table>
  </v-container>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { API_BASE_URL } from '../apiClient';
import { useWorldStore } from '../stores/worldStore';

interface Commander {
  id: number;
  name: string;
  victories: number;
  losses: number;
  winPercentage: number | null;
  enemiesKilled: number;
}

const headers = [
  { title: 'Commander', key: 'name' },
  { title: 'Victories', key: 'victories' },
  { title: 'Losses', key: 'losses' },
  { title: 'Win %', key: 'winPercentage' },
  { title: 'Enemies killed', key: 'enemiesKilled' },
];
const world = useWorldStore();
const commanders = ref<Commander[]>([]);
const loading = ref(false);
const error = ref('');
const retry = ref(0);

watch([() => world.world, retry], async (_, __, cleanup) => {
  const controller = new AbortController();
  cleanup(() => controller.abort());
  commanders.value = [];
  loading.value = true;
  error.value = '';
  try {
    const response = await fetch(`${API_BASE_URL}api/commanders`, { signal: controller.signal });
    if (!response.ok) throw new Error(`Could not load commanders (${response.status}).`);
    const rows = await response.json();
    if (!controller.signal.aborted) commanders.value = rows;
  } catch (e) {
    if (!controller.signal.aborted) error.value = e instanceof Error ? e.message : 'Could not load commanders.';
  } finally {
    if (!controller.signal.aborted) loading.value = false;
  }
}, { immediate: true });
</script>

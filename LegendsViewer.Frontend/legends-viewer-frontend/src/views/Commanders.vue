<template>
  <v-container fluid>
    <h1 class="text-h4 mb-3">Commanders</h1>
    <p class="mb-2">Matchups show wins / battles by native-rating-weighted starting strength when available, otherwise estimated adult body-size strength, with dwarf-equivalent placeholders for unknown creatures. Outnumbered and favored mean at least a 2-to-1 disadvantage or advantage. Near-peer covers the battles in between.</p>
    <details class="mb-4">
      <summary>How the command score and K/D are calculated</summary>
      <p>Each battle earns its result (1 for a win, 0 for a loss) minus its expected strength share (own effective strength / total effective strength). Multiply by the square root of total starting troops / 100. A 400-troop battle carries twice the weight of a 100-troop battle.</p>
      <p>Command score = 100 × sum of weighted results above expectation / (sum of battle weights + 3) × rated battles / (rated battles + 10). The extra 3 carries the weight of three 100-troop battles. The experience factor further discounts short records regardless of battle size: three rated battles retain 23% of the base score, ten retain 50%, and thirty retain 75%.</p>
      <p>The score uses battles with a recorded winner and positive starting counts on both sides. Native adult caste ratings are a proxy, not a simulated victory probability. If native ratings are incomplete, both sides use estimated adult body size / 50 (minimum 1) with a rating of 1,200 per unknown combatant. The bundled vanilla raw snapshot may differ from your game or mods. Animated troops use their underlying race's adult body size, without an undead-specific modifier; this is labeled separately. Transformed figures use their recorded race's adult body size when known; transformation effects are not modeled. Unknown combatants receive a 1,200 placeholder, explicitly listed with rating coverage. Win % includes all decided battles. K/D divides total enemy deaths by own-side deaths across commanded battles. ∞ means kills with zero own deaths, and — means neither side recorded a death.</p>
    </details>
    <v-alert v-if="error" type="error">
      {{ error }} <v-btn variant="text" @click="retry++">Retry</v-btn>
    </v-alert>
    <v-data-table v-else class="commander-table" show-expand :item-value="(item: Commander) => String(item.id)" v-model:expanded="expanded" fixed-header height="min(64vh, 640px)" :headers="headers" :items="commanders" :loading="loading"
      :sort-by="[{ key: 'victories', order: 'desc' }]" :items-per-page="25"
      no-data-text="No battle commanders were recorded in this world.">
      <template #item.data-table-expand="{ item, internalItem, isExpanded, toggleExpand }">
        <v-btn :icon="isExpanded(internalItem) ? 'mdi-chevron-up' : 'mdi-chevron-down'"
          size="small" variant="text" :aria-expanded="isExpanded(internalItem)"
          :aria-label="`Battles commanded by ${item.name || 'historical figure ' + item.id}`"
          @click="toggleExpand(internalItem)" />
      </template>
      <template #expanded-row="{ item, columns }">
        <tr><td :colspan="columns.length" class="py-2">
          <table v-if="item.battles.length" class="battle-list">
            <caption>Battles commanded by {{ item.name || 'historical figure ' + item.id }}</caption>
            <thead><tr>
              <th scope="col">Battle</th><th scope="col">Side</th><th scope="col">Result</th>
              <th scope="col">Troops deployed</th><th scope="col">Troops faced</th>
              <th scope="col">Own strength</th><th scope="col">Enemy strength</th>
              <th scope="col">Strength ratio (own:enemy)</th><th scope="col">Basis</th>
              <th scope="col">Enemy killed</th><th scope="col">Own killed</th>
            </tr></thead>
            <tbody><tr v-for="battle in item.battles" :key="battle.id">
              <td><router-link :to="`/battle/${battle.id}`">{{ battle.name || `Battle #${battle.id}` }}</router-link>
                <div v-if="battle.year >= 0" class="text-caption">Year {{ battle.year }}</div></td>
              <td>{{ battle.side }}</td><td>{{ battle.outcome }}</td>
              <td>{{ battle.ownTroops.toLocaleString() }}<div class="text-caption">{{ battle.ownComposition }}</div><div v-for="p in battle.ownStrengthEstimate?.placeholders" :key="p.race" class="text-caption" title="Unknown rating: 1,200 per unit">* {{ p.count }} × {{ p.race }} (placeholder)</div></td>
              <td>{{ battle.enemyTroops.toLocaleString() }}<div class="text-caption">{{ battle.enemyComposition }}</div><div v-for="p in battle.enemyStrengthEstimate?.placeholders" :key="p.race" class="text-caption" title="Unknown rating: 1,200 per unit">* {{ p.count }} × {{ p.race }} (placeholder)</div></td>
              <td>{{ (battle.ownEffectiveStrength ?? battle.ownTroops).toLocaleString() }}<div class="text-caption">{{ battle.ownStrengthEstimate?.coverage?.toFixed(1) ?? '—' }}% covered</div></td>
              <td>{{ (battle.enemyEffectiveStrength ?? battle.enemyTroops).toLocaleString() }}<div class="text-caption">{{ battle.enemyStrengthEstimate?.coverage?.toFixed(1) ?? '—' }}% covered</div></td>
              <td>{{ strengthRatio(battle) }}</td>
              <td>{{ battle.oddsBasis.includes('placeholders') ? ('Body size + placeholders' + (battle.oddsBasis.includes('undead') ? ' (undead)' : '')) : battle.oddsBasis === 'native' ? 'Native' : battle.oddsBasis === 'body-size-undead' ? 'Body size (undead modifiers unknown)' : battle.oddsBasis === 'body-size' ? 'Body size' : 'Numerical fallback' }}</td>
              <td>{{ battle.ownKills.toLocaleString() }}</td><td>{{ battle.enemyKills.toLocaleString() }}</td>
            </tr></tbody>
          </table>
          <p v-else>No linked battles with a recorded command side.</p>
        </td></tr>
      </template>
      <template #item.name="{ item }">
        <router-link :to="`/hf/${item.id}`">{{ item.name || `Historical figure ${item.id}` }}</router-link>
      </template>
      <template #item.winPercentage="{ item }">
        {{ item.winPercentage == null ? '—' : item.winPercentage.toLocaleString(undefined, { maximumFractionDigits: 1 }) + '%' }}
      </template>
      <template #item.enemiesKilled="{ item }">{{ item.enemiesKilled.toLocaleString() }}</template>
      <template #item.commandScore="{ item }">
        {{ item.commandScore?.toLocaleString(undefined, { maximumFractionDigits: 1, signDisplay: 'exceptZero' }) ?? '—' }}
      </template>
      <template #item.killDeathRatio="{ item }">
        {{ item.ownDeaths ? (item.enemiesKilled / item.ownDeaths).toFixed(2) : item.enemiesKilled ? '∞' : '—' }}
      </template>
      <template v-for="band in bands" :key="band.key" #[`item.${band.key}`]="{ item }">
        {{ item[band.key].wins }} / {{ item[band.key].battles }}
      </template>
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
  ownDeaths: number;
  commandScore: number | null;
  ratedBattles: number;
  battles: CommandedBattle[];
  outnumbered: Matchup;
  nearPeer: Matchup;
  favored: Matchup;
}

interface CommandedBattle {
  id: number;
  name: string;
  year: number;
  side: string;
  outcome: string;
  ownTroops: number;
  enemyTroops: number;
  ownKills: number;
  enemyKills: number;
  oddsBasis: string;
  ownStrengthEstimate?: StrengthEstimate;
  enemyStrengthEstimate?: StrengthEstimate;
  ownEffectiveStrength: number | null;
  enemyEffectiveStrength: number | null;
  ownComposition: string;
  enemyComposition: string;
}

interface StrengthEstimate {
  coverage: number | null;
  placeholders: { race: string; count: number }[];
}

function strengthRatio(battle: CommandedBattle) {
  const own = battle.ownEffectiveStrength ?? battle.ownTroops;
  const enemy = battle.enemyEffectiveStrength ?? battle.enemyTroops;
  return enemy > 0 ? (own / enemy).toLocaleString(undefined, { maximumFractionDigits: 3, minimumSignificantDigits: 1 }) + ' : 1' : '—';
}

interface Matchup {
  battles: number;
  wins: number;
}

const bands = [
  { key: 'outnumbered', title: 'Outnumbered' },
  { key: 'nearPeer', title: 'Near-peer' },
  { key: 'favored', title: 'Favored' },
] as const;

const headers = [
  { title: '', key: 'data-table-expand', width: 48, sortable: false },
  { title: 'Commander', key: 'name', width: 216 },
  { title: 'Command score', key: 'commandScore', width: 128 },
  { title: 'Rated battles', key: 'ratedBattles', width: 104 },
  { title: 'Victories', key: 'victories', width: 96 },
  { title: 'Losses', key: 'losses', width: 84 },
  { title: 'Win %', key: 'winPercentage', width: 88 },
  { title: 'Enemies killed', key: 'enemiesKilled', width: 112 },
  { title: 'Own deaths', key: 'ownDeaths', width: 104 },
  { title: 'K/D', key: 'killDeathRatio', width: 90,
    value: (row: Commander) => row.ownDeaths ? row.enemiesKilled / row.ownDeaths : row.enemiesKilled ? Infinity : -1,
    sort: (a: number, b: number) => a === b ? 0 : a < b ? -1 : 1 },
  ...bands.map(band => ({ ...band, width: 128, value: (row: Commander) => row[band.key].battles })),
];
const expanded = ref<string[]>([]);
const world = useWorldStore();
const commanders = ref<Commander[]>([]);
const loading = ref(false);
const error = ref('');
const retry = ref(0);

watch([() => world.world, retry], async (_, __, cleanup) => {
  const controller = new AbortController();
  cleanup(() => controller.abort());
  commanders.value = [];
  expanded.value = [];
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
<style scoped>
.commander-table :deep(.v-table__wrapper > table) {
  table-layout: fixed;
  width: 1454px;
}
.commander-table :deep(td) {
  overflow-wrap: anywhere;
}
.battle-list {
  width: 100%;
  table-layout: fixed;
  border-collapse: collapse;
}
.battle-list caption, .battle-list th, .battle-list td {
  padding: 6px 10px;
  text-align: left;
}
.battle-list thead th,
.battle-list tbody tr:not(:last-child) td {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.28);
}
.battle-list caption { font-weight: 600; }
.battle-list th:first-child { width: 17%; }
.battle-list th:nth-child(4), .battle-list th:nth-child(5) { width: 14%; }
</style>

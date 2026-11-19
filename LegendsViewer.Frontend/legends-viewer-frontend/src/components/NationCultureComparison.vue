<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import {
    cultureDimensionDescriptions, cultureWindows,
    type NationCulturePeriod, type NationCultureSummary
} from '../types/nationCulture';

type RateKey = 'settlementExpansionPerCentury' | 'conquestPerCentury' | 'sitesLostInWarPerCentury' | 'offensiveOperationsPerCentury' |
    'plundersPerCentury' | 'diplomacyPerCentury' | 'upheavalsPerCentury';
type SortKey = RateKey | 'name' | 'currentSites';
type CultureRow = NationCultureSummary & { period: NationCulturePeriod };

const nations = ref<NationCultureSummary[]>([]);
const loading = ref(false);
const error = ref('');
const search = ref('');
const windowYears = ref(100);
const sortKey = ref<SortKey>('settlementExpansionPerCentury');
const descending = ref(true);

const rateColumns: { key: RateKey; title: string }[] = [
    { key: 'settlementExpansionPerCentury', title: 'Expansion' },
    { key: 'conquestPerCentury', title: 'Conquest' },
    { key: 'sitesLostInWarPerCentury', title: 'Settlements Lost in War' },
    { key: 'offensiveOperationsPerCentury', title: 'Offense' },
    { key: 'plundersPerCentury', title: 'Plunder' },
    { key: 'diplomacyPerCentury', title: 'Diplomacy' },
    { key: 'upheavalsPerCentury', title: 'Upheaval' },
];
const sort = (key: SortKey) => {
    if (sortKey.value === key) descending.value = !descending.value;
    else {
        sortKey.value = key;
        descending.value = key !== 'name';
    }
};
const sortedNations = computed<CultureRow[]>(() => nations.value
    .filter(n => n.name.toLowerCase().includes(search.value.toLowerCase()) ||
        n.race.toLowerCase().includes(search.value.toLowerCase()))
    .map(n => ({ ...n, period: n.periods.find(p => p.windowYears === windowYears.value) ?? n.periods[0] }))
    .filter((n): n is CultureRow => Boolean(n.period))
    .sort((a, b) => {
        let comparison = 0;
        if (sortKey.value === 'name') comparison = a.name.localeCompare(b.name);
        else if (sortKey.value === 'currentSites') comparison = a.currentSites - b.currentSites;
        else comparison = a.period[sortKey.value] - b.period[sortKey.value];
        return (descending.value ? -1 : 1) * (comparison || a.name.localeCompare(b.name));
    }));
const formatRate = (value: number) => value.toLocaleString(undefined, { maximumFractionDigits: 2 });

onMounted(async () => {
    loading.value = true;
    try {
        const response = await fetch('http://localhost:15421/api/Entity/cultures');
        if (!response.ok) throw new Error('HTTP ' + response.status);
        nations.value = await response.json() as NationCultureSummary[];
    } catch (e) {
        error.value = 'Could not load nation cultures: ' + String(e);
    } finally {
        loading.value = false;
    }
});
</script>

<template>
    <v-col cols="12">
        <v-card title="Civilization Culture Comparison" subtitle="Compare observed historical behavior across all civilizations" variant="text">
            <template #prepend><v-icon class="mr-2" icon="mdi-scale-balance" size="32px" /></template>
            <v-card-text>
                <v-row dense class="mb-2">
                    <v-col cols="12" sm="5" md="4">
                        <v-text-field v-model="search" label="Search civilization or race" prepend-inner-icon="mdi-magnify"
                            density="compact" variant="outlined" hide-details />
                    </v-col>
                    <v-col cols="12" sm="4" md="3">
                        <v-select v-model="windowYears" :items="cultureWindows" label="Historical period"
                            density="compact" variant="outlined" hide-details />
                    </v-col>
                    <v-col cols="12" sm="3" md="5" class="d-flex align-center justify-end">
                        <span class="text-caption text-medium-emphasis">{{ sortedNations.length }} civilizations</span>
                    </v-col>
                </v-row>
                <div class="text-caption text-medium-emphasis mb-3">
                    Rates are recorded actions per 100 observed years, not official cultural values.
                    Click a header to sort all civilizations. Expansion = founded settlements; Conquest = captured sites; Settlements Lost in War = confirmed wartime captures, surrenders or destruction. Reclamations and ordinary abandonment are excluded.
                </div>
                <v-progress-linear v-if="loading" color="primary" indeterminate />
                <v-alert v-else-if="error" type="warning" variant="tonal" :text="error" />
                <div v-else class="culture-table-scroll">
                    <v-table density="compact" fixed-header height="500px">
                        <thead>
                            <tr>
                                <th class="sortable-header" @click="sort('name')">
                                    Civilization <v-icon v-if="sortKey === 'name'" size="x-small">{{ descending ? 'mdi-arrow-down' : 'mdi-arrow-up' }}</v-icon>
                                </th>
                                <th>Character</th>
                                <th class="text-end sortable-header" @click="sort('currentSites')">
                                    Sites <v-icon v-if="sortKey === 'currentSites'" size="x-small">{{ descending ? 'mdi-arrow-down' : 'mdi-arrow-up' }}</v-icon>
                                </th>
                                <th v-for="column in rateColumns" :key="column.key" class="text-end sortable-header"
                                    @click="sort(column.key)">
                                    <v-tooltip :text="cultureDimensionDescriptions[column.key]" location="top">
                                        <template #activator="{ props: tooltipProps }">
                                            <span v-bind="tooltipProps">{{ column.title }}</span>
                                        </template>
                                    </v-tooltip>
                                    <v-icon v-if="sortKey === column.key" size="x-small">
                                        {{ descending ? 'mdi-arrow-down' : 'mdi-arrow-up' }}
                                    </v-icon>
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="nation in sortedNations" :key="nation.id">
                                <td>
                                    <router-link :to="'/entity/' + nation.id">{{ nation.name }}</router-link>
                                    <div class="text-caption text-medium-emphasis">{{ nation.race }}</div>
                                </td>
                                <td>{{ nation.period.character }}</td>
                                <td class="text-end">{{ nation.currentSites.toLocaleString() }}</td>
                                <td v-for="column in rateColumns" :key="column.key" class="text-end culture-number">
                                    <v-tooltip v-if="column.key === 'sitesLostInWarPerCentury'"
                                        :text="nation.period.sitesLostInWar.toLocaleString() + ' recorded wartime settlement losses in this period'">
                                        <template #activator="{ props: tooltipProps }">
                                            <span v-bind="tooltipProps">{{ formatRate(nation.period[column.key]) }}</span>
                                        </template>
                                    </v-tooltip>
                                    <span v-else>{{ formatRate(nation.period[column.key]) }}</span>
                                </td>
                            </tr>
                            <tr v-if="sortedNations.length === 0">
                                <td colspan="10" class="text-medium-emphasis">No matching civilizations.</td>
                            </tr>
                        </tbody>
                    </v-table>
                </div>
                <div class="text-caption text-medium-emphasis mt-2">
                    A value of zero means no matching event was recorded, not that the civilization was peaceful.
                    See each nation for supporting events and the years used in its rates.
                </div>
            </v-card-text>
        </v-card>
    </v-col>
</template>

<style scoped>
.culture-table-scroll { overflow-x: auto; }
.culture-table-scroll :deep(table) { min-width: 1250px; }
.sortable-header { cursor: pointer; white-space: nowrap; user-select: none; }
.sortable-header:hover { text-decoration: underline; }
.culture-number { font-variant-numeric: tabular-nums; }
</style>

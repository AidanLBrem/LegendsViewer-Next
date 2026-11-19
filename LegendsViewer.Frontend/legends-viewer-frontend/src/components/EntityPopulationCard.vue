<script setup lang="ts">
import { computed } from 'vue';
import type { components } from '../generated/api-schema';
import { summarizeNationPopulation } from '../utils/entityPopulation';

const props = defineProps<{
    populations?: components['schemas']['Population'][] | null;
    entityPopulation?: components['schemas']['EntityPopulation'] | null;
}>();

const population = computed(() =>
    summarizeNationPopulation(props.populations, props.entityPopulation)
);

const formatCount = (count: number): string => count.toLocaleString();
const formatShare = (percentage: number): string =>
    percentage > 0 && percentage < 0.1 ? '<0.1%' : `${percentage.toFixed(1)}%`;
</script>

<template>
    <v-card title="Population" subtitle="Recorded population by race, from available exports" variant="text">
        <template #prepend>
            <v-icon class="mr-2" icon="mdi-account-group" size="32px" />
        </template>
        <v-card-text>
            <template v-if="population.combinedRaces.length > 0">
                <div class="text-h5 font-weight-medium mb-1">
                    {{ formatCount(population.combinedPopulationTotal) }}
                </div>
                <div class="text-caption mb-3">
                    Combined recorded population estimate (overlapping race records counted once)
                </div>
                <v-table class="population-table" density="compact" fixed-header height="340px">
                    <thead>
                        <tr>
                            <th>Race</th>
                            <th class="text-end">Sites</th>
                            <th class="text-end">Legends Plus</th>
                            <th class="text-end">Included</th>
                            <th class="text-end">Share</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="row in population.combinedRaces" :key="row.key">
                            <td class="population-race">
                                <div>{{ row.race }}</div>
                                <div class="text-caption text-medium-emphasis" v-if="row.classifications.length">
                                    {{ row.classifications.join(' · ') }}
                                </div>
                                <v-progress-linear
                                    class="mt-1"
                                    :model-value="row.percentage"
                                    color="primary"
                                    height="4"
                                    rounded
                                />
                            </td>
                            <td class="text-end population-number">
                                {{ row.siteCount !== null ? formatCount(row.siteCount) : '—' }}
                            </td>
                            <td class="text-end population-number">
                                {{ row.legendsPlusCount !== null ? formatCount(row.legendsPlusCount) : '—' }}
                            </td>
                            <td class="text-end population-number font-weight-medium">
                                {{ formatCount(row.count) }}
                            </td>
                            <td class="text-end">{{ formatShare(row.percentage) }}</td>
                        </tr>
                    </tbody>
                </v-table>
                <div class="text-caption mt-2">
                    Sites: {{ formatCount(population.sitePopulationTotal) }} residents reported in
                    <code>world_sites_and_pops.txt</code>.
                    <template v-if="population.entityPopulationCount !== null">
                        Legends Plus: {{ formatCount(population.entityPopulationCount) }}
                        {{ population.entityPopulationRace }} from an
                        <code>entity_population</code> entry in <code>legends_plus.xml</code>
                        linked by <code>civ_id</code>.
                    </template>
                </div>
                <div class="text-caption text-medium-emphasis mt-2">
                    The exports use different population records, which may overlap and are not
                    synchronized historical censuses.
                    <template v-if="population.overlappingRaceCount > 0">
                        Where both sources report the same race, the larger count is used rather
                        than summing them.
                    </template>
                </div>
            </template>
            <div v-else class="text-body-2 text-medium-emphasis">
                No population counts were imported for this faction.
                Import <code>legends_plus.xml</code> or the world's Sites and Populations export.
            </div>
        </v-card-text>
    </v-card>
</template>

<style scoped>
.population-race {
    min-width: 130px;
    max-width: 270px;
    padding-top: 8px !important;
    padding-bottom: 8px !important;
}
.population-number {
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
}
</style>

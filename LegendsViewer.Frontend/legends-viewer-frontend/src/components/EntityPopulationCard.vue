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
    <v-card title="Population" subtitle="Recorded population by race" variant="text">
        <template #prepend>
            <v-icon class="mr-2" icon="mdi-account-group" size="32px" />
        </template>
        <v-card-text>
            <template v-if="population.races.length > 0">
                <div class="text-h5 font-weight-medium mb-1">
                    {{ formatCount(population.sitePopulationTotal) }}
                </div>
                <div class="text-caption mb-3">Residents counted at the nation's recorded sites</div>
                <v-table class="population-table" density="compact" fixed-header height="340px">
                    <thead>
                        <tr>
                            <th>Race</th>
                            <th class="text-end">Population</th>
                            <th class="text-end">Share</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="row in population.races" :key="row.key">
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
                            <td class="text-end population-number">{{ formatCount(row.count) }}</td>
                            <td class="text-end">{{ formatShare(row.percentage) }}</td>
                        </tr>
                    </tbody>
                </v-table>
                <div class="text-caption mt-2">
                    Site population is a snapshot from the optional Sites and Populations export.
                    It is not a historical population graph.
                </div>
            </template>
            <div v-else class="text-body-2 text-medium-emphasis">
                No settlement population counts were imported for this faction.
                Import the world's Sites and Populations export to see a race breakdown.
            </div>

            <div v-if="population.entityPopulationCount !== null" class="mt-3 pt-2 population-entity-record">
                <div class="font-weight-medium">Separate entity population record</div>
                <div class="text-body-2">
                    {{ formatCount(population.entityPopulationCount) }}
                    {{ population.entityPopulationRace }}
                </div>
                <div class="text-caption text-medium-emphasis">
                    From legends_plus. This may use different accounting and is not added
                    to the site population total.
                </div>
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
.population-entity-record {
    border-top: 1px solid rgba(127, 127, 127, .25);
}
</style>

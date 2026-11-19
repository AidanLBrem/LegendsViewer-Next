<script setup lang="ts">
import { computed } from 'vue';
import DoughnutChart from './DoughnutChart.vue';
import type { components } from '../generated/api-schema';

type RaceChart = components['schemas']['ChartDataDto'];
const props = defineProps<{
    title: string;
    subtitle: string;
    chartData: RaceChart;
    total: number;
}>();

const races = computed(() => {
    const values = props.chartData.datasets?.[0]?.data ?? [];
    return (props.chartData.labels ?? [])
        .map((race, index) => ({ race, troops: values[index] ?? 0 }))
        .filter(item => item.troops > 0)
        .sort((a, b) => b.troops - a.troops);
});

const chartTotal = computed(() => races.value.reduce((sum, item) => sum + item.troops, 0));
const percentage = (count: number) =>
    chartTotal.value > 0 ? `${(100 * count / chartTotal.value).toFixed(1)}%` : '—';
</script>

<template>
    <v-card :title="title" :subtitle="subtitle" variant="text" height="480">
        <template #prepend>
            <v-icon class="mr-2" icon="mdi-account-group" size="32px" />
        </template>
        <v-card-text>
            <div class="text-subtitle-2 mb-2">
                Total recorded troops: {{ total.toLocaleString() }}
            </div>
            <div class="army-composition-chart">
                <DoughnutChart :chart-data="chartData" />
            </div>
            <div class="army-race-breakdown" role="region" aria-label="Army composition by race">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Race</th>
                            <th scope="col">Troops</th>
                            <th scope="col">Share</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="(item, index) in races" :key="item.race + index">
                            <td>{{ item.race }}</td>
                            <td class="number">{{ item.troops.toLocaleString() }}</td>
                            <td class="number">{{ percentage(item.troops) }}</td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </v-card-text>
    </v-card>
</template>

<style scoped>
.army-composition-chart {
    height: 235px;
    min-height: 235px;
}
.army-race-breakdown {
    max-height: 124px;
    overflow: auto;
    margin-top: 8px;
}
.army-race-breakdown table {
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
}
.army-race-breakdown th {
    text-align: left;
    position: sticky;
    top: 0;
    background: rgb(var(--v-theme-surface));
}
.army-race-breakdown th,
.army-race-breakdown td {
    padding: 3px 8px;
    border-bottom: 1px solid rgba(127, 127, 127, 0.2);
}
.army-race-breakdown .number,
.army-race-breakdown th:nth-child(n+2) {
    text-align: right;
}
</style>

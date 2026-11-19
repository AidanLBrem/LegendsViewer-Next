<script setup lang="ts">
import { useBattleStore } from '../stores/worldObjectStores';
import { useBattleMapStore } from '../stores/mapStore';
import WorldObjectPage from '../components/WorldObjectPage.vue';
import LegendsCardList from '../components/LegendsCardList.vue';
import DoughnutChart from '../components/DoughnutChart.vue';
import { computed, ComputedRef } from 'vue';
import { LegendLinkListData } from '../types/legends';

const store = useBattleStore()
const mapStore = useBattleMapStore()


const casualtyRows = computed(() => {
    const battle = store.object;
    return [
        {
            side: 'Attackers',
            deployed: battle?.attackerCount ?? 0,
            deaths: battle?.attackerDeathCount ?? 0,
            remaining: battle?.attackersRemainingCount ?? 0,
        },
        {
            side: 'Defenders',
            deployed: battle?.defenderCount ?? 0,
            deaths: battle?.defenderDeathCount ?? 0,
            remaining: battle?.defendersRemainingCount ?? 0,
        },
    ];
});

function casualtyPercentage(deaths: number, deployed: number): string {
    return deployed > 0 ? `${(100 * deaths / deployed).toFixed(1)}%` : '—';
}

const lists: ComputedRef<LegendLinkListData[]> = computed(() => [
    { title: 'Battle Overview', items: store.object?.miscList ?? [], icon: "mdi-swords-crossed", subtitle: "A summary of significant details of this battle" },
    { title: 'Notable Deaths', items: store.object?.notableDeathLinks ?? [], icon: "mdi-grave-stone", subtitle: "Key figures who died in this battle" },
]);

</script>

<template>
    <WorldObjectPage :store="store" :mapStore="mapStore" :object-type="'battle'">
        <template v-slot:type-specific-before-table>
            <v-col cols="12" xl="4" lg="6" md="12">
                <v-card title="Casualty Report" subtitle="Losses and survivors on each side" variant="text">
                    <template v-slot:prepend>
                        <v-icon class="mr-2" icon="mdi-shield-sword" size="32px"></v-icon>
                    </template>
                    <v-card-text>
                        <v-table density="compact">
                            <thead>
                                <tr>
                                    <th>Side</th>
                                    <th class="text-end">Forces</th>
                                    <th class="text-end">Deaths</th>
                                    <th class="text-end">Survivors</th>
                                    <th class="text-end">Loss %</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr v-for="row in casualtyRows" :key="row.side">
                                    <td>{{ row.side }}</td>
                                    <td class="text-end">{{ row.deployed.toLocaleString() }}</td>
                                    <td class="text-end">{{ row.deaths.toLocaleString() }}</td>
                                    <td class="text-end">{{ row.remaining.toLocaleString() }}</td>
                                    <td class="text-end">{{ casualtyPercentage(row.deaths, row.deployed) }}</td>
                                </tr>
                            </tbody>
                        </v-table>
                    </v-card-text>
                </v-card>
            </v-col>
            <template v-for="(list, i) in lists" :key="i">
                <v-col v-if="list?.items.length" cols="12" xl="4" lg="6" md="12">
                    <LegendsCardList :list="list" />
                </v-col>
            </template>
           <v-col v-if="store.object?.deathsByRace?.labels != null && store.object?.deathsByRace?.labels?.length > 0"
                cols="12" xl="4" lg="6" md="12">
                <v-card title="Deaths by Race" :subtitle="'How many deaths were in this battle, grouped by the race'"
                    height="400" variant="text">
                    <template v-slot:prepend>
                        <v-icon class="mr-2" icon="mdi-grave-stone" size="32px"></v-icon>
                    </template>
                    <v-card-text>
                        <DoughnutChart :chart-data="store.object?.deathsByRace" />
                    </v-card-text>
                </v-card>
            </v-col>
        </template>
    </WorldObjectPage>
</template>

<style scoped></style>
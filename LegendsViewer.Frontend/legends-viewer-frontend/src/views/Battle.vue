<script setup lang="ts">
import { useBattleStore } from '../stores/worldObjectStores';
import { useBattleMapStore } from '../stores/mapStore';
import WorldObjectPage from '../components/WorldObjectPage.vue';
import LegendsCardList from '../components/LegendsCardList.vue';
import DoughnutChart from '../components/DoughnutChart.vue';
import WarfareOverviewCard from '../components/WarfareOverviewCard.vue';
import { computed, ComputedRef } from 'vue';
import { LegendLinkListData } from '../types/legends';

const store = useBattleStore()
const mapStore = useBattleMapStore()



const lists: ComputedRef<LegendLinkListData[]> = computed(() => [
    { title: 'Battle Overview', items: store.object?.miscList ?? [], icon: "mdi-swords-crossed", subtitle: "A summary of significant details of this battle" },
    { title: 'Notable Deaths', items: store.object?.notableDeathLinks ?? [], icon: "mdi-grave-stone", subtitle: "Key figures who died in this battle" },
]);

</script>

<template>
    <WorldObjectPage :store="store" :mapStore="mapStore" :object-type="'battle'">
        <template v-slot:type-specific-before-table>
            <v-col cols="12" xl="6" lg="12" md="12">
                <WarfareOverviewCard
                    :attacker-deaths="store.object?.attackerDeathCount"
                    :defender-deaths="store.object?.defenderDeathCount"
                    :total-deaths="store.object?.deathCount"
                    :attacker-deployed="store.object?.attackerCount"
                    :defender-deployed="store.object?.defenderCount"
                    :attacker-remaining="store.object?.attackersRemainingCount"
                    :defender-remaining="store.object?.defendersRemainingCount"
                    :site-outcomes="store.object?.siteOutcomes" />
            </v-col>
            <template v-for="(list, i) in lists" :key="i">
                <v-col v-if="list?.items.length" cols="12" xl="4" lg="6" md="12">
                    <LegendsCardList :list="list" />
                </v-col>
            </template>
            <v-col v-if="store.object?.attackerDeathsByRace?.labels?.length"
                cols="12" xl="4" lg="6" md="12">
                <v-card title="Attacker Deaths by Race"
                    subtitle="Deaths among the attacking forces, including recorded notable figures"
                    height="400" variant="text">
                    <template #prepend>
                        <v-icon class="mr-2" icon="mdi-grave-stone" size="32px" />
                    </template>
                    <v-card-text>
                        <DoughnutChart :chart-data="store.object.attackerDeathsByRace" />
                    </v-card-text>
                </v-card>
            </v-col>
            <v-col v-if="store.object?.defenderDeathsByRace?.labels?.length"
                cols="12" xl="4" lg="6" md="12">
                <v-card title="Defender Deaths by Race"
                    subtitle="Deaths among the defending forces, including recorded notable figures"
                    height="400" variant="text">
                    <template #prepend>
                        <v-icon class="mr-2" icon="mdi-grave-stone" size="32px" />
                    </template>
                    <v-card-text>
                        <DoughnutChart :chart-data="store.object.defenderDeathsByRace" />
                    </v-card-text>
                </v-card>
            </v-col>
        </template>
    </WorldObjectPage>
</template>

<style scoped></style>
<script setup lang="ts">
import { useBattleStore } from '../stores/worldObjectStores';
import { useBattleMapStore } from '../stores/mapStore';
import WorldObjectPage from '../components/WorldObjectPage.vue';
import LegendsCardList from '../components/LegendsCardList.vue';
import DoughnutChart from '../components/DoughnutChart.vue';
import WarfareOverviewCard from '../components/WarfareOverviewCard.vue';
import ArmyCompositionCard from '../components/ArmyCompositionCard.vue';
import { computed, ComputedRef } from 'vue';
import { LegendLinkListData } from '../types/legends';

import { strengthBasisLabel, skillEstimateExplanation } from '../utils/strengthBasis';

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
            <v-col v-if="store.object" cols="12" xl="6" lg="12" md="12">
                <v-card title="Starting Army Strength" variant="text"
                    :subtitle="strengthBasisLabel(store.object.armyStrengthBasis)">
                    <v-card-text>
                        <v-row>
                            <v-col cols="6">
                                <div class="text-caption">Attacker army strength</div>
                                <div class="text-h5">{{ store.object.attackerArmyStrength?.toLocaleString() ?? '—' }}</div>
                                <div class="text-caption">Rating coverage: {{ store.object.attackerRatingCoverage?.toFixed(1) ?? '—' }}%</div>
                                <div v-for="p in store.object.attackerStrengthEstimate?.placeholders" :key="p.race" class="text-caption" title="Unknown combat rating: 1,200 per unit">* {{ p.count }} × {{ p.race }} (placeholder)</div>
                            </v-col>
                            <v-col cols="6">
                                <div class="text-caption">Defender army strength</div>
                                <div class="text-h5">{{ store.object.defenderArmyStrength?.toLocaleString() ?? '—' }}</div>
                                <div class="text-caption">Rating coverage: {{ store.object.defenderRatingCoverage?.toFixed(1) ?? '—' }}%</div>
                                <div v-for="p in store.object.defenderStrengthEstimate?.placeholders" :key="p.race" class="text-caption" title="Unknown combat rating: 1,200 per unit">* {{ p.count }} × {{ p.race }} (placeholder)</div>
                            </v-col>
                        </v-row>
                        <div class="mt-3">Attacker : defender strength ratio:
                            {{ store.object.armyStrengthRatio == null ? '—' : store.object.armyStrengthRatio.toLocaleString(undefined, { maximumFractionDigits: 3, minimumSignificantDigits: 1 }) + ' : 1' }}
                        </div>
                        <div class="text-caption">Before casualties. {{ skillEstimateExplanation }}</div>
                    </v-card-text>
                </v-card>
            </v-col>
            <v-col v-if="store.object?.attackerInitialArmyByRace?.labels?.length"
                cols="12" xl="4" lg="6" md="12">
                <ArmyCompositionCard
                    title="Attacking Army by Race"
                    subtitle="Starting troops, before battle casualties"
                    :total="store.object?.attackerCount ?? 0"
                    :chart-data="store.object.attackerInitialArmyByRace" />
            </v-col>
            <v-col v-if="store.object?.defenderInitialArmyByRace?.labels?.length"
                cols="12" xl="4" lg="6" md="12">
                <ArmyCompositionCard
                    title="Defending Army by Race"
                    subtitle="Starting troops, before battle casualties"
                    :total="store.object?.defenderCount ?? 0"
                    :chart-data="store.object.defenderInitialArmyByRace" />
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
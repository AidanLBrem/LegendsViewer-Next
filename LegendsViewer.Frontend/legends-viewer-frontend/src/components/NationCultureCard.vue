<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import {
    cultureDimensionDescriptions, cultureWindows,
    type NationCultureEvidence, type NationCulturePeriod, type NationCultureProfile
} from '../types/nationCulture';

const props = defineProps<{ entityId: number }>();
const profile = ref<NationCultureProfile | null>(null);
const loading = ref(false);
const error = ref('');
const windowYears = ref(100);
const evidenceCategory = ref('all');
let requestNumber = 0;

watch(() => props.entityId, async id => {
    const request = ++requestNumber;
    profile.value = null;
    if (!id) return;
    loading.value = true;
    error.value = '';
    try {
        const response = await fetch('http://localhost:15421/api/Entity/' + id + '/culture');
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const data: NationCultureProfile = await response.json();
        if (request === requestNumber) profile.value = data;
    } catch (e) {
        if (request === requestNumber) error.value = 'Could not load recorded culture: ' + String(e);
    } finally {
        if (request === requestNumber) loading.value = false;
    }
}, { immediate: true });

const period = computed<NationCulturePeriod | undefined>(() =>
    profile.value?.periods.find(p => p.windowYears === windowYears.value)
);
const rateRows = computed(() => {
    const p = period.value;
    if (!p) return [];
    return [
        { title: 'Expansion', value: p.expansionPerCentury, key: 'expansionPerCentury', icon: 'mdi-map-marker-plus' },
        { title: 'Offensive warfare', value: p.offensiveOperationsPerCentury, key: 'offensiveOperationsPerCentury', icon: 'mdi-sword-cross' },
        { title: 'Plundering', value: p.plundersPerCentury, key: 'plundersPerCentury', icon: 'mdi-sack' },
        { title: 'Diplomacy', value: p.diplomacyPerCentury, key: 'diplomacyPerCentury', icon: 'mdi-handshake' },
        { title: 'Internal upheaval', value: p.upheavalsPerCentury, key: 'upheavalsPerCentury', icon: 'mdi-alert-circle-outline' },
    ];
});
const countRows = computed(() => {
    const p = period.value;
    if (!p) return [];
    return [
        { title: 'Sites conquered', count: p.conquests },
        { title: 'Settlements founded', count: p.settlementsFounded },
        { title: 'Sites reclaimed', count: p.sitesReclaimed },
        { title: 'Sites lost', count: p.sitesLost },
        { title: 'Offensive operations', count: p.offensiveOperations },
        { title: 'Defensive operations', count: p.defensiveOperations },
        { title: 'Wars initiated', count: p.warsInitiated },
        { title: 'Wars defended', count: p.warsDefended },
        { title: 'Sites plundered', count: p.sitesPlundered },
        { title: 'Peace agreements', count: p.peaceAgreements },
        { title: 'Alliances formed', count: p.alliances },
        { title: 'Internal upheavals', count: p.internalUpheavals },
        { title: 'Harsh edicts issued', count: p.harshEdictsIssued },
        { title: 'Harsh edicts repealed', count: p.harshEdictsRevoked },
    ];
});
const categories = [
    { title: 'All evidence', value: 'all' },
    { title: 'Conquests', value: 'conquest' },
    { title: 'Settlements', value: 'settlement' },
    { title: 'Offensive engagements', value: 'offensive' },
    { title: 'Defensive engagements', value: 'defensive' },
    { title: 'Wars initiated', value: 'warInitiated' },
    { title: 'Plundering', value: 'plunder' },
    { title: 'Peace', value: 'peace' },
    { title: 'Alliances', value: 'alliance' },
    { title: 'Internal upheaval', value: 'upheaval' },
    { title: 'Harsh laws issued', value: 'harshLaw' },
    { title: 'Harsh laws repealed', value: 'lawRepealed' },
    { title: 'Territorial losses', value: 'loss' },
];
const selectedEvidence = computed<NationCultureEvidence[]>(() => {
    const start = period.value?.startYear ?? 0;
    return (profile.value?.evidence ?? [])
        .filter(e => e.year >= start && (evidenceCategory.value === 'all' || e.category === evidenceCategory.value))
        .slice(0, 40);
});
const visibleTimeline = computed(() =>
    (profile.value?.timeline ?? []).filter(point => point.year + 9 >= (period.value?.startYear ?? 0))
);
const maxTimelineValue = computed(() => Math.max(1, ...visibleTimeline.value.map(point =>
    Math.max(point.conquests + point.settlementsFounded, point.offensiveOperations, point.peaceAgreements)
)));
const barHeight = (value: number) =>
    value === 0 ? '0%' : Math.max(3, Math.round(value * 100 / maxTimelineValue.value)) + '%';
const formatRate = (number: number) => number.toLocaleString(undefined, { maximumFractionDigits: 2 });
</script>

<template>
    <v-col cols="12">
        <v-card title="Culture & National Character" subtitle="Observed behavior from the recorded history of this civilization" variant="text">
            <template #prepend><v-icon class="mr-2" icon="mdi-compass-rose" size="32px" /></template>
            <v-card-text>
                <v-progress-linear v-if="loading" indeterminate color="primary" />
                <v-alert v-else-if="error" type="warning" variant="tonal" :text="error" />
                <template v-else-if="profile && period">
                    <div class="d-flex align-center flex-wrap ga-3 mb-3">
                        <v-chip color="primary" variant="tonal" prepend-icon="mdi-history">{{ period.character }}</v-chip>
                        <v-select v-model="windowYears" :items="cultureWindows" label="Historical period"
                            variant="outlined" density="compact" hide-details style="max-width: 260px;" />
                        <span class="text-caption text-medium-emphasis">
                            Years {{ period.startYear }}–{{ period.endYear }} · {{ period.observedYears }} observed years
                        </span>
                    </div>
                    <div class="text-caption text-medium-emphasis mb-2">
                        Rates are recorded events per 100 observed years, not in-game personality values or 0–100 scores.
                        Select a dimension for its calculation.
                    </div>
                    <v-row dense>
                        <v-col v-for="row in rateRows" :key="row.key" cols="6" sm="4" lg>
                            <v-tooltip :text="cultureDimensionDescriptions[row.key]" location="top">
                                <template #activator="{ props: tooltipProps }">
                                    <v-sheet border rounded class="pa-3 culture-metric" v-bind="tooltipProps">
                                        <v-icon :icon="row.icon" color="primary" size="small" />
                                        <div class="text-h6 font-weight-bold">{{ formatRate(row.value) }}</div>
                                        <div class="text-caption">{{ row.title }} / century</div>
                                    </v-sheet>
                                </template>
                            </v-tooltip>
                        </v-col>
                    </v-row>
                    <v-row class="mt-3">
                        <v-col cols="12" lg="6">
                            <div class="text-subtitle-2 mb-2">Historical activity breakdown</div>
                            <v-table density="compact">
                                <tbody>
                                    <tr v-for="row in countRows" :key="row.title">
                                        <td>{{ row.title }}</td>
                                        <td class="text-end font-weight-medium">{{ row.count.toLocaleString() }}</td>
                                    </tr>
                                </tbody>
                            </v-table>
                        </v-col>
                        <v-col cols="12" lg="6">
                            <div class="text-subtitle-2 mb-2">Activity over time</div>
                            <div class="d-flex flex-wrap ga-3 text-caption mb-2">
                                <span><span class="legend-dot settlement" /> Expansion</span>
                                <span><span class="legend-dot offensive" /> Offensive engagements</span>
                                <span><span class="legend-dot peace" /> Peace</span>
                            </div>
                            <div v-if="visibleTimeline.length" class="culture-timeline">
                                <div v-for="point in visibleTimeline" :key="point.year" class="decade"
                                    :title="'Years ' + point.year + '–' + (point.year + 9)">
                                    <div class="decade-bars">
                                        <div class="timeline-bar settlement" :style="{height: barHeight(point.conquests + point.settlementsFounded)}"
                                            :title="'Expansion: ' + (point.conquests + point.settlementsFounded)" />
                                        <div class="timeline-bar offensive" :style="{height: barHeight(point.offensiveOperations)}"
                                            :title="'Offensive engagements: ' + point.offensiveOperations" />
                                        <div class="timeline-bar peace" :style="{height: barHeight(point.peaceAgreements)}"
                                            :title="'Peace agreements: ' + point.peaceAgreements" />
                                    </div>
                                    <span class="decade-label">{{ point.year }}</span>
                                </div>
                            </div>
                            <div class="text-caption text-medium-emphasis mt-2">
                                Ten-year buckets (first and last buckets may extend beyond the selected period).
                                A missing event is not evidence of peacefulness.
                            </div>
                            <v-divider class="my-3" />
                            <div class="text-subtitle-2">Recorded religion</div>
                            <div class="text-body-2">
                                {{ profile.worshippedDeities }} worshipped {{ profile.worshippedDeities === 1 ? 'deity' : 'deities' }} in the entity record.
                                This does not measure religious influence.
                            </div>
                            <div class="text-subtitle-2 mt-3">Intrinsic cultural values and ethics</div>
                            <div class="text-body-2 text-medium-emphasis">{{ profile.intrinsicValuesNote }}</div>
                        </v-col>
                    </v-row>
                    <v-expansion-panels variant="accordion" class="mt-4">
                        <v-expansion-panel>
                            <v-expansion-panel-title>Supporting historical evidence</v-expansion-panel-title>
                            <v-expansion-panel-text>
                                <v-select v-model="evidenceCategory" :items="categories" label="Event type"
                                    density="compact" variant="outlined" hide-details class="mb-3" style="max-width: 320px;" />
                                <v-list v-if="selectedEvidence.length" density="compact" lines="two">
                                    <v-list-item v-for="(entry, index) in selectedEvidence" :key="entry.year + '-' + index">
                                        <v-list-item-title>
                                            <router-link v-if="entry.link" :to="entry.link">{{ entry.description }}</router-link>
                                            <span v-else>{{ entry.description }}</span>
                                        </v-list-item-title>
                                        <v-list-item-subtitle>Year {{ entry.year }} · {{ entry.category }}</v-list-item-subtitle>
                                    </v-list-item>
                                </v-list>
                                <div v-else class="text-body-2 text-medium-emphasis">
                                    No supporting records in this category and period.
                                </div>
                                <div class="text-caption text-medium-emphasis mt-2">
                                    Displays up to 40 matches from the 500 most recent evidence records. The metrics use all records.
                                </div>
                            </v-expansion-panel-text>
                        </v-expansion-panel>
                    </v-expansion-panels>
                </template>
            </v-card-text>
        </v-card>
    </v-col>
</template>

<style scoped>
.culture-metric { height: 100%; }
.culture-timeline {
    min-height: 180px;
    display: flex;
    gap: 4px;
    align-items: flex-end;
    overflow-x: auto;
    padding: 10px 4px;
    border-bottom: 1px solid rgba(127, 127, 127, .3);
}
.decade { flex: 1 0 24px; min-width: 24px; text-align: center; }
.decade-bars { display: flex; align-items: flex-end; height: 138px; gap: 2px; }
.timeline-bar { flex: 1; min-width: 4px; border-radius: 2px 2px 0 0; }
.settlement { background-color: #478f72; }
.offensive { background-color: #c28c50; }
.peace { background-color: #648ac7; }
.decade-label { font-size: 9px; color: rgba(150, 150, 150, .9); }
.legend-dot { width: 9px; height: 9px; display: inline-block; margin-right: 3px; border-radius: 2px; }
</style>

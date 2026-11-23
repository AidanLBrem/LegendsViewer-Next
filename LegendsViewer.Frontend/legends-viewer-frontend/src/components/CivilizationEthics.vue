<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { compareEthics, ethicsSource, type EthicsNation } from '../utils/civilizationEthics';

const props = defineProps<{ entityId: number }>();
const nations = ref<EthicsNation[]>([]);
const loading = ref(true);
const error = ref('');
const selected = ref<number | null>(null);
const onlyTensions = ref(true);
interface DiplomacyEvidence { year: number; kind: string; description: string; link: string | null }
interface DiplomacyFactors {
    exactScoresAvailable: boolean;
    exactScoresNote: string;
    personal: DiplomacyEvidence[];
    historical: DiplomacyEvidence[];
}
const factors = ref<DiplomacyFactors | null>(null);
const factorLoading = ref(false);
const factorError = ref('');
let factorRequest = 0;
const nation = computed(() => nations.value.find(n => n.id === props.entityId));
const peers = computed(() => nations.value.filter(n => n.id !== props.entityId));
const other = computed(() => peers.value.find(n => n.id === selected.value));
const rows = computed(() => {
    if (!nation.value) return [];
    const all = compareEthics(nation.value, other.value);
    if (!other.value || !onlyTensions.value) return all;
    return all.filter(r => (r.reaction ?? 0) < 0 || (r.otherReaction ?? 0) < 0)
        .sort((a, b) => Math.min(a.reaction!, a.otherReaction!) - Math.min(b.reaction!, b.otherReaction!));
});
const ethicsTensions = computed(() => {
    if (!nation.value || !other.value) return [];
    return compareEthics(nation.value, other.value)
        .filter(row => (row.reaction ?? 0) < 0 || (row.otherReaction ?? 0) < 0);
});
watch(() => props.entityId, () => { selected.value = null; });
watch(() => [props.entityId, selected.value] as const, async ([id, otherId]) => {
    const request = ++factorRequest;
    factors.value = null;
    factorError.value = '';
    if (otherId == null) {
        factorLoading.value = false;
        return;
    }
    factorLoading.value = true;
    try {
        const response = await fetch('http://localhost:15421/api/Entity/' + id + '/diplomacy/' + otherId);
        if (!response.ok) throw Error('HTTP ' + response.status);
        const data: DiplomacyFactors = await response.json();
        if (request === factorRequest) factors.value = data;
    } catch (e) {
        if (request === factorRequest) factorError.value = 'Could not load diplomatic history: ' + String(e);
    } finally {
        if (request === factorRequest) factorLoading.value = false;
    }
}, { immediate: true });
onMounted(async () => {
    try {
        const response = await fetch('http://localhost:15421/api/Entity/ethics');
        if (!response.ok) throw Error('HTTP ' + response.status);
        nations.value = await response.json();
    } catch (e) {
        error.value = 'Could not load civilization ethics. ' + String(e);
    } finally {
        loading.value = false;
    }
});
</script>

<template>
    <section class="mb-5">
        <h3 class="text-h6">Civilization ethics</h3>
        <v-progress-linear v-if="loading" indeterminate />
        <v-alert v-else-if="error" type="warning" :text="error" />
        <template v-else-if="nation">
            <p><strong>Slavery</strong> · {{ compareEthics(nation)[0].policy }} · {{ ethicsSource(nation) }}</p>
            <p class="text-caption mb-2">Vanilla estimates use the civilization’s race. Modded and individual civilizations can differ.
                For world-specific policies, run the supplied DFHack script export-civ-ethics alongside your Legends export.</p>
            <v-autocomplete v-model="selected" :items="peers" item-title="name" item-value="id"
                label="Compare ethics with another civilization" density="compact" clearable hide-details />
            <template v-if="other">
                <section class="mb-4 mt-3" aria-label="Diplomatic factors">
                    <h4 class="text-subtitle-1 mb-1">Diplomatic factor breakdown · {{ nation.name }} and {{ other.name }}</h4>
                    <p class="text-caption text-medium-emphasis mb-2">
                        Personal, general, and historical context from the available exports. These are not the
                        hidden DFHack diplomatic evaluation totals or a prediction of war.
                    </p>
                    <v-progress-linear v-if="factorLoading" indeterminate color="primary" />
                    <v-alert v-else-if="factorError" type="warning" variant="tonal" :text="factorError" />
                    <template v-else-if="factors">
                        <v-alert type="info" variant="tonal" density="compact" class="mb-3">
                            {{ factors.exactScoresNote }}
                        </v-alert>
                        <v-row dense>
                            <v-col cols="12" md="4">
                                <v-card variant="outlined" class="fill-height" title="Personal factors">
                                    <v-card-text>
                                        <div class="text-caption text-medium-emphasis mb-2">
                                            Religious and leadership context. True ruler-specific values are not in Legends.
                                        </div>
                                        <ul v-if="factors.personal.length" class="pl-4">
                                            <li v-for="(item, i) in factors.personal" :key="i">
                                                <router-link v-if="item.link" :to="item.link">{{ item.description }}</router-link>
                                                <span v-else>{{ item.description }}</span>
                                            </li>
                                        </ul>
                                        <p v-else>No shared deities recorded. This does not establish hostility.</p>
                                    </v-card-text>
                                </v-card>
                            </v-col>
                            <v-col cols="12" md="4">
                                <v-card variant="outlined" class="fill-height" title="General factors">
                                    <v-card-text>
                                        <div class="text-caption text-medium-emphasis mb-2">
                                            Ethics comparisons; {{ nation.name }}: {{ ethicsSource(nation) }},
                                            {{ other.name }}: {{ ethicsSource(other) }}. Not an in-game total.
                                        </div>
                                        <p><strong>{{ ethicsTensions.length }}</strong> practices with at least one adverse directional reaction.</p>
                                        <ul v-if="ethicsTensions.length" class="pl-4">
                                            <li v-for="item in ethicsTensions.slice(0, 8)" :key="item.key">
                                                {{ item.issue }}:
                                                {{ item.reaction ?? 'unknown' }} / {{ item.otherReaction ?? 'unknown' }}
                                            </li>
                                        </ul>
                                        <p v-else>No adverse reactions found among comparable known practices.</p>
                                        <p class="text-caption text-medium-emphasis mt-2">
                                            Reactions are {{ nation.name }} toward {{ other.name }} / reverse.
                                            See the ethics table below for all practices.
                                        </p>
                                    </v-card-text>
                                </v-card>
                            </v-col>
                            <v-col cols="12" md="4">
                                <v-card variant="outlined" class="fill-height" title="Historical factors">
                                    <v-card-text>
                                        <div class="text-caption text-medium-emphasis mb-2">
                                            Bilateral events. A past alliance or war does not establish current diplomatic status.
                                        </div>
                                        <div v-if="factors.historical.length" style="max-height: 340px; overflow-y: auto;">
                                            <div v-for="(item, i) in factors.historical" :key="i" class="mb-2">
                                                <v-chip size="x-small" variant="tonal"
                                                    :color="item.kind === 'hostile' ? 'error' : item.kind === 'cooperative' ? 'success' : undefined">
                                                    Year {{ item.year }} · {{ item.kind }}
                                                </v-chip>
                                                <div>
                                                    <router-link v-if="item.link" :to="item.link">{{ item.description }}</router-link>
                                                    <span v-else>{{ item.description }}</span>
                                                </div>
                                            </div>
                                        </div>
                                        <p v-else>No bilateral events recorded; this is not proof of good relations.</p>
                                        <div class="text-caption text-medium-emphasis">Up to 100 most recent recorded events shown.</div>
                                    </v-card-text>
                                </v-card>
                            </v-col>
                        </v-row>
                    </template>
                </section>
                <v-checkbox v-model="onlyTensions" label="Only show sources of tension" density="compact" hide-details />
                <p class="text-caption">Negative reactions indicate potential friction. Each score uses the documented ethics comparison table.
                    A score alone cannot establish the cause of a particular war.</p>
            </template>
            <v-table density="compact">
                <thead><tr><th>Practice</th><th>{{ nation.name }} · {{ ethicsSource(nation) }}</th>
                    <th v-if="other">{{ other.name }} · {{ ethicsSource(other) }}</th>
                    <th v-if="other">Reaction of {{ nation.name }}</th><th v-if="other">Reaction of {{ other.name }}</th></tr></thead>
                <tbody><tr v-for="row in rows" :key="row.key"><th scope="row">{{ row.issue }}</th><td>{{ row.policy }}</td>
                    <td v-if="other">{{ row.otherPolicy }}</td><td v-if="other">{{ row.reaction ?? '—' }}</td>
                    <td v-if="other">{{ row.otherReaction ?? '—' }}</td></tr></tbody>
            </v-table>
            <p v-if="!rows.length">No negative reactions among practices with known policies.</p>
        </template>
        <p v-else>No civilization ethics record available.</p>
    </section>
</template>

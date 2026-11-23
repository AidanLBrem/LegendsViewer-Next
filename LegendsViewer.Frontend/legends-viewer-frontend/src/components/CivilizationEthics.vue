<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { compareEthics, ethicsSource, type EthicsNation } from '../utils/civilizationEthics';

const props = defineProps<{ entityId: number }>();
const nations = ref<EthicsNation[]>([]);
const loading = ref(true);
const error = ref('');
const selected = ref<number | null>(null);
const onlyTensions = ref(true);
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
watch(() => props.entityId, () => { selected.value = null; });
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

<script setup lang="ts">
import { computed } from 'vue';
import type { components } from '../generated/api-schema';

type SiteOutcomes = components['schemas']['WarfareSiteOutcomesDto'];

const props = defineProps<{
    attackerDeaths?: number | null;
    defenderDeaths?: number | null;
    totalDeaths?: number | null;
    attackerDeployed?: number | null;
    defenderDeployed?: number | null;
    attackerRemaining?: number | null;
    defenderRemaining?: number | null;
    siteOutcomes?: SiteOutcomes | null;
}>();

const showForces = computed(() =>
    props.attackerDeployed != null && props.defenderDeployed != null
);

const rows = computed(() => [
    {
        side: 'Attackers',
        deployed: props.attackerDeployed ?? 0,
        deaths: props.attackerDeaths ?? 0,
        survivors: props.attackerRemaining ?? 0,
        gained: props.siteOutcomes?.attackerGained ?? [],
        lost: props.siteOutcomes?.attackerLost ?? [],
        plundered: props.siteOutcomes?.attackerPlundered ?? [],
    },
    {
        side: 'Defenders',
        deployed: props.defenderDeployed ?? 0,
        deaths: props.defenderDeaths ?? 0,
        survivors: props.defenderRemaining ?? 0,
        gained: props.siteOutcomes?.defenderGained ?? [],
        lost: props.siteOutcomes?.defenderLost ?? [],
        plundered: props.siteOutcomes?.defenderPlundered ?? [],
    },
]);

const otherDeaths = computed(() =>
    Math.max(0, (props.totalDeaths ?? 0) -
        (props.attackerDeaths ?? 0) - (props.defenderDeaths ?? 0))
);

const siteGroups = computed(() => rows.value.flatMap(row => [
    { key: row.side + '-gained', title: row.side + ' gained', links: row.gained },
    { key: row.side + '-lost', title: row.side + ' lost', links: row.lost },
    { key: row.side + '-plundered', title: row.side + ' plundered', links: row.plundered },
]).filter(group => group.links.length > 0));

function lossPercentage(deaths: number, deployed: number): string {
    return deployed > 0 ? `${(100 * deaths / deployed).toFixed(1)}%` : '—';
}
</script>

<template>
    <v-card title="Casualties & Site Outcomes"
        subtitle="Losses and recorded territorial changes for each side"
        variant="text">
        <template #prepend>
            <v-icon class="mr-2" icon="mdi-shield-sword" size="32px" />
        </template>
        <v-card-text>
            <v-table density="compact">
                <thead>
                    <tr>
                        <th>Side</th>
                        <th v-if="showForces" class="text-end">Forces</th>
                        <th class="text-end">Deaths</th>
                        <th v-if="showForces" class="text-end">Survivors</th>
                        <th v-if="showForces" class="text-end">Loss %</th>
                        <th class="text-end">Gained</th>
                        <th class="text-end">Lost</th>
                        <th class="text-end">Plundered</th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-for="row in rows" :key="row.side">
                        <td>{{ row.side }}</td>
                        <td v-if="showForces" class="text-end">{{ row.deployed.toLocaleString() }}</td>
                        <td class="text-end">{{ row.deaths.toLocaleString() }}</td>
                        <td v-if="showForces" class="text-end">{{ row.survivors.toLocaleString() }}</td>
                        <td v-if="showForces" class="text-end">{{ lossPercentage(row.deaths, row.deployed) }}</td>
                        <td class="text-end">{{ row.gained.length.toLocaleString() }}</td>
                        <td class="text-end">{{ row.lost.length.toLocaleString() }}</td>
                        <td class="text-end">{{ row.plundered.length.toLocaleString() }}</td>
                    </tr>
                </tbody>
            </v-table>
            <div class="text-caption mt-2">
                Total recorded deaths: {{ (totalDeaths ?? 0).toLocaleString() }}
                <span v-if="otherDeaths > 0">
                    (including {{ otherDeaths.toLocaleString() }} other or unattributed deaths)
                </span>
            </div>
            <div class="text-caption mt-1">
                Conquest changes possession; destruction counts as a site lost, not gained.
                Plundering does not count as conquest.
            </div>
            <div v-if="siteGroups.length" class="warfare-site-links mt-3">
                <details v-for="group in siteGroups" :key="group.key">
                    <summary>{{ group.title }} ({{ group.links.length.toLocaleString() }})</summary>
                    <div v-for="(link, index) in group.links" :key="index" class="warfare-site-link" v-html="link"></div>
                </details>
            </div>
        </v-card-text>
    </v-card>
</template>

<style scoped>
.warfare-site-links {
    max-height: 210px;
    overflow-y: auto;
}
.warfare-site-links details {
    margin-bottom: 6px;
}
.warfare-site-links summary {
    cursor: pointer;
    font-size: 13px;
}
.warfare-site-link {
    padding: 3px 0 3px 16px;
    font-size: 13px;
}
</style>

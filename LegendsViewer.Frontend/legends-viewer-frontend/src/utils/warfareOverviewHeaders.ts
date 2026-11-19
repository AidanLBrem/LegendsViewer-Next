import type { TableHeader } from '../types/legends';

// These keys are real numeric properties on War and Battle. The overview
// endpoint sorts the underlying records before applying page limits.
export const warfareOverviewHeaders: TableHeader[] = [
    { title: 'Atk troops', key: 'attackerTroopCount', align: 'end' },
    { title: 'Def troops', key: 'defenderTroopCount', align: 'end' },
    { title: 'Total deaths', key: 'deathCount', align: 'end' },
    { title: 'Atk deaths', key: 'attackerDeathCount', align: 'end' },
    { title: 'Def deaths', key: 'defenderDeathCount', align: 'end' },
    { title: 'Atk gained', key: 'attackerSitesGainedCount', align: 'end' },
    { title: 'Atk lost', key: 'attackerSitesLostCount', align: 'end' },
    { title: 'Atk plundered', key: 'attackerSitesPlunderedCount', align: 'end' },
    { title: 'Def gained', key: 'defenderSitesGainedCount', align: 'end' },
    { title: 'Def lost', key: 'defenderSitesLostCount', align: 'end' },
    { title: 'Def plundered', key: 'defenderSitesPlunderedCount', align: 'end' },
    { title: 'Other sites', key: 'unattributedSitesCount', align: 'end' },
];

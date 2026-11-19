import type { TableHeader } from '../types/legends';

// These keys are real numeric properties on War and Battle. The overview
// endpoint sorts the underlying records before applying page limits.
export const warfareOverviewHeaders: TableHeader[] = [
    { title: 'Total deaths', key: 'deathCount', align: 'end' },
    { title: 'Atk deaths', key: 'attackerDeathCount', align: 'end' },
    { title: 'Def deaths', key: 'defenderDeathCount', align: 'end' },
    { title: 'Atk gained', key: 'attackerSitesGained', align: 'end' },
    { title: 'Atk lost', key: 'attackerSitesLost', align: 'end' },
    { title: 'Atk plundered', key: 'attackerSitesPlundered', align: 'end' },
    { title: 'Def gained', key: 'defenderSitesGained', align: 'end' },
    { title: 'Def lost', key: 'defenderSitesLost', align: 'end' },
    { title: 'Def plundered', key: 'defenderSitesPlundered', align: 'end' },
];

import type { TableHeader } from '../types/legends';

// The keys match numeric War/Battle properties so sorting occurs server-side
// across the entire filtered result set, not just the current page.
export const warfareOverviewColumns: TableHeader[] = [
    { title: 'Deaths', key: 'deathCount', align: 'end' },
    { title: 'Att. deaths', key: 'attackerDeathCount', align: 'end' },
    { title: 'Def. deaths', key: 'defenderDeathCount', align: 'end' },
    { title: 'A gained', key: 'attackerSitesGained', align: 'end' },
    { title: 'A lost', key: 'attackerSitesLost', align: 'end' },
    { title: 'A plundered', key: 'attackerSitesPlundered', align: 'end' },
    { title: 'D gained', key: 'defenderSitesGained', align: 'end' },
    { title: 'D lost', key: 'defenderSitesLost', align: 'end' },
    { title: 'D plundered', key: 'defenderSitesPlundered', align: 'end' },
];

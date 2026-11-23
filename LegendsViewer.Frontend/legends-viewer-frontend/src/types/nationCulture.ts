export interface NationCulturePeriod {
    windowYears: number;
    character: string;
    startYear: number;
    endYear: number;
    observedYears: number;
    conquests: number;
    settlementsFounded: number;
    sitesReclaimed: number;
    sitesLost: number;
    sitesLostInWar: number;
    offensiveOperations: number;
    defensiveOperations: number;
    warsInitiated: number;
    warsDefended: number;
    sitesPlundered: number;
    peaceAgreements: number;
    alliances: number;
    internalUpheavals: number;
    harshEdictsIssued: number;
    harshEdictsRevoked: number;
    expansionPerCentury: number;
    settlementExpansionPerCentury: number;
    conquestPerCentury: number;
    sitesLostInWarPerCentury: number;
    offensiveOperationsPerCentury: number;
    plundersPerCentury: number;
    diplomacyPerCentury: number;
    upheavalsPerCentury: number;
}

export interface NationCultureEvidence {
    year: number;
    category: string;
    description: string;
    link: string | null;
}

export interface NationCultureTimeline {
    year: number;
    conquests: number;
    settlementsFounded: number;
    offensiveOperations: number;
    peaceAgreements: number;
}

export interface NationCultureProfile {
    entityId: number;
    currentYear: number;
    character: string;
    worshippedDeities: number;
    lawHistory: { id: number; year: number; law: string; enacted: boolean; figureId: number | null; figureName: string | null }[];
    occasions: { id: number; name: string; activities: string[] }[];
    deities: { id: number; name: string }[];
    intrinsicValuesAvailable: boolean;
    intrinsicValuesNote: string;
    periods: NationCulturePeriod[];
    timeline: NationCultureTimeline[];
    evidence: NationCultureEvidence[];
}

export interface NationCultureSummary {
    id: number;
    name: string;
    race: string;
    currentSites: number;
    character: string;
    periods: NationCulturePeriod[];
}

export const cultureWindows = [
    { title: 'Entire recorded history', value: 0 },
    { title: 'Last 100 years', value: 100 },
    { title: 'Last 25 years', value: 25 },
];

export const cultureDimensionDescriptions: Record<string, string> = {
    expansion: 'Total settlements founded plus sites conquered in the selected period; reclamations are excluded.',
    settlementsFounded: 'Total settlements founded in the selected period; conquered and reclaimed sites are excluded.',
    conquests: 'Total sites taken over or conquered in the selected period; founded and reclaimed settlements are excluded.',
    sitesLostInWar: 'Total settlements captured, surrendered or destroyed by opposing belligerents in recorded wars during the selected period. Excludes pillaging, abandonment and unattributed losses.',
    offensiveOperations: 'Total recorded attacks on sites and offensive field battles in the selected period. These are engagements, not necessarily new wars.',
    sitesPlundered: 'Total recorded site plunderings by this civilization in the selected period.',
    diplomacy: 'Total accepted peace offers and alliances formed in the selected period; not a measure of all diplomatic activity.',
    internalUpheavals: 'Total recorded insurrections and government overthrows in the selected period. A lack of records does not prove stability.',
};

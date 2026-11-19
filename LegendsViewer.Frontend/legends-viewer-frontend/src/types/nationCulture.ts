export interface NationCulturePeriod {
    windowYears: number;
    startYear: number;
    endYear: number;
    observedYears: number;
    conquests: number;
    settlementsFounded: number;
    sitesReclaimed: number;
    sitesLost: number;
    offensiveOperations: number;
    defensiveOperations: number;
    warsInitiated: number;
    warsDefended: number;
    sitesPlundered: number;
    peaceAgreements: number;
    alliances: number;
    internalUpheavals: number;
    expansionPerCentury: number;
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
    { title: 'Last 25 years', value: 25 },
    { title: 'Last 100 years', value: 100 },
    { title: 'Entire recorded history', value: 0 },
];

export const cultureDimensionDescriptions: Record<string, string> = {
    expansionPerCentury: 'Recorded conquests and settlements founded per 100 observed years; reclaims are not new expansion.',
    offensiveOperationsPerCentury: 'Recorded attacks on sites and offensive field battles per 100 observed years. These are engagements, not necessarily new wars.',
    plundersPerCentury: 'Recorded site plunderings by this civilization per 100 observed years.',
    diplomacyPerCentury: 'Accepted peace offers and alliances formed per 100 observed years; not a measure of all diplomatic activity.',
    upheavalsPerCentury: 'Recorded insurrections and government overthrows per 100 observed years. A lack of records does not prove stability.',
};

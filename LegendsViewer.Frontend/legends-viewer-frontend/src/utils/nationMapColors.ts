import { practicePolicy, type EthicsNation } from './civilizationEthics';

export interface MapNation extends Omit<EthicsNation, 'name'> {
    raceName: string;
    necromancer: boolean;
    civilization: boolean;
}
export interface Relation {
    a: number; b: number; kind: 'contact' | 'alliance' | 'war' | 'peace';
    year: number; endYear: number;
}
export interface NationMapData { nations: MapNation[]; relations: Relation[] }
export type NationColorMode = 'Nation' | 'Race' | 'Practice' | 'Diplomacy';
export const nationColorModes: NationColorMode[] = ['Nation', 'Race', 'Practice', 'Diplomacy'];

export function diplomaticStatus(records: Relation[], year: number) {
    const past = records.filter(r => r.year <= year);
    if (!past.length) return 'Unmet';
    const wars = past.filter(r => r.kind === 'war');
    const lastPeace = Math.max(-1, ...past.filter(r => r.kind === 'peace').map(r => r.year));
    if (wars.some(w => w.endYear >= 0 ? w.endYear > year : w.year > lastPeace)) return 'War';
    const lastWar = Math.max(-1, ...wars.map(w => w.year));
    if (past.some(r => r.kind === 'alliance' && r.year > lastWar)) return 'Allied';
    return 'Neutral';
}

const statusColors: Record<string, string> = {
    'Selected nation': '#3a78da', War: '#d94742', Allied: '#248454', Neutral: '#d1ad5c', Unmet: '#777c86',
};
const raceColors: Record<string, string> = {
    dwarf: '#bd7739', elf: '#378856', human: '#487cc0', goblin: '#a64142', kobold: '#a78b37',
};
const policyColors = ['#a8aaae','#178456','#56a75a','#8fbb5e','#c2bb49','#d1a347','#bf833c','#a86d36',
    '#ac98bf','#a26caa','#875086','#dd9259','#d36c49','#bb424c','#7f2638','#462c55','#167b8b'];

function raceColor(token: string) {
    if (Object.prototype.hasOwnProperty.call(raceColors, token)) return raceColors[token];
    let hash = 2166136261;
    for (const char of token) hash = Math.imul(hash ^ char.charCodeAt(0), 16777619);
    return '#' + [0, 8, 16].map(shift => (64 + ((hash >>> shift) & 127)).toString(16)).join('');
}

export function mapCategory(mode: NationColorMode, nation: MapNation | undefined, necromancer: boolean,
    practice: string, status: string) {
    if (mode === 'Diplomacy') return { label: status, color: statusColors[status] };
    if (mode === 'Race' && (necromancer || nation?.necromancer))
        return { label: 'Necromancer', color: '#733fb0' };
    if (mode === 'Race' && nation?.race && nation.race.toLowerCase() !== 'unknown')
        return { label: nation.raceName || nation.race, color: raceColor(nation.race.toLowerCase()) };
    if (mode === 'Practice' && nation && (nation.civilization && !necromancer && !nation.necromancer || Object.keys(nation.responses).length)) {
        const policy = practicePolicy(nation, practice);
        if (policy.index >= 0) return { label: policy.label, color: policyColors[policy.index] };
    }
    return { label: 'Unknown', color: '#808080' };
}

// DFHack ethic_response order. The vanilla profiles and directional reactions are documented at
// https://dwarffortresswiki.org/index.php/Ethic (including the developer's published comparison table).
const responses = [
    'NOT_APPLICABLE', 'ACCEPTABLE', 'PERSONAL_MATTER', 'JUSTIFIED_IF_NO_REPERCUSSIONS',
    'JUSTIFIED_IF_GOOD_REASON', 'JUSTIFIED_IF_EXTREME_REASON', 'JUSTIFIED_IF_SELF_DEFENSE',
    'ONLY_IF_SANCTIONED', 'MISGUIDED', 'SHUN', 'APPALLING', 'PUNISH_REPRIMAND', 'PUNISH_SERIOUS',
    'PUNISH_EXILE', 'PUNISH_CAPITAL', 'UNTHINKABLE', 'REQUIRED',
];
const labels = [
    'No stance', 'Permitted', 'Personal matter', 'Justified if there are no repercussions',
    'Requires good reason', 'Requires extreme justification', 'Only in self-defense',
    'Requires official sanction', 'Considered misguided', 'Socially shunned', 'Considered appalling',
    'Punishable by reprimand', 'Serious punishment', 'Punishable by exile', 'Capital offense',
    'Unthinkable', 'Required',
];
const races = ['dwarf', 'elf', 'human', 'goblin', 'kobold'];
const issues: [string, string, number[]][] = [
    ['SLAVERY', 'Slavery', [14,13,1,2,15]],
    ['KILL_PLANT', 'Killing plants and felling trees', [1,15,1,1,1]],
    ['EAT_SAPIENT_KILL', 'Eating sapients killed in battle', [15,1,15,2,15]],
    ['EAT_SAPIENT_OTHER', 'Eating other sapient dead', [15,15,15,2,15]],
    ['MAKE_TROPHY_SAPIENT', 'Taking trophies from other sapient species', [9,15,1,1,15]],
    ['MAKE_TROPHY_SAME_RACE', 'Taking trophies from one’s own species', [10,15,1,1,15]],
    ['MAKE_TROPHY_ANIMAL', 'Taking animal trophies', [1,15,1,1,15]],
    ['KILL_ANIMAL', 'Killing animals', [1,6,1,1,1]],
    ['KILL_ENTITY_MEMBER', 'Killing a fellow citizen', [14,5,4,2,13]],
    ['KILL_NEUTRAL', 'Killing neutral sapients', [7,1,3,16,16]],
    ['KILL_ENEMY', 'Killing enemies', [1,1,1,16,16]],
    ['TORTURE_AS_EXAMPLE', 'Torture as an example', [15,1,1,1,15]],
    ['TORTURE_FOR_INFORMATION', 'Torture for information', [15,8,1,1,0]],
    ['TORTURE_FOR_FUN', 'Torture for amusement', [15,15,10,1,1]],
    ['TORTURE_ANIMALS', 'Torture of animals', [15,15,9,1,15]],
    ['TREASON', 'Treason', [14,13,14,14,15]],
    ['OATH_BREAKING', 'Breaking oaths', [14,13,14,2,0]],
    ['LYING', 'Lying', [2,13,2,2,0]],
    ['VANDALISM', 'Vandalism', [12,11,12,2,0]],
    ['TRESPASSING', 'Trespassing', [12,11,12,2,0]],
    ['THEFT', 'Theft', [12,11,12,2,0]],
    ['ASSAULT', 'Assault', [12,13,12,2,2]],
];

// Rows are the observer, columns the other civilization. Index 0 (no stance) is excluded.
const permissive = [1,1,1,0,0,0,0,-1,-2,-2,-2,-3,-5,-5,-2,1];
const serious = [-10,-10,-7,-3,-2,-2,-3,0,1,1,0,2,1,1,1,-10];
const capital = [-15,-15,-10,-5,-3,-3,-5,0,0,1,0,1,1,2,1,-15];
const reactions = [
    permissive, permissive, permissive,
    [0,0,0,2,1,0,0,0,-1,-1,-1,-1,-1,-1,-1,0],
    [-1,-1,0,1,2,0,0,0,-1,-1,-1,-1,-1,-1,-1,-1],
    [-2,-2,-1,0,1,2,0,0,0,-1,-1,-1,-1,-1,-1,-2],
    [-2,-2,-1,0,1,0,2,0,0,-1,-1,-1,-1,-1,-1,-2],
    [-1,-1,0,0,0,0,0,2,1,1,1,0,0,-1,1,-1],
    [-1,-1,0,0,0,0,0,1,2,1,1,0,0,-1,1,-1],
    [-5,-5,-3,-2,-1,-1,-2,1,1,2,1,1,1,0,1,-5],
    [-5,-5,-3,-2,-1,-1,-2,1,1,1,2,1,1,0,1,-5],
    serious, [-10,-10,-7,-3,-2,-2,-3,0,1,1,0,1,2,1,1,-10],
    capital, [-15,-15,-10,-5,-3,-3,-5,0,0,1,0,1,1,1,2,-15],
    permissive,
];

export interface EthicsNation {
    id: number;
    name: string;
    race: string;
    responses: Record<string, string>;
}

export function ethicsSource(nation: EthicsNation) {
    if (Object.keys(nation.responses).length) return 'World export';
    return races.includes(nation.race.toLowerCase()) ? 'Vanilla estimate' : 'Unknown';
}

function policy(nation: EthicsNation, key: string, defaults: number[]) {
    if (ethicsSource(nation) === 'World export') return responses.indexOf(nation.responses[key]);
    return defaults[races.indexOf(nation.race.toLowerCase())] ?? -1;
}

export function compareEthics(nation: EthicsNation, other?: EthicsNation) {
    return issues.map(([key, issue, defaults]) => {
        const first = policy(nation, key, defaults);
        const second = other ? policy(other, key, defaults) : -1;
        const comparable = first > 0 && second > 0;
        return { key, issue, policy: labels[first] ?? 'Unknown', otherPolicy: labels[second] ?? 'Unknown',
            reaction: comparable ? reactions[first - 1][second - 1] : null,
            otherReaction: comparable ? reactions[second - 1][first - 1] : null };
    });
}

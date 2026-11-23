import { expect, it } from 'vitest';
import { compareEthics, ethicsSource, type EthicsNation } from '../utils/civilizationEthics';

const dwarf: EthicsNation = { id: 1, name: 'Mountainhome', race: 'DWARF', responses: {} };
const human: EthicsNation = { id: 2, name: 'Kingdom', race: 'human', responses: {} };

it('distinguishes each observer’s reaction to slavery', () => {
    const rows = compareEthics(dwarf, human);
    expect(rows).toHaveLength(22);
    expect(rows[0]).toMatchObject({ key: 'SLAVERY', policy: 'Capital offense', otherPolicy: 'Permitted',
        reaction: -15, otherReaction: -5 });
});

it('prefers the actual world and leaves gaps and unrecognized civilizations unknown', () => {
    const custom = { ...dwarf, responses: { SLAVERY: 'ACCEPTABLE' } };
    expect(ethicsSource(custom)).toBe('World export');
    expect(compareEthics(custom, human)[0].reaction).toBe(1);
    expect(compareEthics(custom, human)[1].reaction).toBeNull();
    expect(compareEthics({ ...human, race: 'rodent man' })[0].policy).toBe('Unknown');
});

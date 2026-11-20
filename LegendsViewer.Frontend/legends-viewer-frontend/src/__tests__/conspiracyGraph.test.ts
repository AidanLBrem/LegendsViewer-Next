import { describe, expect, it } from 'vitest';
import { conspiracyGraph } from '../utils/conspiracyGraph';
import type { Conspiracy } from '../types/conspiracy';

const plot: Conspiracy = {
  key: '10:0', localId: 0, owner: { id: 10, name: 'Owner', path: '/hf/10' },
  objective: 'assassinate actor', status: 'On hold', entity: null, artifact: null,
  civilizations: [], parent: null, delegated: { figureId: 20, localId: 0, resolved: false }, fields: [],
  participants: [{ actorId: 0, role: 'Target', actorRole: 'enemy', isTarget: true, strategy: 'neutralize',
    figure: { id: 11, name: 'Target', path: '/hf/11' }, entity: null, strategyEntity: null,
    strategyPositionId: -1, handleActorId: -1, agreementId: -1, agreementHasMessenger: false,
    promisedActorImmortality: false, promisedMeImmortality: false, fields: [] }],
};

describe('conspiracy network', () => {
  it('links resolved figures, marks targets and leaves missing plots unlinked', () => {
    const data = conspiracyGraph(plot).map(e => e.data);
    expect(data.find(n => n.id === '/hf/11')).toMatchObject({ path: '/hf/11', kind: 'target' });
    expect(data.find(n => n.id === 'plot-20-0')?.path).toBeNull();
    expect(data.find(n => n.source === 'plot' && n.target === '/hf/11')?.label).toBe('Target');
  });
  it('uses distinct unresolved actors and keeps every edge endpoint present', () => {
    const data = conspiracyGraph({ ...plot, participants: [
      { ...plot.participants[0]!, figure: null, actorId: 1 },
      { ...plot.participants[0]!, figure: null, actorId: 2 },
    ] }).map(e => e.data);
    const ids = new Set(data.filter(e => !e.source).map(e => e.id));
    expect(ids.has('actor-1')).toBe(true); expect(ids.has('actor-2')).toBe(true);
    for (const edge of data.filter(e => e.source)) {
      expect(ids.has(edge.source)).toBe(true); expect(ids.has(edge.target)).toBe(true);
    }
  });
});

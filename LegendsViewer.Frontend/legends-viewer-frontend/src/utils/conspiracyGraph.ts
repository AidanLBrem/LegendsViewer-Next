import type { ElementDefinition } from 'cytoscape';
import type { Conspiracy, IntrigueReference } from '../types/conspiracy';

export function conspiracyGraph(plot: Conspiracy): ElementDefinition[] {
  const nodes = new Map<string, ElementDefinition>();
  const edges: ElementDefinition[] = [];
  nodes.set('plot', { data: { id: 'plot', label: plot.objective, kind: 'plot' } });
  function add(reference: IntrigueReference | null, fallback: string, label: string, kind: string) {
    const id = reference?.path ?? fallback;
    nodes.set(id, { data: { id, label: reference?.name ?? label, path: reference?.path, kind } });
    edges.push({ data: { id: `edge-${edges.length}`, source: 'plot', target: id, label } });
  }
  add(plot.owner, 'owner', 'Recorded under', 'figure');
  if (plot.entity) add(plot.entity, `entity-${plot.entity.id}`, 'Associated entity', 'entity');
  if (plot.artifact) add(plot.artifact, `artifact-${plot.artifact.id}`, 'Artifact', 'artifact');
  for (const actor of plot.participants) {
    add(actor.figure, `actor-${actor.actorId}`, actor.role, actor.isTarget ? 'target' : 'figure');
    if (actor.entity) add(actor.entity, `entity-${actor.entity.id}`, `Actor ${actor.actorId} entity`, 'entity');
    if (actor.strategyEntity) add(actor.strategyEntity, `entity-${actor.strategyEntity.id}`, actor.strategy ?? 'Strategy entity', 'entity');
  }
  for (const [label, related] of [['Parent plot', plot.parent], ['Delegated plot', plot.delegated]] as const) {
    if (related) add({ id: related.localId, name: `${label} ${related.figureId}:${related.localId}`,
      path: related.resolved ? `/conspiracies/${related.figureId}/${related.localId}` : null },
      `plot-${related.figureId}-${related.localId}`, label, 'plot');
  }
  return [...nodes.values(), ...edges];
}

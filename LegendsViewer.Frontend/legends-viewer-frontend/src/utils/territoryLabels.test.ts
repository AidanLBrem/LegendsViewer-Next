import { describe, expect, it } from 'vitest';
import { layoutTerritoryLabels } from './territoryLabels';

function layout(width: number, height: number, nation: number[], water = new Uint8Array(Math.ceil(width * height / 8))) {
  return layoutTerritoryLabels({width, height, nation: Int32Array.from(nation), colors: new Map()},
    width, height, water, new Map([[1, 'The Copper Halls']]), () => 8);
}

describe('territory names', () => {
  it('grows and shrinks with visible territory', () => {
    const small = layout(10, 10, Array.from({length: 100}, (_, i) => i % 10 < 4 ? 1 : -1));
    const large = layout(10, 10, Array(100).fill(1));
    expect(small).toHaveLength(1);
    expect(large[0].fontSize).toBeGreaterThan(small[0].fontSize);
    expect(layout(10, 10, Array(100).fill(-1))).toEqual([]);
  });
  it('fits the full name on one island and excludes the water between islands', () => {
    const width = 14, height = 6;
    const water = new Uint8Array(Math.ceil(width * height / 8));
    for (let i = 0; i < width * height; i++) {
      if (i % width >= 3 && i % width < 7) water[i >> 3] |= 1 << (i & 7);
    }
    const labels = layout(width, height, Array(width * height).fill(1), water);
    expect(labels).toHaveLength(1);
    expect(labels[0].x - 4 * labels[0].fontSize).toBeGreaterThanOrEqual(7);
    expect(labels[0].x + 4 * labels[0].fontSize).toBeLessThanOrEqual(width);
  });
});

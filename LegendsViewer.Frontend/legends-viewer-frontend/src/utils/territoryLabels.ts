import { getTerritoryNationAtTile, type TerritoryGrid } from './historicalAtlas';

export interface TerritoryLabel {
  name: string;
  x: number;
  y: number;
  fontSize: number;
}

// Find a rectangle wholly inside the owner's visible land, including on irregular
// coasts. Each bar is the consecutive height of a single owner's column.
export function layoutTerritoryLabels(grid: TerritoryGrid, width: number, height: number,
  water: Uint8Array, names: ReadonlyMap<number, string>, measureEm: (name: string) => number): TerritoryLabel[] {
  const entries = new Map([...names].filter(([, name]) => name.trim()).map(([id, name]) =>
    [id, { name, em: Math.max(1, measureEm(name)), tiles: 0, fit: 0, area: 0, x: 0, y: 0 }]));
  const owners = new Int32Array(width).fill(-1);
  const heights = new Int32Array(width);
  const stack = new Int32Array(width);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const owner = getTerritoryNationAtTile(grid, width, height, x, y, water) ?? -1;
      heights[x] = owner === owners[x] ? heights[x] + 1 : 1;
      owners[x] = owner;
    }
    for (let start = 0; start < width;) {
      let end = start + 1;
      while (end < width && owners[end] === owners[start]) end++;
      const entry = entries.get(owners[start]);
      if (entry) {
        entry.tiles += end - start;
        let count = 0;
        for (let x = start; x <= end; x++) {
          while (count && (x === end || heights[stack[count - 1]] >= heights[x])) {
            const h = heights[stack[--count]];
            const left = count ? stack[count - 1] + 1 : start;
            const w = x - left;
            const fit = Math.min(w * 0.88 / entry.em, h * 0.76);
            if (fit > entry.fit || (fit === entry.fit && w * h > entry.area)) {
              entry.fit = fit;
              entry.area = w * h;
              entry.x = left + w / 2;
              entry.y = y + 1 - h / 2;
            }
          }
          if (x < end) stack[count++] = x;
        }
      }
      start = end;
    }
  }
  return [...entries.values()].filter(entry => entry.tiles > 0).map(entry => ({
    name: entry.name, x: entry.x, y: entry.y,
    fontSize: Math.min(entry.fit, 0.19 * Math.sqrt(entry.tiles)),
  }));
}

export function territoryLabelSvg(grid: TerritoryGrid, width: number, height: number,
  water: Uint8Array, names: ReadonlyMap<number, string>): SVGSVGElement {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
  svg.setAttribute('preserveAspectRatio', 'none');
  svg.setAttribute('aria-hidden', 'true');
  svg.classList.add('territory-names');
  const ctx = document.createElement('canvas').getContext('2d')!;
  ctx.font = 'bold 100px Georgia, serif';
  const labels = layoutTerritoryLabels(grid, width, height, water, names, name => ctx.measureText(name).width / 100);
  for (const label of labels) {
    const text = document.createElementNS(ns, 'text');
    text.setAttribute('x', String(label.x));
    text.setAttribute('y', String(label.y));
    text.setAttribute('font-size', String(label.fontSize));
    text.textContent = label.name;
    svg.appendChild(text);
  }
  return svg;
}

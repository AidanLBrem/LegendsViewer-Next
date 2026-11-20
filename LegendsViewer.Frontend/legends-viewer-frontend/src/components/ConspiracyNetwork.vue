<template>
  <div>
    <p class="text-caption">Scroll to zoom, drag to pan, select a node to open its record. Targets are red. The details below provide the same links.</p>
    <div ref="container" class="network" role="img" :aria-label="`Network for ${plot.objective}`"></div>
  </div>
</template>
<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue';
import { useRouter } from 'vue-router';
import cytoscape, { type Core } from 'cytoscape';
import type { Conspiracy } from '../types/conspiracy';
import { conspiracyGraph } from '../utils/conspiracyGraph';
const props = defineProps<{ plot: Conspiracy }>();
const container = ref<HTMLDivElement>();
const router = useRouter();
let graph: Core | undefined;
let resize: ResizeObserver | undefined;
watch([() => props.plot, container], () => {
  graph?.destroy(); resize?.disconnect();
  if (!container.value) return;
  graph = cytoscape({ container: container.value, elements: conspiracyGraph(props.plot),
    layout: { name: 'circle', padding: 65 }, minZoom: 0.15, maxZoom: 3,
    style: [
      { selector: 'node', style: { label: 'data(label)', 'background-color': '#4987ad', color: '#999', 'text-wrap': 'wrap', 'text-max-width': '140px', 'font-size': 12, 'text-valign': 'bottom', 'text-margin-y': 6 } },
      { selector: 'node[kind="plot"]', style: { 'background-color': '#b28a3d', shape: 'diamond' } },
      { selector: 'node[kind="target"]', style: { 'background-color': '#ce5661' } },
      { selector: 'node[kind="entity"]', style: { shape: 'rectangle' } },
      { selector: 'edge', style: { label: 'data(label)', 'font-size': 10, color: '#999', 'line-color': '#777', 'target-arrow-color': '#777', 'target-arrow-shape': 'triangle', 'curve-style': 'bezier', 'text-rotation': 'autorotate' } },
    ] });
  graph.on('tap', 'node', event => { const path = event.target.data('path'); if (path) router.push(path); });
  resize = new ResizeObserver(() => graph?.resize()); resize.observe(container.value);
}, { flush: 'post', immediate: true });
onBeforeUnmount(() => { resize?.disconnect(); graph?.destroy(); });
</script>
<style scoped>.network { height: 420px; width: 100%; border: 1px solid #7775; border-radius: 8px; }</style>

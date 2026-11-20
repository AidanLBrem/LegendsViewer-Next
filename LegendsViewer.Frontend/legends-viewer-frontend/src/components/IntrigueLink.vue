<template>
  <router-link v-if="reference?.path" :to="reference.path">{{ reference.name }}</router-link>
  <span v-else>{{ reference?.name ?? 'Not recorded' }}</span>
</template>
<script setup lang="ts">
import type { IntrigueReference } from '../types/conspiracy';
defineProps<{ reference: IntrigueReference | null }>();
</script>

<template>
  <ul class="ml-5">
    <li v-for="(field, index) in fields" :key="index">
      <strong>{{ field.name }}</strong>: {{ field.value || (field.children.length ? '' : '(present)') }}
      <IntrigueFields v-if="field.children.length" :fields="field.children" />
    </li>
  </ul>
</template>
<script setup lang="ts">
import type { IntrigueField } from '../types/conspiracy';
defineProps<{ fields: IntrigueField[] }>();
</script>

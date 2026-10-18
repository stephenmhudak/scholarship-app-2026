<script setup>
import { computed } from 'vue'

const props = defineProps({
  answered: {
    type: Number,
    required: true,
  },
  total: {
    type: Number,
    required: true,
  },
})

const percentage = computed(() =>
  props.total > 0 ? Math.round((props.answered / props.total) * 100) : 0
)
</script>

<template>
  <div class="space-y-1">
    <div class="flex justify-between gap-3 text-sm">
      <span class="font-semibold text-navy">Progress</span>
      <span class="text-slate-400"><slot>{{ answered }} of {{ total }} answered</slot></span>
    </div>
    <v-progress-linear :model-value="percentage" color="primary" bg-color="#E9EDF7" bg-opacity="1" height="6" rounded />
  </div>
</template>

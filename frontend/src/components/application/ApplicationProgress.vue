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
  <div class="card p-4 space-y-2">
    <div class="flex justify-between text-sm">
      <span class="font-semibold text-navy">Progress</span>
      <span class="text-slate-400">{{ answered }} / {{ total }} answered ({{ percentage }}%)</span>
    </div>
    <div class="w-full bg-[#E9EDF7] rounded-full h-2">
      <div
        class="bg-primary h-2 rounded-full transition-all duration-500"
        :style="{ width: `${percentage}%` }"
      ></div>
    </div>
  </div>
</template>

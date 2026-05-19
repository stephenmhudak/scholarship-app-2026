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
    <div class="flex justify-between text-sm text-gray-600">
      <span>Progress</span>
      <span>{{ answered }} / {{ total }} questions answered ({{ percentage }}%)</span>
    </div>
    <div class="w-full bg-gray-200 rounded-full h-2.5">
      <div
        class="bg-blue-600 h-2.5 rounded-full transition-all duration-500"
        :style="{ width: `${percentage}%` }"
      ></div>
    </div>
  </div>
</template>

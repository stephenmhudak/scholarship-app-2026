<script setup>
defineProps({
  score: {
    type: Object,
    required: true,
  },
})
</script>

<template>
  <div class="bg-white border border-gray-200 rounded-xl p-5 space-y-3">
    <div class="flex items-center justify-between">
      <div>
        <p class="text-sm text-gray-500">Reviewer</p>
        <p class="font-semibold text-gray-900">{{ score.reviewer_name || 'Anonymous' }}</p>
      </div>
      <div class="text-right">
        <p class="text-sm text-gray-500">Score</p>
        <p class="text-3xl font-bold text-blue-600">{{ score.score }}</p>
      </div>
    </div>

    <div v-if="score.section" class="text-sm text-gray-500">
      Section: <span class="font-medium text-gray-700">{{ score.section }}</span>
    </div>

    <div v-if="score.comments" class="pt-2 border-t border-gray-100">
      <p class="text-sm font-medium text-gray-700 mb-1">Comments</p>
      <p class="text-sm text-gray-600 whitespace-pre-wrap">{{ score.comments }}</p>
    </div>

    <div v-if="score.created_at" class="text-xs text-gray-400">
      Submitted {{ new Date(score.created_at).toLocaleDateString() }}
    </div>
  </div>
</template>

<script setup>
defineProps({
  score: {
    type: Object,
    required: true,
  },
})
</script>

<template>
  <v-card class="pa-5 space-y-3">
    <div class="flex items-center justify-between">
      <div>
        <p class="text-xs font-bold text-slate-400 uppercase tracking-wide">Reviewer</p>
        <p class="font-semibold text-navy mt-0.5">{{ score.reviewer_name || 'Anonymous' }}</p>
      </div>
      <div class="text-right">
        <p class="text-xs font-bold text-slate-400 uppercase tracking-wide">Score</p>
        <p class="text-3xl font-bold text-primary mt-0.5">{{ score.score }}</p>
      </div>
    </div>

    <div v-if="score.section" class="text-sm text-slate-400">
      Section: <span class="font-semibold text-navy">{{ score.section }}</span>
    </div>

    <v-divider v-if="score.comments" />
    <div v-if="score.comments">
      <p class="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1.5">Comments</p>
      <p class="text-sm text-navy whitespace-pre-wrap">{{ score.comments }}</p>
    </div>

    <div v-if="score.created_at" class="text-xs text-slate-400">
      Submitted {{ new Date(score.created_at).toLocaleDateString() }}
    </div>
  </v-card>
</template>

<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  sections: {
    type: Array,
    default: () => [{ id: null, label: 'Overall Score' }],
  },
  existingScores: {
    type: Array,
    default: () => [],
  },
  saving: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['save'])

function buildScores() {
  return props.sections.map((s) => {
    const existing = props.existingScores.find((e) => e.sectionId === s.id)
    return {
      sectionId: s.id,
      label: s.label,
      score: existing?.scoreValue ?? null,
      comments: existing?.comments ?? '',
    }
  })
}

const scores = ref(buildScores())

watch(
  () => [props.sections, props.existingScores],
  () => { scores.value = buildScores() },
  { deep: true },
)

function onSave() {
  const filled = scores.value.filter((s) => s.score !== '' && s.score != null)
  if (!filled.length) return
  emit('save', {
    sectionScores: filled.map((s) => ({
      sectionId: s.sectionId,
      score: Number(s.score),
      comments: s.comments || null,
    })),
  })
}
</script>

<template>
  <div class="space-y-5">
    <v-card
      v-for="(section, idx) in scores"
      :key="section.sectionId ?? 'overall'"
      class="pa-5 space-y-4"
    >
      <h3 class="text-sm font-bold text-navy">{{ section.label }}</h3>

      <v-number-input
        v-model="scores[idx].score"
        label="Score (1–100)"
        :min="1"
        :max="100"
        control-variant="split"
        class="max-w-[14rem]"
        hide-details
      />

      <v-textarea
        v-model="scores[idx].comments"
        label="Comments"
        rows="3"
        placeholder="Enter your comments…"
        hide-details
      />
    </v-card>

    <div class="flex justify-end">
      <v-btn color="primary" prepend-icon="mdi-content-save-outline" :loading="saving" @click="onSave">
        Save Scores
      </v-btn>
    </div>
  </div>
</template>

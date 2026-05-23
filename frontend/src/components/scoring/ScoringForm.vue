<script setup>
import { ref, watch } from 'vue'
import BaseButton from '../common/BaseButton.vue'

const props = defineProps({
  sections: {
    type: Array,
    default: () => [{ id: null, label: 'Overall Score' }],
  },
  existingScores: {
    type: Array,
    default: () => [],
  },
  submitting: {
    type: Boolean,
    default: false,
  },
  saving: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['save', 'submit'])

function buildScores() {
  return props.sections.map((s) => {
    const existing = props.existingScores.find((e) => e.sectionId === s.id)
    return {
      sectionId: s.id,
      label: s.label,
      score: existing?.scoreValue ?? '',
      comments: existing?.comments ?? '',
    }
  })
}

const scores = ref(buildScores())
const submitError = ref(null)

watch(
  () => [props.sections, props.existingScores],
  () => { scores.value = buildScores() },
  { deep: true },
)

function toPayload(rows) {
  return {
    sectionScores: rows.map((s) => ({
      sectionId: s.sectionId,
      score: Number(s.score),
      comments: s.comments || null,
    })),
  }
}

function onSave() {
  submitError.value = null
  const filled = scores.value.filter((s) => s.score !== '' && s.score != null)
  if (!filled.length) return
  emit('save', toPayload(filled))
}

function onSubmit() {
  submitError.value = null
  const unfilled = scores.value.filter((s) => s.score === '' || s.score == null)
  if (unfilled.length) {
    submitError.value = `Please enter scores for all sections before submitting (${unfilled.map((s) => s.label).join(', ')}).`
    return
  }
  emit('submit', toPayload(scores.value))
}
</script>

<template>
  <div class="space-y-5">
    <div
      v-for="(section, idx) in scores"
      :key="section.sectionId ?? 'overall'"
      class="card p-5 space-y-4"
    >
      <h3 class="text-sm font-bold text-navy">{{ section.label }}</h3>

      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-navy">
          Score <span class="text-slate-400 font-normal">(1–100)</span>
        </label>
        <input
          v-model.number="scores[idx].score"
          type="number"
          min="1"
          max="100"
          placeholder="Enter score…"
          class="form-input w-36"
        />
      </div>

      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-navy">Comments</label>
        <textarea
          v-model="scores[idx].comments"
          rows="3"
          placeholder="Enter your comments…"
          class="form-input resize-y"
        />
      </div>
    </div>

    <p v-if="submitError" class="text-sm text-danger font-medium">{{ submitError }}</p>

    <div class="flex items-center gap-3">
      <BaseButton type="button" variant="secondary" :loading="saving" :disabled="submitting" @click="onSave">
        <span class="mdi mdi-content-save-outline mr-1.5"></span>
        Save Progress
      </BaseButton>
      <BaseButton type="button" :loading="submitting" :disabled="saving" @click="onSubmit">
        <span class="mdi mdi-send mr-1.5"></span>
        Submit Score
      </BaseButton>
    </div>
  </div>
</template>

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
})

const emit = defineEmits(['submit'])

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

watch(
  () => [props.sections, props.existingScores],
  () => { scores.value = buildScores() },
  { deep: true },
)

const isEditing = ref(props.existingScores.length > 0)
watch(
  () => props.existingScores,
  (val) => { isEditing.value = val.length > 0 },
)

function onSubmit() {
  emit('submit', {
    sectionScores: scores.value.map((s) => ({
      sectionId: s.sectionId,
      score: Number(s.score),
      comments: s.comments || null,
    })),
  })
}
</script>

<template>
  <form @submit.prevent="onSubmit" class="space-y-5">
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
          required
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

    <BaseButton type="submit" :loading="submitting">
      <span :class="`mdi ${isEditing ? 'mdi-pencil' : 'mdi-send'} mr-1.5`"></span>
      {{ isEditing ? 'Update Score' : 'Submit Score' }}
    </BaseButton>
  </form>
</template>

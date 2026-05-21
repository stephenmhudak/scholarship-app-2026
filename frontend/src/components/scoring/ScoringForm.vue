<script setup>
import { ref } from 'vue'
import BaseButton from '../common/BaseButton.vue'

const props = defineProps({
  sections: {
    type: Array,
    default: () => [{ id: 'overall', label: 'Overall Score' }],
  },
  submitting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['submit'])

const scores = ref(
  Object.fromEntries(props.sections.map((s) => [s.id, { score: '', comments: '' }]))
)

function onSubmit() {
  emit('submit', { ...scores.value })
}
</script>

<template>
  <form @submit.prevent="onSubmit" class="space-y-5">
    <div
      v-for="section in sections"
      :key="section.id"
      class="card p-5 space-y-4"
    >
      <h3 class="text-sm font-bold text-navy">{{ section.label }}</h3>

      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-navy">
          Score <span class="text-slate-400 font-normal">(1–100)</span>
        </label>
        <input
          v-model.number="scores[section.id].score"
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
          v-model="scores[section.id].comments"
          rows="3"
          placeholder="Enter your comments…"
          class="form-input resize-y"
        />
      </div>
    </div>

    <BaseButton type="submit" :loading="submitting">
      <span class="mdi mdi-send mr-1.5"></span>
      Submit Score
    </BaseButton>
  </form>
</template>

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
  <form @submit.prevent="onSubmit" class="space-y-6">
    <div
      v-for="section in sections"
      :key="section.id"
      class="bg-gray-50 border border-gray-200 rounded-xl p-5 space-y-3"
    >
      <h3 class="font-semibold text-gray-800">{{ section.label }}</h3>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">
          Score <span class="text-gray-400">(1–100)</span>
        </label>
        <input
          v-model.number="scores[section.id].score"
          type="number"
          min="1"
          max="100"
          required
          placeholder="Enter score..."
          class="block w-32 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Comments</label>
        <textarea
          v-model="scores[section.id].comments"
          rows="3"
          placeholder="Enter your comments..."
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
        />
      </div>
    </div>

    <BaseButton type="submit" :loading="submitting">
      <span class="mdi mdi-send mr-1"></span>
      Submit Score
    </BaseButton>
  </form>
</template>

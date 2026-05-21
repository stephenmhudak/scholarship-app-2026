<script setup>
import { ref, onMounted } from 'vue'
import BaseModal from '../common/BaseModal.vue'
import BaseButton from '../common/BaseButton.vue'
import api from '../../services/api'

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  applicationId: {
    type: [String, Number],
    required: true,
  },
})

const emit = defineEmits(['close', 'assigned'])

const scorers = ref([])
const selectedScorerId = ref(null)
const loading = ref(false)
const error = ref(null)

onMounted(async () => {
  try {
    const response = await api.get('/admin/scorers')
    scorers.value = response.data
  } catch {
    error.value = 'Failed to load scorers.'
  }
})

async function confirm() {
  if (!selectedScorerId.value) {
    error.value = 'Please select a scorer.'
    return
  }

  loading.value = true
  error.value = null

  try {
    emit('assigned', { applicationId: props.applicationId, scorerId: selectedScorerId.value })
    emit('close')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <BaseModal :show="show" title="Assign Scorer" @close="emit('close')">
    <div class="space-y-4">
      <div class="space-y-1.5">
        <label class="block text-sm font-semibold text-navy">Select Scorer</label>
        <select
          v-model="selectedScorerId"
          class="form-select w-full"
        >
          <option value="" disabled>— Choose a scorer —</option>
          <option
            v-for="scorer in scorers"
            :key="scorer.id"
            :value="scorer.id"
          >
            {{ scorer.firstName }} {{ scorer.lastName }} ({{ scorer.email }})
          </option>
        </select>
      </div>

      <p v-if="error" class="text-sm font-medium text-danger">{{ error }}</p>

      <div class="flex justify-end gap-3 pt-2">
        <BaseButton variant="secondary" @click="emit('close')">Cancel</BaseButton>
        <BaseButton :loading="loading" @click="confirm">Assign Scorer</BaseButton>
      </div>
    </div>
  </BaseModal>
</template>

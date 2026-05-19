<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const settings = ref({
  cycle_name: '',
  deadline: '',
  is_active: false,
})
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)

onMounted(async () => {
  try {
    const response = await api.get('/admin/settings')
    settings.value = response.data
  } catch {
    // Use defaults if no settings exist yet
  } finally {
    loading.value = false
  }
})

async function save() {
  saving.value = true
  alert.value = null
  try {
    await api.put('/admin/settings', settings.value)
    alert.value = { type: 'success', message: 'Settings saved successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to save settings.' }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-2xl">
    <div>
      <h1 class="text-2xl font-bold text-gray-900">Scholarship Settings</h1>
      <p class="text-gray-500 mt-1">Manage the scholarship cycle and application deadline.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading settings...
    </div>

    <template v-else>
      <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm space-y-5">
        <h2 class="text-lg font-semibold text-gray-900">Scholarship Cycle</h2>

        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">Cycle Name</label>
          <input
            v-model="settings.cycle_name"
            type="text"
            placeholder="e.g. 2026 Spring Scholarship"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">Application Deadline</label>
          <input
            v-model="settings.deadline"
            type="date"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div class="flex items-center gap-3">
          <input
            id="is_active"
            v-model="settings.is_active"
            type="checkbox"
            class="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
          />
          <label for="is_active" class="text-sm font-medium text-gray-700">
            Applications are currently open
          </label>
        </div>

        <BaseButton :loading="saving" @click="save">
          <span class="mdi mdi-content-save mr-1"></span>
          Save Settings
        </BaseButton>
      </div>
    </template>
  </div>
</template>

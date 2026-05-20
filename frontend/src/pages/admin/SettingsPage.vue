<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const cycles = ref([])
const editing = ref(null)
const creating = ref(false)
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)

const blank = () => ({
  name: '',
  openDate: '',
  closeDate: '',
  isActive: false,
})

onMounted(async () => {
  await loadCycles()
})

async function loadCycles() {
  loading.value = true
  try {
    const response = await api.get('/admin/cycles')
    cycles.value = response.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load cycles.' }
  } finally {
    loading.value = false
  }
}

function startCreate() {
  creating.value = true
  editing.value = blank()
}

function editCycle(cycle) {
  creating.value = false
  editing.value = {
    id: cycle.id,
    name: cycle.name,
    openDate: cycle.openDate?.slice(0, 10) ?? '',
    closeDate: cycle.closeDate?.slice(0, 10) ?? '',
    isActive: cycle.isActive,
  }
}

function cancel() {
  editing.value = null
  creating.value = false
}

async function save() {
  saving.value = true
  alert.value = null
  try {
    const payload = {
      name: editing.value.name,
      openDate: editing.value.openDate,
      closeDate: editing.value.closeDate,
      isActive: editing.value.isActive,
    }

    if (creating.value) {
      await api.post('/admin/cycles', payload)
      alert.value = { type: 'success', message: 'Cycle created.' }
    } else {
      await api.put(`/admin/cycles/${editing.value.id}`, payload)
      alert.value = { type: 'success', message: 'Cycle updated.' }
    }

    editing.value = null
    creating.value = false
    await loadCycles()
  } catch {
    alert.value = { type: 'error', message: 'Failed to save cycle.' }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-2xl">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">Scholarship Settings</h1>
        <p class="text-gray-500 mt-1">Manage scholarship cycles and application windows.</p>
      </div>
      <BaseButton v-if="!editing" @click="startCreate">
        <span class="mdi mdi-plus mr-1"></span>
        New Cycle
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Edit / Create Form -->
    <div v-if="editing" class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm space-y-5">
      <h2 class="text-lg font-semibold text-gray-900">
        {{ creating ? 'Create Cycle' : 'Edit Cycle' }}
      </h2>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Cycle Name</label>
        <input
          v-model="editing.name"
          type="text"
          placeholder="e.g. 2026 Spring Scholarship"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div class="grid grid-cols-2 gap-4">
        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">Open Date</label>
          <input
            v-model="editing.openDate"
            type="date"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">Close Date</label>
          <input
            v-model="editing.closeDate"
            type="date"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      <div class="flex items-center gap-3">
        <input
          id="is_active"
          v-model="editing.isActive"
          type="checkbox"
          class="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
        />
        <label for="is_active" class="text-sm font-medium text-gray-700">
          Active (applications open)
        </label>
      </div>

      <div class="flex gap-3 pt-2">
        <BaseButton :loading="saving" @click="save">
          <span class="mdi mdi-content-save mr-1"></span>
          Save
        </BaseButton>
        <BaseButton variant="secondary" @click="cancel">Cancel</BaseButton>
      </div>
    </div>

    <!-- Cycles List -->
    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading cycles...
    </div>

    <div v-else-if="cycles.length === 0 && !editing" class="bg-white rounded-xl border border-gray-200 p-8 text-center shadow-sm">
      <p class="text-gray-500 text-sm">No scholarship cycles yet. Create one to get started.</p>
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="cycle in cycles"
        :key="cycle.id"
        class="bg-white rounded-xl border border-gray-200 p-4 shadow-sm flex items-center justify-between"
      >
        <div>
          <div class="flex items-center gap-2">
            <p class="text-sm font-semibold text-gray-900">{{ cycle.name }}</p>
            <span
              v-if="cycle.isActive"
              class="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-700 font-medium"
            >
              Active
            </span>
          </div>
          <p class="text-xs text-gray-500 mt-0.5">
            {{ new Date(cycle.openDate).toLocaleDateString() }} –
            {{ new Date(cycle.closeDate).toLocaleDateString() }}
          </p>
        </div>
        <BaseButton variant="secondary" size="sm" @click="editCycle(cycle)">
          <span class="mdi mdi-pencil mr-1"></span>
          Edit
        </BaseButton>
      </div>
    </div>
  </div>
</template>

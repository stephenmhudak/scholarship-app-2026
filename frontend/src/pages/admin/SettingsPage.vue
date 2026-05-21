<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseInput from '../../components/common/BaseInput.vue'

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
        <h1 class="page-title">Scholarship Settings</h1>
        <p class="page-subtitle">Manage scholarship cycles and application windows.</p>
      </div>
      <BaseButton v-if="!editing" @click="startCreate">
        <span class="mdi mdi-plus mr-1.5"></span>
        New Cycle
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Edit / Create Form -->
    <div v-if="editing" class="section-card space-y-5">
      <h2 class="text-base font-bold text-navy">
        {{ creating ? 'Create Cycle' : 'Edit Cycle' }}
      </h2>

      <BaseInput
        v-model="editing.name"
        label="Cycle Name"
        placeholder="e.g. 2026 Spring Scholarship"
      />

      <div class="grid grid-cols-2 gap-4">
        <BaseInput
          v-model="editing.openDate"
          type="date"
          label="Open Date"
        />
        <BaseInput
          v-model="editing.closeDate"
          type="date"
          label="Close Date"
        />
      </div>

      <label class="flex items-center gap-3 cursor-pointer">
        <input
          id="is_active"
          v-model="editing.isActive"
          type="checkbox"
          class="w-4 h-4 rounded border-[#E9EDF7] text-primary focus:ring-primary/30"
        />
        <span class="text-sm font-semibold text-navy">Active (applications open)</span>
      </label>

      <div class="flex gap-3 pt-2">
        <BaseButton :loading="saving" @click="save">
          <span class="mdi mdi-content-save mr-1.5"></span>
          Save
        </BaseButton>
        <BaseButton variant="secondary" @click="cancel">Cancel</BaseButton>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading cycles…
    </div>

    <!-- Empty state -->
    <div v-else-if="cycles.length === 0 && !editing" class="section-card text-center py-10">
      <span class="mdi mdi-calendar-blank-outline text-4xl text-slate-400 mb-2 block"></span>
      <p class="text-slate-400 text-sm">No scholarship cycles yet. Create one to get started.</p>
    </div>

    <!-- Cycles list -->
    <div v-else class="space-y-3">
      <div
        v-for="cycle in cycles"
        :key="cycle.id"
        class="card p-4 flex items-center justify-between"
      >
        <div>
          <div class="flex items-center gap-2">
            <p class="text-sm font-bold text-navy">{{ cycle.name }}</p>
            <span
              v-if="cycle.isActive"
              class="text-xs px-2 py-0.5 rounded-full bg-success-light text-success-dark font-semibold border border-success/20"
            >
              Active
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-0.5">
            {{ new Date(cycle.openDate).toLocaleDateString() }} –
            {{ new Date(cycle.closeDate).toLocaleDateString() }}
          </p>
        </div>
        <div class="flex gap-2">
          <RouterLink
            :to="`/admin/cycles/${cycle.id}/questions`"
            class="inline-flex items-center text-sm font-semibold border border-[#E9EDF7] bg-white text-navy px-3 py-1.5 rounded-xl hover:bg-[#F4F7FE] transition-colors"
          >
            <span class="mdi mdi-format-list-bulleted mr-1.5"></span>
            Questions
          </RouterLink>
          <BaseButton variant="secondary" size="sm" @click="editCycle(cycle)">
            <span class="mdi mdi-pencil mr-1.5"></span>
            Edit
          </BaseButton>
        </div>
      </div>
    </div>
  </div>
</template>

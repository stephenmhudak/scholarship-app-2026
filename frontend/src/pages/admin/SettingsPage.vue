<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../../services/api'
import DateField from '../../components/common/DateField.vue'

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
  copyFromCycleId: '',
})

const copyFromItems = computed(() => [
  { title: 'The default application questions', value: '' },
  ...cycles.value.map((c) => ({ title: c.name, value: c.id })),
])

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

function startCreate(source = null) {
  creating.value = true
  editing.value = blank()
  if (source) {
    editing.value.name = `${source.name} (copy)`
    editing.value.copyFromCycleId = source.id
  }
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
      await api.post('/admin/cycles', { ...payload, copyFromCycleId: editing.value.copyFromCycleId || null })
      alert.value = { type: 'success', message: 'Application period created.' }
    } else {
      await api.put(`/admin/cycles/${editing.value.id}`, payload)
      alert.value = { type: 'success', message: 'Application period updated.' }
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
        <h1 class="page-title">Application Periods</h1>
        <p class="page-subtitle">Each application period has its own dates, application form, scholarships and references.</p>
      </div>
      <v-btn v-if="!editing" color="primary" prepend-icon="mdi-plus" @click="startCreate()">
        New Application Period
      </v-btn>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Edit / Create Form -->
    <v-card v-if="editing" :title="creating ? 'New Application Period' : 'Edit Application Period'">
      <v-card-text class="pa-6 space-y-5">
        <v-text-field
          v-model="editing.name"
          label="Name"
          placeholder="e.g. 2026–27 Scholarship Application"
          hide-details="auto"
        />

        <div class="grid grid-cols-2 gap-4">
          <DateField v-model="editing.openDate" label="Open Date" hide-details="auto" />
          <DateField v-model="editing.closeDate" label="Close Date" hide-details="auto" />
        </div>

        <v-select
          v-if="creating"
          v-model="editing.copyFromCycleId"
          :items="copyFromItems"
          label="Start with questions from"
          hint="Copies its sections, questions, scholarships, references and letters. Applications, scores and awards aren't copied."
          persistent-hint
        />

        <v-switch
          v-model="editing.isActive"
          label="Accepting applications"
          hint="Applicants can apply between the open and close dates while this is on."
          persistent-hint
        />

        <div class="flex gap-3 pt-2">
          <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="save">
            Save
          </v-btn>
          <v-btn variant="outlined" @click="cancel">Cancel</v-btn>
        </div>
      </v-card-text>
    </v-card>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading cycles…
    </div>

    <!-- Empty state -->
    <v-card v-else-if="cycles.length === 0 && !editing">
      <v-empty-state
        icon="mdi-calendar-blank-outline"
        text="No application periods yet. Create one to get started."
      />
    </v-card>

    <!-- Cycles list -->
    <div v-else class="space-y-3">
      <v-card
        v-for="cycle in cycles"
        :key="cycle.id"
        class="pa-4 flex items-center justify-between"
      >
        <div>
          <div class="flex items-center gap-2">
            <p class="text-sm font-bold text-navy">{{ cycle.name }}</p>
            <v-chip v-if="cycle.isActive" color="success" class="font-semibold">Active</v-chip>
          </div>
          <p class="text-xs text-slate-400 mt-0.5">
            {{ new Date(cycle.openDate).toLocaleDateString() }} –
            {{ new Date(cycle.closeDate).toLocaleDateString() }}
          </p>
        </div>
        <div class="flex gap-2">
          <v-btn variant="outlined" size="small" prepend-icon="mdi-format-list-bulleted" :to="`/admin/cycles/${cycle.id}/questions`">
            Questions
          </v-btn>
          <v-btn variant="outlined" size="small" prepend-icon="mdi-content-copy" :disabled="!!editing" @click="startCreate(cycle)">
            Duplicate
          </v-btn>
          <v-btn variant="outlined" size="small" prepend-icon="mdi-pencil" @click="editCycle(cycle)">
            Edit
          </v-btn>
        </div>
      </v-card>
    </div>
  </div>
</template>

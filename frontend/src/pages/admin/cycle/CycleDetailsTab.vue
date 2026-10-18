<script setup>
import { ref, inject, watch } from 'vue'
import api from '../../../services/api'
import DateField from '../../../components/common/DateField.vue'

// The period's name, dates and whether it's accepting applications
const { cycle, reload } = inject('cycle')

const form = ref(null)
const saving = ref(false)
const alert = ref(null)

function reset() {
  const c = cycle.value
  form.value = c && {
    name: c.name,
    openDate: c.openDate?.slice(0, 10) ?? '',
    closeDate: c.closeDate?.slice(0, 10) ?? '',
    isActive: c.isActive,
  }
}
watch(cycle, reset, { immediate: true })

async function save() {
  saving.value = true
  alert.value = null
  try {
    await api.put(`/admin/cycles/${cycle.value.id}`, form.value)
    await reload()
    alert.value = { type: 'success', message: 'Application period saved.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to save the application period.' }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-2xl">
    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <div v-if="!form" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading…
    </div>

    <v-card v-else title="Details">
      <v-card-text class="pa-6 space-y-5">
        <v-text-field v-model="form.name" label="Name" placeholder="e.g. 2026–27 Scholarship Application" hide-details="auto" />

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <DateField v-model="form.openDate" label="Open Date" hide-details="auto" />
          <DateField v-model="form.closeDate" label="Close Date" hide-details="auto" />
        </div>

        <v-switch
          v-model="form.isActive"
          label="Accepting applications"
          hint="Applicants can apply between the open and close dates while this is on."
          persistent-hint
        />

        <div class="flex gap-3 pt-2">
          <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="save">Save</v-btn>
          <v-btn variant="outlined" :disabled="saving" @click="reset">Undo changes</v-btn>
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>

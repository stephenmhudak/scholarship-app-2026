<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../../services/api'
import { useConfirm } from '../../composables/useConfirm'

// The references an application needs and the links made for them. The applicant shares each link with the person
// writing that reference; they only ever see its status, never what was written.
const props = defineProps({
  applicationId: { type: String, required: true },
  // Dashboard summary: status only, with a link to the application to manage links
  compact: { type: Boolean, default: false },
  title: { type: String, default: 'References' },
})

const confirm = useConfirm()
const loading = ref(true)
const data = ref(null)
const alert = ref(null)
const labels = ref({})
const creating = ref(null)

const forms = computed(() => data.value?.forms ?? [])
const allReceived = computed(() => forms.value.every((f) => !f.required || f.received >= f.requiredCount))

async function refresh() {
  try {
    const res = await api.get(`/applications/${props.applicationId}/references`)
    data.value = res.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load references.' }
  } finally {
    loading.value = false
  }
}

defineExpose({ refresh })
onMounted(refresh)

function canAdd(form) {
  return data.value?.canRequest && form.required && form.requests.length < form.requiredCount
}

async function createLink(form) {
  creating.value = form.formId
  alert.value = null
  try {
    await api.post(`/applications/${props.applicationId}/references`, { formId: form.formId, label: labels.value[form.formId] || null })
    labels.value[form.formId] = ''
    await refresh()
    alert.value = { type: 'success', message: 'Link created. Copy it and send it to the person writing your reference.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to create the link.' }
  } finally {
    creating.value = null
  }
}

async function copyLink(request) {
  try {
    await navigator.clipboard.writeText(request.link)
    alert.value = { type: 'success', message: 'Link copied.' }
  } catch {
    alert.value = { type: 'info', message: 'Select the link and copy it manually.' }
  }
}

async function deleteLink(request) {
  if (!await confirm({
    title: 'Delete link',
    text: 'This link will stop working. If you already sent it, the person won\'t be able to submit their reference with it.',
    confirmText: 'Delete',
    color: 'error',
  })) return
  try {
    await api.delete(`/applications/${props.applicationId}/references/${request.id}`)
    await refresh()
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to delete the link.' }
  }
}

// Referees can type the code in on their My References page instead of using the link
function codeOf(request) {
  return request.link?.split('/r/')[1] ?? null
}

function requestName(form, request, i) {
  return request.label || (form.requiredCount > 1 ? `Reference ${i + 1}` : form.title)
}

function formatDate(value) {
  return value ? new Date(value).toLocaleDateString() : ''
}
</script>

<template>
  <v-card v-if="!loading && forms.length" :title="title">
    <template v-if="compact" #append>
      <v-chip :color="allReceived ? 'success' : 'warning'" class="font-semibold">
        {{ allReceived ? 'All received' : 'Waiting' }}
      </v-chip>
    </template>

    <v-card-text class="pa-6 space-y-5">
      <p v-if="!compact" class="text-sm text-slate-400">
        Create a link for each reference and send it to the person writing it. They'll sign in (or create a free
        referee account), fill it in and sign it online. It goes straight to the scholarship committee, so you won't
        see what they wrote. We'll email you as each one comes in. You can submit your application once every
        reference has been received.
      </p>

      <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

      <div v-for="form in forms" :key="form.formId" class="space-y-3">
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="text-sm font-bold text-navy">{{ form.title }}</p>
            <p v-if="form.description && !compact" class="text-xs text-slate-400">{{ form.description }}</p>
            <p v-if="!form.required" class="text-xs text-slate-400">No longer needed based on your answers.</p>
          </div>
          <v-chip :color="form.received >= form.requiredCount ? 'success' : 'warning'" class="font-semibold shrink-0">
            {{ form.received }} of {{ form.requiredCount }} received
          </v-chip>
        </div>

        <template v-if="!compact">
          <div
            v-for="(request, i) in form.requests"
            :key="request.id"
            class="rounded-lg border border-[#E9EDF7] pa-3 space-y-2"
          >
            <div class="flex items-center justify-between gap-2">
              <p class="text-sm font-semibold text-navy truncate">{{ requestName(form, request, i) }}</p>
              <v-chip v-if="request.status === 'submitted'" color="success" prepend-icon="mdi-check">
                Received {{ formatDate(request.submittedAt) }}
              </v-chip>
              <v-chip v-else color="warning">Waiting</v-chip>
            </div>
            <div v-if="request.status === 'pending'" class="flex items-center gap-2">
              <v-text-field
                v-if="request.link"
                :model-value="request.link"
                readonly
                density="compact"
                hide-details
                class="flex-1 font-mono"
                @focus="$event.target.select()"
              />
              <p v-else class="flex-1 text-xs text-slate-400">This link can no longer be shown. Delete it and create a new one.</p>
              <v-btn v-if="request.link" variant="outlined" prepend-icon="mdi-content-copy" @click="copyLink(request)">Copy</v-btn>
              <v-btn
                v-if="data.canRequest"
                icon="mdi-trash-can"
                variant="text"
                color="error"
                size="small"
                title="Delete link"
                @click="deleteLink(request)"
              />
            </div>
            <p v-if="request.status === 'pending' && codeOf(request)" class="text-xs text-slate-400">
              Or they can enter the code <span class="font-mono font-semibold text-navy">{{ codeOf(request) }}</span> after signing in.
            </p>
          </div>

          <div v-if="canAdd(form)" class="flex items-center gap-2">
            <v-text-field
              v-model="labels[form.formId]"
              label="Who is this reference from? (optional)"
              placeholder="e.g. Ms. Rivera"
              density="compact"
              hide-details
              class="flex-1"
              @keydown.enter.prevent="createLink(form)"
            />
            <v-btn color="primary" prepend-icon="mdi-link-plus" :loading="creating === form.formId" @click="createLink(form)">
              Create Link
            </v-btn>
          </div>
        </template>
      </div>

      <v-btn
        v-if="compact && data.canRequest"
        variant="outlined"
        color="primary"
        :to="`/application/${applicationId}`"
      >
        Manage references
      </v-btn>
    </v-card-text>
  </v-card>
</template>

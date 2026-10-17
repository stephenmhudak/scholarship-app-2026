<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import { useConfirm } from '../../composables/useConfirm'
import LetterEditorDialog from '../../components/letters/LetterEditorDialog.vue'
import { downloadPdf } from '../../utils/pdf'

// A cycle's award letters: base letters (one is the default every scholarship uses unless it has its own) and
// each scholarship's own letter. Awarders generate the PDFs from the Award Scholarships page.
const route = useRoute()
const confirm = useConfirm()
const cycleId = route.params.cycleId

const cycleName = ref('')
const baseLetters = ref([])
const scholarships = ref([])
const loading = ref(true)
const alert = ref(null)
// { title, letter, isBase, previewUrl, save } for the open editor
const editing = ref(null)
const editorOpen = computed({ get: () => !!editing.value, set: (v) => { if (!v) editing.value = null } })

async function load() {
  try {
    const [lettersRes, cyclesRes] = await Promise.all([api.get(`/cycles/${cycleId}/letters`), api.get('/admin/cycles')])
    baseLetters.value = lettersRes.data.baseLetters
    scholarships.value = lettersRes.data.scholarships
    cycleName.value = cyclesRes.data.find((c) => c.id === cycleId)?.name ?? ''
  } catch {
    alert.value = { type: 'error', message: 'Failed to load award letters.' }
  } finally {
    loading.value = false
  }
}

onMounted(load)

const defaultLetter = computed(() => baseLetters.value.find((l) => l.isDefault))
const downloading = ref(null)

// Every awardee's letter for a scholarship, in one PDF
async function downloadLetters(s) {
  downloading.value = s.id
  alert.value = null
  try {
    await downloadPdf(`/scholarships/${s.id}/letters`, `${s.name} - award letters.pdf`)
  } catch (err) {
    // Errors come back as a blob; show the API's message
    const text = err.response?.data instanceof Blob ? await err.response.data.text() : null
    let message = 'Failed to generate the letters.'
    try { message = JSON.parse(text).error || message } catch { /* not JSON */ }
    alert.value = { type: 'error', message }
  } finally {
    downloading.value = null
  }
}

function editBase(letter = null) {
  editing.value = {
    title: letter ? `Edit ${letter.name}` : 'New base letter',
    letter,
    isBase: true,
    previewUrl: `/cycles/${cycleId}/letters/preview`,
    save: async (form) => {
      if (letter) await api.put(`/cycles/${cycleId}/letters/${letter.id}`, form)
      else await api.post(`/cycles/${cycleId}/letters`, form)
      await load()
      alert.value = { type: 'success', message: 'Letter saved.' }
    },
  }
}

function editScholarship(s) {
  editing.value = {
    title: `${s.name}: award letter`,
    letter: s.letter,
    isBase: false,
    previewUrl: `/scholarships/${s.id}/letter/preview`,
    save: async (form) => {
      await api.put(`/scholarships/${s.id}/letter`, { name: s.name, body: form.body })
      await load()
      alert.value = { type: 'success', message: `${s.name} now has its own letter.` }
    },
  }
}

async function makeDefault(letter) {
  await api.put(`/cycles/${cycleId}/letters/${letter.id}`, { name: letter.name, body: letter.body, isDefault: true })
  await load()
}

async function removeBase(letter) {
  if (!await confirm({ title: 'Delete letter', text: `Delete "${letter.name}"?`, confirmText: 'Delete', color: 'error' })) return
  await api.delete(`/cycles/${cycleId}/letters/${letter.id}`)
  await load()
}

async function resetScholarship(s) {
  if (!await confirm({
    title: 'Use the default letter',
    text: `Delete ${s.name}'s own letter? It will use the default letter instead.`,
    confirmText: 'Delete its letter',
    color: 'error',
  })) return
  await api.delete(`/scholarships/${s.id}/letter`)
  await load()
}
</script>

<template>
  <div class="space-y-6 max-w-4xl">
    <div>
      <v-btn variant="text" color="primary" size="small" prepend-icon="mdi-arrow-left" :to="`/admin/cycles/${cycleId}/questions`" class="-ml-3 mb-1">
        Questions &amp; scholarships
      </v-btn>
      <h1 class="page-title">Award Letters</h1>
      <p class="page-subtitle">{{ cycleName }}</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading letters…
    </div>

    <template v-else>
      <v-card title="Base letters">
        <template #append>
          <v-btn variant="tonal" prepend-icon="mdi-plus" @click="editBase()">New letter</v-btn>
        </template>
        <v-card-text class="pa-6 space-y-3">
          <p class="text-sm text-slate-400">
            Scholarships use the default letter unless they have their own. A scholarship's own letter can start from any base letter.
          </p>
          <p v-if="baseLetters.length === 0" class="text-sm text-slate-400">No base letters yet. The first one you add becomes the default.</p>
          <div v-for="letter in baseLetters" :key="letter.id" class="rounded-lg border border-[#E9EDF7] pa-4 flex items-center gap-3">
            <div class="flex-1 min-w-0">
              <p class="text-sm font-semibold text-navy">{{ letter.name }}</p>
              <p class="text-xs text-slate-400">Updated {{ new Date(letter.updatedAt).toLocaleDateString() }}</p>
            </div>
            <v-chip v-if="letter.isDefault" color="primary">Default</v-chip>
            <v-btn v-else variant="text" size="small" @click="makeDefault(letter)">Make default</v-btn>
            <v-btn icon="mdi-pencil" variant="text" size="small" title="Edit letter" @click="editBase(letter)" />
            <v-btn icon="mdi-trash-can" variant="text" size="small" color="error" title="Delete letter" @click="removeBase(letter)" />
          </div>
        </v-card-text>
      </v-card>

      <v-card title="Scholarship letters">
        <v-card-text class="pa-6 space-y-3">
          <p v-if="scholarships.length === 0" class="text-sm text-slate-400">This cycle has no scholarships yet.</p>
          <div v-for="s in scholarships" :key="s.id" class="rounded-lg border border-[#E9EDF7] pa-4 flex items-center gap-3">
            <div class="flex-1 min-w-0">
              <p class="text-sm font-semibold text-navy">{{ s.name }}</p>
              <p class="text-xs text-slate-400">
                <template v-if="s.letter">Its own letter · updated {{ new Date(s.letter.updatedAt).toLocaleDateString() }}</template>
                <template v-else-if="defaultLetter">Uses the default letter ({{ defaultLetter.name }})</template>
                <template v-else>No letter yet</template>
                · {{ s.awardCount }} awarded
              </p>
            </div>
            <v-btn
              color="primary"
              size="small"
              prepend-icon="mdi-file-pdf-box"
              :disabled="!s.awardCount || !(s.letter || defaultLetter)"
              :title="!s.awardCount ? 'Not awarded to anyone yet' : !(s.letter || defaultLetter) ? 'Write a letter first' : 'Every awardee\'s letter in one PDF'"
              :loading="downloading === s.id"
              @click="downloadLetters(s)"
            >
              All letters (PDF)
            </v-btn>
            <v-btn variant="outlined" size="small" @click="editScholarship(s)">{{ s.letter ? 'Edit its letter' : 'Write its own letter' }}</v-btn>
            <v-btn v-if="s.letter" variant="text" size="small" color="error" @click="resetScholarship(s)">Use default</v-btn>
          </div>
        </v-card-text>
      </v-card>
    </template>

    <LetterEditorDialog
      v-if="editing"
      v-model="editorOpen"
      :title="editing.title"
      :cycle-id="cycleId"
      :letter="editing.letter"
      :is-base="editing.isBase"
      :base-letters="baseLetters"
      :preview-url="editing.previewUrl"
      :save="editing.save"
    />
  </div>
</template>

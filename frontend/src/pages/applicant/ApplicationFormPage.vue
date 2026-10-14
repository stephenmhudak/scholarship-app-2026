<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'
import { useApplicationStore } from '../../stores/application'
import { validateForm } from '../../utils/validate'
import { visibleQuestionIds } from '../../utils/questionVisibility'
import QuestionRenderer from '../../components/application/QuestionRenderer.vue'
import ApplicationProgress from '../../components/application/ApplicationProgress.vue'
import NoApplicationCard from '../../components/application/NoApplicationCard.vue'
import ApplicationReferences from '../../components/application/ApplicationReferences.vue'

const route = useRoute()
const router = useRouter()
const appStore = useApplicationStore()

const appId = route.params.id
const loading = ref(true)
const saving = ref(false)
const submitting = ref(false)
const alert = ref(null)
// /application/current 404s when the applicant hasn't started one — not an error.
const noApplication = ref(false)

// Other applications (different cycles) whose matching answers can be copied in.
const copySources = ref([])
const copySourceId = ref(null)
const copying = ref(false)
const validationErrors = ref({})

// Sections loaded alongside questions
const sections = ref([])
// Which references are needed depends on saved answers, so this refreshes after each save
const referencesPanel = ref(null)

onMounted(async () => {
  try {
    await appStore.loadApplicationForm(appId)

    // Submitted, archived, or closed-cycle applications can't be edited — show them read-only.
    const app = appStore.currentApplication
    if (app.status !== 'draft' || !app.isCycleOpen) {
      router.replace(`/application/${app.id}/status`)
      return
    }

    const myRes = await api.get('/applications/my')
    copySources.value = myRes.data
      .filter((a) => a.cycleId !== app.cycleId)
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))

    // Load sections for the cycle
    if (appStore.cycleId) {
      const res = await api.get(`/cycles/${appStore.cycleId}/sections`)
      sections.value = res.data
    }
  } catch (err) {
    if (appId === 'current' && err.response?.status === 404) noApplication.value = true
    else alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

// Conditional questions only appear once the answers they depend on match, and disappear again if those answers
// change. Hidden questions aren't validated, and the server drops their answers when the draft is saved.
const visibleQuestions = computed(() => {
  const visible = visibleQuestionIds(appStore.questions, (id) => appStore.answers[id])
  return appStore.questions.filter((q) => visible.has(q.id))
})

// Group questions by section, preserving section order.
// Questions with no sectionId go into an implicit "Other" group at the end.
const groupedSections = computed(() => {
  const orderedSections = [...sections.value].sort((a, b) => a.order - b.order)
  const groups = orderedSections.map((s) => ({
    ...s,
    questions: visibleQuestions.value.filter((q) => q.sectionId === s.id),
  })).filter((g) => g.questions.length > 0)

  const ungrouped = visibleQuestions.value.filter((q) => !q.sectionId)
  if (ungrouped.length > 0) {
    groups.push({ id: null, title: null, description: null, questions: ungrouped })
  }

  return groups
})

const answeredCount = computed(() =>
  visibleQuestions.value.filter((q) => {
    const v = appStore.answers[q.id]
    return v != null && v !== '' && !(Array.isArray(v) && v.length === 0)
  }).length
)

function handleAnswer(questionId, value) {
  appStore.saveAnswer(questionId, value)
  // Clear inline error as user types
  if (validationErrors.value[questionId]) {
    delete validationErrors.value[questionId]
  }
}

async function saveDraft() {
  saving.value = true
  alert.value = null
  try {
    await appStore.saveDraft(appId)
    referencesPanel.value?.refresh()
    alert.value = { type: 'success', message: 'Draft saved.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to save draft.' }
  } finally {
    saving.value = false
  }
}

async function copyAnswers() {
  if (!copySourceId.value) return
  copying.value = true
  alert.value = null
  const id = appStore.currentApplication.id
  try {
    // Save first so unsaved edits aren't lost when the form reloads; copying only fills unanswered questions.
    await appStore.saveDraft(id)
    const { data } = await api.post(`/applications/${id}/copy-answers`, { sourceApplicationId: copySourceId.value })
    await appStore.loadApplicationForm(id)
    copySourceId.value = null

    const parts = []
    if (data.copied) parts.push(`Copied ${data.copied} answer${data.copied === 1 ? '' : 's'}.`)
    if (data.alreadyAnswered) parts.push(`${data.alreadyAnswered} matching question${data.alreadyAnswered === 1 ? ' was' : 's were'} already answered and left unchanged.`)
    alert.value = parts.length
      ? { type: 'success', message: parts.join(' ') }
      : { type: 'info', message: 'No answered questions in that application match this one.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to copy answers.' }
  } finally {
    copying.value = false
  }
}

function sourceLabel(a) {
  const status = { draft: 'Incomplete', archived: 'Archived' }[a.status] ?? 'Submitted'
  return `${a.cycleName || 'Scholarship'} — ${status}`
}

async function submit() {
  // Run validation
  const errors = validateForm(visibleQuestions.value, appStore.answers)
  validationErrors.value = errors

  if (Object.keys(errors).length > 0) {
    alert.value = { type: 'warning', message: 'Please fix the errors below before submitting.' }
    return
  }

  submitting.value = true
  alert.value = null
  try {
    await appStore.saveDraft(appId)
    referencesPanel.value?.refresh()
    await appStore.submitApplication(appId)
    alert.value = { type: 'success', message: 'Application submitted!' }
    setTimeout(() => router.push(`/application/${appId}/status`), 1500)
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to submit application.' }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <div>
      <h1 class="page-title">{{ appStore.currentApplication?.cycleName || 'Scholarship Application' }}</h1>
      <p class="page-subtitle">
        Complete all sections and submit before the deadline<template v-if="appStore.currentApplication?.cycleCloseDate">
          ({{ new Date(appStore.currentApplication.cycleCloseDate).toLocaleDateString() }})</template>.
      </p>
    </div>

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading…
    </div>

    <NoApplicationCard v-else-if="noApplication" />

    <template v-else>
      <ApplicationProgress :answered="answeredCount" :total="visibleQuestions.length" />

      <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

      <!-- Reuse answers from another cycle's application -->
      <v-card v-if="copySources.length" title="Reuse previous answers">
        <v-card-text class="pa-6 space-y-3">
          <p class="text-sm text-slate-400">
            Copies answers for questions that match exactly. Questions you've already answered aren't changed.
          </p>
          <div class="flex items-center gap-3">
            <v-select
              v-model="copySourceId"
              :items="copySources"
              :item-title="sourceLabel"
              item-value="id"
              placeholder="Choose an application…"
              hide-details
              class="flex-1"
            />
            <v-btn variant="outlined" prepend-icon="mdi-content-copy" :loading="copying" :disabled="!copySourceId" @click="copyAnswers">
              Copy Answers
            </v-btn>
          </div>
        </v-card-text>
      </v-card>

      <!-- Sections + questions -->
      <template v-for="group in groupedSections" :key="group.id ?? '__ungrouped'">
        <!-- Section header (only when named sections exist) -->
        <div v-if="group.title" class="pt-2">
          <h2 class="text-base font-bold text-navy">{{ group.title }}</h2>
          <p v-if="group.description" class="text-sm text-slate-400 mt-0.5">{{ group.description }}</p>
          <v-divider class="mt-2" />
        </div>

        <div class="space-y-4">
          <QuestionRenderer
            v-for="question in group.questions"
            :key="question.id"
            :question="question"
            :applicationId="appStore.currentApplication?.id"
            :modelValue="appStore.answers[question.id]"
            :error="validationErrors[question.id] ?? null"
            @answer="(val) => handleAnswer(question.id, val)"
          />
        </div>
      </template>

      <ApplicationReferences
        v-if="appStore.currentApplication"
        ref="referencesPanel"
        :applicationId="appStore.currentApplication.id"
      />

      <v-divider />
      <div class="flex items-center gap-3">
        <v-btn variant="outlined" prepend-icon="mdi-content-save" :loading="saving" @click="saveDraft">
          Save Draft
        </v-btn>
        <v-btn color="primary" prepend-icon="mdi-send" :loading="submitting" @click="submit">
          Submit Application
        </v-btn>
      </div>
    </template>
  </div>
</template>

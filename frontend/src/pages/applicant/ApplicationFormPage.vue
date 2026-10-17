<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import api from '../../services/api'
import { useApplicationStore } from '../../stores/application'
import { useConfirm } from '../../composables/useConfirm'
import { validateForm, isAnswered } from '../../utils/validate'
import { visibleQuestionIds } from '../../utils/questionVisibility'
import QuestionRenderer from '../../components/application/QuestionRenderer.vue'
import ApplicationProgress from '../../components/application/ApplicationProgress.vue'
import NoApplicationCard from '../../components/application/NoApplicationCard.vue'
import ApplicationReferences from '../../components/application/ApplicationReferences.vue'
import { formatDeadline } from '../../utils/deadline'

const route = useRoute()
const router = useRouter()
const appStore = useApplicationStore()
const confirm = useConfirm()

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
const referencesMissing = ref(0)

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

// Progress counts required questions (what blocks submitting); a form with none counts every question
const requiredQuestions = computed(() => visibleQuestions.value.filter((q) => q.isRequired))
const progressQuestions = computed(() => requiredQuestions.value.length ? requiredQuestions.value : visibleQuestions.value)
const answeredCount = computed(() => progressQuestions.value.filter((q) => isAnswered(q, appStore.answers[q.id])).length)
const missingRequired = computed(() => requiredQuestions.value.filter((q) => !isAnswered(q, appStore.answers[q.id])))

// Jump list: each section, done once its required questions are answered (every question if it has none required)
const sectionNav = computed(() => {
  if (!groupedSections.value.some((g) => g.title)) return []
  return groupedSections.value.map((g) => {
    const counted = g.questions.some((q) => q.isRequired) ? g.questions.filter((q) => q.isRequired) : g.questions
    const answered = counted.filter((q) => isAnswered(q, appStore.answers[q.id])).length
    return { key: g.id ?? 'other', title: g.title || 'Other questions', answered, total: counted.length, done: answered === counted.length }
  })
})

function goToSection(key) {
  document.getElementById(`section-${key}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function goToReferences() {
  document.getElementById('references')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

// Scrolls to a question and puts the cursor in it
function goToQuestion(id) {
  const el = document.getElementById(`q-${id}`)
  if (!el) return
  el.scrollIntoView({ behavior: 'smooth', block: 'start' })
  el.querySelector('input, textarea, [contenteditable="true"]')?.focus({ preventScroll: true })
}

// ── Autosave ─────────────────────────────────────────────────────────────────
// Answers save a couple of seconds after the applicant stops changing them. `edits` counts changes so an edit made
// while a save is in flight isn't marked as saved.
const AUTOSAVE_DELAY_MS = 2000
const edits = ref(0)
const savedEdits = ref(0)
const saveFailed = ref(false)
let autosaveTimer = null

const hasUnsavedChanges = computed(() => edits.value !== savedEdits.value)
const saveStatus = computed(() => {
  if (saving.value) return { icon: 'mdi-cloud-upload-outline', text: 'Saving…', class: 'text-slate-400' }
  if (saveFailed.value) return { icon: 'mdi-cloud-alert-outline', text: "Couldn't save. Check your connection; we'll keep trying.", class: 'text-danger-dark' }
  if (hasUnsavedChanges.value) return { icon: 'mdi-pencil-outline', text: 'Unsaved changes', class: 'text-slate-400' }
  return { icon: 'mdi-cloud-check-outline', text: 'All changes saved', class: 'text-slate-400' }
})

function handleAnswer(questionId, value) {
  appStore.saveAnswer(questionId, value)
  // Clear inline error as user types
  if (validationErrors.value[questionId]) {
    delete validationErrors.value[questionId]
  }
  edits.value++
  clearTimeout(autosaveTimer)
  autosaveTimer = setTimeout(() => saveDraft(), AUTOSAVE_DELAY_MS)
}

// Saves whatever has changed. Returns false if the save failed. A call made while a save is running waits for it, then
// saves anything changed since.
let inFlight = null
async function saveDraft() {
  clearTimeout(autosaveTimer)
  while (inFlight) await inFlight
  if (!hasUnsavedChanges.value) return true
  const savingEdits = edits.value
  saving.value = true
  inFlight = (async () => {
    try {
      await appStore.saveDraft(appId)
      savedEdits.value = savingEdits
      saveFailed.value = false
      referencesPanel.value?.refresh()
    } catch {
      saveFailed.value = true
      // Keep retrying in the background
      autosaveTimer = setTimeout(() => saveDraft(), AUTOSAVE_DELAY_MS * 5)
    }
  })()
  await inFlight
  inFlight = null
  saving.value = false
  if (saveFailed.value) return false
  // Changes made during the save get their own save
  if (hasUnsavedChanges.value) autosaveTimer = setTimeout(() => saveDraft(), AUTOSAVE_DELAY_MS)
  return true
}

// Creating a reference link saves first: which references are needed depends on the saved answers
async function saveBeforeReference() {
  if (!await saveDraft()) throw new Error("Your latest changes couldn't be saved. Please try again.")
}

// Leaving the page saves first; only if that fails does the applicant have to choose
onBeforeRouteLeave(async () => {
  if (!hasUnsavedChanges.value || submitting.value) return true
  if (await saveDraft()) return true
  return confirm({
    title: 'Leave without saving?',
    text: "Your latest changes couldn't be saved. If you leave now, they'll be lost.",
    confirmText: 'Leave anyway',
    color: 'error',
  })
})

// Closing or reloading the tab: the browser's own "leave site?" prompt
function warnOnUnload(event) {
  if (!hasUnsavedChanges.value) return
  event.preventDefault()
  event.returnValue = ''
}
window.addEventListener('beforeunload', warnOnUnload)
onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', warnOnUnload)
  clearTimeout(autosaveTimer)
})

async function copyAnswers() {
  if (!copySourceId.value) return
  copying.value = true
  alert.value = null
  const id = appStore.currentApplication.id
  try {
    // Save first so unsaved edits aren't lost when the form reloads; copying only fills unanswered questions.
    if (!await saveDraft()) throw new Error('save failed')
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

  const firstError = visibleQuestions.value.find((q) => errors[q.id])
  if (firstError) {
    const count = Object.keys(errors).length
    alert.value = { type: 'warning', message: `${count} question${count === 1 ? ' needs' : 's need'} attention before you can submit. They're marked in red.` }
    goToQuestion(firstError.id)
    return
  }

  if (!await saveDraft()) {
    alert.value = { type: 'error', message: "Your latest changes couldn't be saved, so the application wasn't submitted. Please try again." }
    return
  }
  submitting.value = true
  alert.value = null
  try {
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
        Complete every section and submit<template v-if="appStore.currentApplication?.cycleCloseDate">
          by {{ formatDeadline(appStore.currentApplication.cycleCloseDate) }}</template>.
      </p>
    </div>

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading…
    </div>

    <NoApplicationCard v-else-if="noApplication" />

    <template v-else>
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

      <!-- Jump to a section; a check mark once its required questions are answered -->
      <v-card v-if="sectionNav.length > 1" title="Sections">
        <v-card-text class="pa-4 flex flex-wrap gap-2">
          <v-chip
            v-for="s in sectionNav"
            :key="s.key"
            :color="s.done ? 'success' : undefined"
            :prepend-icon="s.done ? 'mdi-check-circle' : 'mdi-circle-outline'"
            @click="goToSection(s.key)"
          >
            {{ s.title }}
            <span v-if="!s.done" class="ml-1.5 text-xs opacity-70">{{ s.answered }}/{{ s.total }}</span>
          </v-chip>
        </v-card-text>
      </v-card>

      <!-- Sections + questions -->
      <template v-for="group in groupedSections" :key="group.id ?? '__ungrouped'">
        <!-- Section header (only when named sections exist; questions outside a section come last as "Other questions") -->
        <div v-if="sectionNav.length" :id="`section-${group.id ?? 'other'}`" class="pt-2 scroll-mt-20">
          <h2 class="text-base font-bold text-navy">{{ group.title || 'Other questions' }}</h2>
          <p v-if="group.description" class="text-sm text-slate-400 mt-0.5">{{ group.description }}</p>
          <v-divider class="mt-2" />
        </div>

        <div class="space-y-4">
          <div v-for="question in group.questions" :id="`q-${question.id}`" :key="question.id" class="scroll-mt-20">
            <QuestionRenderer
              :question="question"
              :applicationId="appStore.currentApplication?.id"
              :modelValue="appStore.answers[question.id]"
              :error="validationErrors[question.id] ?? null"
              @answer="(val) => handleAnswer(question.id, val)"
            />
          </div>
        </div>
      </template>

      <ApplicationReferences
        v-if="appStore.currentApplication"
        id="references"
        ref="referencesPanel"
        class="scroll-mt-20"
        @missing="(n) => referencesMissing = n"
        :applicationId="appStore.currentApplication.id"
        :questions="appStore.questions"
        :answers="appStore.answers"
        :before-create="saveBeforeReference"
      />

      <!-- Stays at the bottom of the screen while scrolling the form -->
      <div class="form-action-bar">
        <v-card class="pa-4 flex flex-wrap items-center gap-x-6 gap-y-3">
          <div class="flex-1 min-w-[220px] space-y-1">
            <ApplicationProgress :answered="answeredCount" :total="progressQuestions.length">
              {{ answeredCount }} of {{ progressQuestions.length }} {{ requiredQuestions.length ? 'required ' : '' }}answered
            </ApplicationProgress>
            <div class="flex flex-wrap items-center gap-x-4 gap-y-1">
              <p class="flex items-center gap-1.5 text-xs" :class="saveStatus.class" role="status" aria-live="polite">
                <v-icon :icon="saveStatus.icon" size="16" /> {{ saveStatus.text }}
              </p>
              <!-- Which required questions are still empty; picking one jumps to it -->
              <v-menu v-if="missingRequired.length" location="top start">
                <template #activator="{ props: menuProps }">
                  <v-btn v-bind="menuProps" variant="text" size="x-small" color="warning" append-icon="mdi-chevron-up" class="-ml-2">
                    {{ missingRequired.length }} required still empty
                  </v-btn>
                </template>
                <v-list density="compact" max-height="320" max-width="420">
                  <v-list-subheader>Required questions still empty</v-list-subheader>
                  <v-list-item
                    v-for="q in missingRequired"
                    :key="q.id"
                    :title="q.text"
                    prepend-icon="mdi-alert-circle-outline"
                    @click="goToQuestion(q.id)"
                  />
                </v-list>
              </v-menu>
              <p v-else-if="requiredQuestions.length" class="flex items-center gap-1.5 text-xs text-success-dark">
                <v-icon icon="mdi-check-circle-outline" size="16" /> Every required question is answered
              </p>
            </div>
          </div>
          <div class="flex flex-col items-end gap-1">
            <div class="flex items-center gap-3">
              <v-btn variant="outlined" prepend-icon="mdi-content-save" :loading="saving" :disabled="!hasUnsavedChanges" @click="saveDraft">
                Save now
              </v-btn>
              <v-btn color="primary" prepend-icon="mdi-send" :loading="submitting" :disabled="referencesMissing > 0" @click="submit">
                Submit Application
              </v-btn>
            </div>
            <!-- Why Submit is disabled -->
            <button v-if="referencesMissing" type="button" class="flex items-center gap-1 text-xs text-[#8C6500] bg-transparent border-0 p-0 cursor-pointer hover:underline" @click="goToReferences">
              <v-icon icon="mdi-clock-outline" size="14" />
              Waiting on {{ referencesMissing }} reference{{ referencesMissing === 1 ? '' : 's' }} before you can submit
            </button>
          </div>
        </v-card>
      </div>
    </template>
  </div>
</template>

<style scoped>
.form-action-bar {
  position: sticky;
  bottom: 0;
  z-index: 2;
  padding-bottom: 16px;
}
.form-action-bar :deep(.v-card) {
  box-shadow: 0 -4px 24px rgba(43, 54, 116, 0.12);
}
</style>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'
import { useApplicationStore } from '../../stores/application'
import { validateForm } from '../../utils/validate'
import QuestionRenderer from '../../components/application/QuestionRenderer.vue'
import ApplicationProgress from '../../components/application/ApplicationProgress.vue'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const route = useRoute()
const router = useRouter()
const appStore = useApplicationStore()

const appId = route.params.id
const loading = ref(true)
const saving = ref(false)
const submitting = ref(false)
const alert = ref(null)
const validationErrors = ref({})

// Sections loaded alongside questions
const sections = ref([])

onMounted(async () => {
  try {
    await appStore.loadApplicationForm(appId)

    // Load sections for the cycle
    if (appStore.cycleId) {
      const res = await api.get(`/cycles/${appStore.cycleId}/sections`)
      sections.value = res.data
    }
  } catch {
    alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

// Group questions by section, preserving section order.
// Questions with no sectionId go into an implicit "Other" group at the end.
const groupedSections = computed(() => {
  const orderedSections = [...sections.value].sort((a, b) => a.order - b.order)
  const groups = orderedSections.map((s) => ({
    ...s,
    questions: appStore.questions.filter((q) => q.sectionId === s.id),
  })).filter((g) => g.questions.length > 0)

  const ungrouped = appStore.questions.filter((q) => !q.sectionId)
  if (ungrouped.length > 0) {
    groups.push({ id: null, title: null, description: null, questions: ungrouped })
  }

  return groups
})

const answeredCount = computed(() =>
  appStore.questions.filter((q) => {
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
    alert.value = { type: 'success', message: 'Draft saved.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to save draft.' }
  } finally {
    saving.value = false
  }
}

async function submit() {
  // Run validation
  const errors = validateForm(appStore.questions, appStore.answers)
  validationErrors.value = errors

  if (Object.keys(errors).length > 0) {
    alert.value = { type: 'warning', message: 'Please fix the errors below before submitting.' }
    return
  }

  submitting.value = true
  alert.value = null
  try {
    await appStore.saveDraft(appId)
    await appStore.submitApplication(appId)
    alert.value = { type: 'success', message: 'Application submitted!' }
    setTimeout(() => router.push(`/application/${appId}/status`), 1500)
  } catch {
    alert.value = { type: 'error', message: 'Failed to submit application.' }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <div>
      <h1 class="page-title">Scholarship Application</h1>
      <p class="page-subtitle">Complete all sections and submit before the deadline.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading…
    </div>

    <template v-else>
      <ApplicationProgress :answered="answeredCount" :total="appStore.questions.length" />

      <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

      <!-- Sections + questions -->
      <template v-for="group in groupedSections" :key="group.id ?? '__ungrouped'">
        <!-- Section header (only when named sections exist) -->
        <div v-if="group.title" class="pt-2">
          <h2 class="text-base font-bold text-navy">{{ group.title }}</h2>
          <p v-if="group.description" class="text-sm text-slate-400 mt-0.5">{{ group.description }}</p>
          <hr class="mt-2 border-[#E9EDF7]" />
        </div>

        <div class="space-y-4">
          <QuestionRenderer
            v-for="question in group.questions"
            :key="question.id"
            :question="question"
            :modelValue="appStore.answers[question.id]"
            :error="validationErrors[question.id] ?? null"
            @answer="(val) => handleAnswer(question.id, val)"
          />
        </div>
      </template>

      <div class="flex items-center gap-3 pt-4 border-t border-[#E9EDF7]">
        <BaseButton variant="secondary" :loading="saving" @click="saveDraft">
          <span class="mdi mdi-content-save mr-1.5"></span>Save Draft
        </BaseButton>
        <BaseButton :loading="submitting" @click="submit">
          <span class="mdi mdi-send mr-1.5"></span>Submit Application
        </BaseButton>
      </div>
    </template>
  </div>
</template>

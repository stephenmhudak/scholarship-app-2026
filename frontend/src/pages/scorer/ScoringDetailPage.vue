<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useScoringStore } from '../../stores/scoring'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseButton from '../../components/common/BaseButton.vue'

const route = useRoute()
const scoringStore = useScoringStore()

const applicationId = route.params.applicationId
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)
const scores = ref([])

const applicantName = computed(() => {
  const app = scoringStore.currentApplication
  if (!app) return ''
  return `${app.applicantFirstName ?? ''} ${app.applicantLastName ?? ''}`.trim()
})

const formSections = computed(() => {
  if (!scoringStore.sections?.length) {
    return [{ id: null, label: 'Overall Score' }]
  }
  return scoringStore.sections.map((s) => ({ id: s.id, label: s.title }))
})

const answersGroupedBySections = computed(() => {
  const app = scoringStore.currentApplication
  if (!app?.answers?.length) return []

  const answersMap = Object.fromEntries(app.answers.map((a) => [a.questionId, a]))
  const sectionMap = Object.fromEntries((scoringStore.sections ?? []).map((s) => [s.id, s]))

  const groups = {}
  for (const q of scoringStore.questions) {
    const answer = answersMap[q.id]
    if (!answer) continue
    const sectionId = q.sectionId ?? null
    const key = sectionId ?? 'null'
    if (!groups[key]) {
      const section = sectionId ? sectionMap[sectionId] : null
      groups[key] = { sectionId, title: section?.title ?? null, order: section?.order ?? 9999, answers: [] }
    }
    groups[key].answers.push({ ...answer, questionText: q.text })
  }

  return Object.values(groups).sort((a, b) => a.order - b.order)
})

const sectionsData = computed(() => {
  const answersBySectionId = Object.fromEntries(
    answersGroupedBySections.value.map((g) => [g.sectionId ?? 'null', g.answers])
  )
  return scores.value.map((s, idx) => ({
    idx,
    sectionId: s.sectionId,
    label: s.label,
    answers: answersBySectionId[s.sectionId ?? 'null'] ?? [],
  }))
})

function buildScores() {
  scores.value = formSections.value.map((s) => {
    const existing = scoringStore.existingScores.find((e) => e.sectionId === s.id)
    return {
      sectionId: s.id,
      label: s.label,
      score: existing?.scoreValue ?? '',
      comments: existing?.comments ?? '',
    }
  })
}

watch(() => [formSections.value, scoringStore.existingScores], buildScores, { deep: true, immediate: true })

onMounted(async () => {
  try {
    await scoringStore.fetchApplication(applicationId)
  } catch {
    alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

async function handleSave() {
  const filled = scores.value.filter((s) => s.score !== '' && s.score != null)
  if (!filled.length) return
  saving.value = true
  alert.value = null
  try {
    await scoringStore.submitScore(applicationId, {
      sectionScores: filled.map((s) => ({
        sectionId: s.sectionId,
        score: Number(s.score),
        comments: s.comments || null,
      })),
    })
    alert.value = { type: 'success', message: 'Scores saved.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to save scores.' }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-5xl">
    <div>
      <h1 class="page-title">Score Application</h1>
      <p v-if="applicantName" class="page-subtitle">{{ applicantName }}</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application…
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-if="!loading && scoringStore.currentApplication">
      <div class="space-y-8">
        <div v-for="section in sectionsData" :key="section.sectionId ?? 'overall'" class="space-y-3">
          <p v-if="scoringStore.sections?.length" class="text-xs font-bold text-slate-400 uppercase tracking-widest">
            {{ section.label }}
          </p>

          <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
            <!-- Answers -->
            <div class="card p-5 space-y-4">
              <template v-if="section.answers.length">
                <div
                  v-for="answer in section.answers"
                  :key="answer.questionId"
                >
                  <p class="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1.5">
                    {{ answer.questionText || answer.questionId }}
                  </p>
                  <p class="text-sm text-navy whitespace-pre-wrap">
                    {{ answer.selectedOptions?.length ? answer.selectedOptions.join(', ') : (answer.textValue || '—') }}
                  </p>
                </div>
              </template>
              <p v-else class="text-sm text-slate-400">No answers for this section.</p>
            </div>

            <!-- Score input -->
            <div class="card p-5 space-y-4">
              <div class="space-y-1.5">
                <label class="block text-sm font-semibold text-navy">
                  Score <span class="text-slate-400 font-normal">(1–100)</span>
                </label>
                <input
                  v-model.number="scores[section.idx].score"
                  type="number"
                  min="1"
                  max="100"
                  placeholder="Enter score…"
                  class="form-input w-36"
                />
              </div>
              <div class="space-y-1.5">
                <label class="block text-sm font-semibold text-navy">Comments</label>
                <textarea
                  v-model="scores[section.idx].comments"
                  rows="3"
                  placeholder="Enter your comments…"
                  class="form-input resize-y"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="flex justify-end">
        <BaseButton :loading="saving" @click="handleSave">
          <span class="mdi mdi-content-save-outline mr-1.5"></span>
          Save Scores
        </BaseButton>
      </div>
    </template>
  </div>
</template>

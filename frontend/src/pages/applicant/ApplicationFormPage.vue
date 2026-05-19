<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApplicationStore } from '../../stores/application'
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

onMounted(async () => {
  try {
    await appStore.fetchQuestions(appId)
  } catch {
    alert.value = { type: 'error', message: 'Failed to load questions.' }
  } finally {
    loading.value = false
  }
})

const answeredCount = computed(
  () => appStore.questions.filter((q) => appStore.answers[q.id] != null && appStore.answers[q.id] !== '').length
)

function handleAnswer(questionId, value) {
  appStore.saveAnswer(questionId, value)
}

async function saveDraft() {
  saving.value = true
  alert.value = null
  try {
    await appStore.saveDraft(appId)
    alert.value = { type: 'success', message: 'Draft saved successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to save draft.' }
  } finally {
    saving.value = false
  }
}

async function submit() {
  if (answeredCount.value < appStore.questions.length) {
    alert.value = { type: 'warning', message: 'Please answer all required questions before submitting.' }
    return
  }

  submitting.value = true
  alert.value = null
  try {
    await appStore.submitApplication(appId)
    alert.value = { type: 'success', message: 'Application submitted successfully!' }
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
      <h1 class="text-2xl font-bold text-gray-900">Scholarship Application</h1>
      <p class="text-gray-500 mt-1">Complete all sections and submit before the deadline.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading questions...
    </div>

    <template v-else>
      <ApplicationProgress
        :answered="answeredCount"
        :total="appStore.questions.length"
      />

      <BaseAlert
        v-if="alert"
        :type="alert.type"
        :message="alert.message"
      />

      <div class="space-y-4">
        <QuestionRenderer
          v-for="question in appStore.questions"
          :key="question.id"
          :question="question"
          :modelValue="appStore.answers[question.id]"
          @answer="(val) => handleAnswer(question.id, val)"
        />
      </div>

      <div class="flex items-center gap-3 pt-4 border-t border-gray-200">
        <BaseButton variant="secondary" :loading="saving" @click="saveDraft">
          <span class="mdi mdi-content-save mr-1"></span>
          Save Draft
        </BaseButton>
        <BaseButton :loading="submitting" @click="submit">
          <span class="mdi mdi-send mr-1"></span>
          Submit Application
        </BaseButton>
      </div>
    </template>
  </div>
</template>

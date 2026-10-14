<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import { useAuthStore } from '../../stores/auth'
import { normalizeReferenceCode } from '../../utils/pendingReference'
import { validateForm } from '../../utils/validate'
import QuestionRenderer from '../../components/application/QuestionRenderer.vue'
import SignaturePad from '../../components/reference/SignaturePad.vue'

// A signed-in referee fills in and signs a reference by its code (/r/CODE, from the applicant's link or typed in
// on My References). The submission is tied to their account.
const route = useRoute()
const authStore = useAuthStore()
const code = normalizeReferenceCode(route.params.code)

const loading = ref(true)
const loadError = ref(null)
const link = ref(null)
const answers = ref({})
const errors = ref({})
const signerName = ref(`${authStore.user?.firstName ?? ''} ${authStore.user?.lastName ?? ''}`.trim())
const signature = ref(null)
const submitting = ref(false)
const alert = ref(null)
const submitted = ref(false)

const today = new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })
const uploadUrl = computed(() => `/reference-links/${code}/files`)

onMounted(async () => {
  try {
    const { data } = await api.get(`/reference-links/${code}`)
    link.value = data
  } catch (err) {
    loadError.value = [403, 404].includes(err.response?.status)
      ? err.response.data?.error || "That reference code isn't valid."
      : 'The reference form could not be loaded. Please try again in a moment.'
  } finally {
    loading.value = false
  }
})

function clearError(key) {
  if (errors.value[key]) delete errors.value[key]
}

function setAnswer(questionId, value) {
  answers.value[questionId] = value
  clearError(questionId)
}

async function submit() {
  const found = validateForm(link.value.questions, answers.value)
  if (!signerName.value.trim()) found.signerName = 'Please enter your name.'
  if (!signature.value) found.signature = 'Please sign in the box above.'
  errors.value = found
  if (Object.keys(found).length > 0) {
    alert.value = { type: 'warning', message: 'Please fix the highlighted fields before submitting.' }
    return
  }

  submitting.value = true
  alert.value = null
  try {
    await api.post(`/reference-links/${code}/submit`, {
      signerName: signerName.value.trim(),
      signature: signature.value,
      answers: Object.entries(answers.value).map(([questionId, value]) => ({
        questionId,
        textValue: Array.isArray(value) ? null : (value || null),
        selectedOptions: Array.isArray(value) ? value : null,
      })),
    })
    submitted.value = true
    window.scrollTo({ top: 0 })
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Your reference could not be submitted. Please try again.' }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading reference form…
    </div>

    <v-card v-else-if="loadError">
      <v-empty-state icon="mdi-link-off" title="Can't open this reference" :text="loadError">
        <template #actions>
          <v-btn color="primary" to="/references">Enter a different code</v-btn>
        </template>
      </v-empty-state>
    </v-card>

    <v-card v-else-if="submitted">
      <v-empty-state icon="mdi-check-circle" color="success" title="Thank you!">
        <template #text>
          <p class="text-sm text-slate-400">
            Your reference for <strong class="text-navy">{{ link.applicantName }}</strong> has been submitted.
            It goes directly to the scholarship committee; the applicant can't see what you wrote.
          </p>
        </template>
        <template #actions>
          <v-btn color="primary" to="/references">My References</v-btn>
        </template>
      </v-empty-state>
    </v-card>

    <v-card v-else-if="link.status === 'submitted'">
      <v-empty-state
        icon="mdi-check-circle-outline"
        title="Already submitted"
        :text="link.submittedReferenceId
          ? `You've already submitted this ${link.formTitle} for ${link.applicantName}.`
          : `This ${link.formTitle} for ${link.applicantName} has already been submitted.`"
      >
        <template #actions>
          <v-btn color="primary" :to="link.submittedReferenceId ? `/references/${link.submittedReferenceId}` : '/references'">
            {{ link.submittedReferenceId ? 'View your reference' : 'My References' }}
          </v-btn>
        </template>
      </v-empty-state>
    </v-card>

    <v-card v-else-if="link.status === 'closed'">
      <v-empty-state icon="mdi-calendar-remove" title="This link has expired"
        :text="`The deadline for ${link.cycleName} has passed, so references can no longer be submitted.`" />
    </v-card>

    <template v-else>
      <div>
        <h1 class="page-title">{{ link.formTitle }}</h1>
        <p class="page-subtitle">{{ link.cycleName }}</p>
      </div>

      <v-card title="Applicant">
        <v-card-text class="pa-6 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <p class="eyebrow">Applicant's name</p>
            <p class="text-sm font-semibold text-navy">{{ link.applicantName }}</p>
          </div>
          <div v-if="link.schoolName">
            <p class="eyebrow">School</p>
            <p class="text-sm font-semibold text-navy">{{ link.schoolName }}</p>
          </div>
        </v-card-text>
      </v-card>

      <p v-if="link.formDescription" class="text-sm text-slate-400 whitespace-pre-line">{{ link.formDescription }}</p>
      <v-alert type="info"
        text="Your answers go directly to the scholarship committee. The applicant won't see them." />

      <div class="space-y-4">
        <QuestionRenderer
          v-for="question in link.questions"
          :key="question.id"
          :question="question"
          :uploadUrl="uploadUrl"
          :modelValue="answers[question.id]"
          :error="errors[question.id] ?? null"
          @answer="(val) => setAnswer(question.id, val)"
        />
      </div>

      <v-card title="Signature">
        <v-card-text class="pa-6 space-y-5">
          <v-text-field
            v-model="signerName"
            label="Your full name"
            autocomplete="name"
            :error-messages="errors.signerName || []"
            hide-details="auto"
            @update:model-value="clearError('signerName')"
          />
          <SignaturePad v-model="signature" :error="errors.signature" @update:model-value="clearError('signature')" />
          <p class="text-sm text-slate-400">Date: <span class="text-navy font-medium">{{ today }}</span></p>
        </v-card-text>
      </v-card>

      <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

      <div class="flex justify-end">
        <v-btn color="primary" size="large" prepend-icon="mdi-send" :loading="submitting" @click="submit">
          Submit Reference
        </v-btn>
      </div>
    </template>
  </div>
</template>

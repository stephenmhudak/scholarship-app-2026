<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import api from '../../services/api'
import { useConfirm } from '../../composables/useConfirm'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import LetterEditorDialog from '../../components/letters/LetterEditorDialog.vue'
import { downloadPdf } from '../../utils/pdf'

// Awarding the scholarships you're assigned to (super admins: all) to eligible, submitted applications
const confirm = useConfirm()
const scholarships = ref([])
const selectedId = ref(null)
const candidates = ref([])
const loading = ref(true)
const loadingCandidates = ref(false)
const alert = ref(null)
const awarding = ref(null)
const saving = ref(false)

const selected = computed(() => scholarships.value.find((s) => s.Id === selectedId.value) ?? null)
const scholarshipItems = computed(() =>
  scholarships.value.map((s) => ({ value: s.Id, title: s.Name, subtitle: `${s.CycleName} · ${s.AwardCount} awarded` })))
const awardedCount = computed(() => candidates.value.filter((c) => c.award).length)

const headers = [
  { title: 'Applicant', key: 'applicantName' },
  { title: 'School', key: 'schoolName' },
  { title: 'Status', key: 'status' },
  { title: 'Avg. Score', key: 'averageScore', align: 'end', value: (c) => c.averageScore ?? -1 },
  { title: 'Award', key: 'award', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

onMounted(async () => {
  try {
    const res = await api.get('/awards/scholarships')
    scholarships.value = res.data
    if (res.data.length === 1) selectedId.value = res.data[0].Id
  } catch {
    alert.value = { type: 'error', message: 'Failed to load your scholarships.' }
  } finally {
    loading.value = false
  }
})

watch(selectedId, () => { loadCandidates(); loadLetter() })

// The scholarship's award letter: its own, else the cycle's default base letter
const letterInfo = ref(null)
const letterEditorOpen = ref(false)
const downloading = ref(null)

async function loadLetter() {
  letterInfo.value = null
  if (!selectedId.value) return
  try {
    letterInfo.value = (await api.get(`/scholarships/${selectedId.value}/letter`)).data
  } catch {
    letterInfo.value = null
  }
}

const letterText = computed(() => {
  if (!letterInfo.value) return ''
  if (letterInfo.value.own) return 'This scholarship has its own award letter.'
  if (letterInfo.value.default) return `Uses the default letter (${letterInfo.value.default.name}).`
  return 'No award letter yet. Write one to generate letters.'
})
const hasLetter = computed(() => !!(letterInfo.value?.own || letterInfo.value?.default))

async function saveLetter(form) {
  await api.put(`/scholarships/${selectedId.value}/letter`, { name: selected.value.Name, body: form.body })
  await loadLetter()
  alert.value = { type: 'success', message: 'Award letter saved.' }
}

async function download(url, key) {
  downloading.value = key
  alert.value = null
  try {
    await downloadPdf(url)
  } catch (err) {
    const text = err.response?.data instanceof Blob ? await err.response.data.text() : null
    alert.value = { type: 'error', message: (text && JSON.parse(text).error) || 'Failed to generate the letter.' }
  } finally {
    downloading.value = null
  }
}

async function loadCandidates() {
  candidates.value = []
  if (!selectedId.value) return
  loadingCandidates.value = true
  try {
    const res = await api.get(`/awards/scholarships/${selectedId.value}/candidates`)
    candidates.value = res.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load eligible applicants.' }
  } finally {
    loadingCandidates.value = false
  }
}

function formatAmount(amount) {
  return amount == null ? null : Number(amount).toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}

function openAward(candidate) {
  awarding.value = {
    candidate,
    // A new award starts at the scholarship's award amount
    amount: candidate.award ? candidate.award.amount : (selected.value?.AwardAmount ?? null),
    note: candidate.award?.note ?? '',
  }
}

async function saveAward() {
  saving.value = true
  alert.value = null
  const { candidate, amount, note } = awarding.value
  try {
    await api.post(`/awards/scholarships/${selectedId.value}/awards`, {
      applicationId: candidate.applicationId,
      amount: amount === '' || amount == null ? null : Number(amount),
      note: note || null,
    })
    const wasNew = !candidate.award
    awarding.value = null
    await loadCandidates()
    const s = scholarships.value.find((x) => x.Id === selectedId.value)
    if (s && wasNew) s.AwardCount++
    alert.value = { type: 'success', message: wasNew ? `${selected.value.Name} awarded to ${candidate.applicantName}.` : 'Award updated.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to save the award.' }
  } finally {
    saving.value = false
  }
}

async function removeAward(candidate) {
  if (!await confirm({
    title: 'Remove award',
    text: `Take ${selected.value.Name} away from ${candidate.applicantName}? If it's their only scholarship, their application goes back to Under Review.`,
    confirmText: 'Remove',
    color: 'error',
  })) return
  try {
    await api.delete(`/awards/scholarships/${selectedId.value}/awards/${candidate.applicationId}`)
    await loadCandidates()
    const s = scholarships.value.find((x) => x.Id === selectedId.value)
    if (s) s.AwardCount = Math.max(0, s.AwardCount - 1)
    alert.value = { type: 'success', message: 'Award removed.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to remove the award.' }
  }
}
</script>

<template>
  <div class="space-y-6 max-w-6xl">
    <div>
      <h1 class="page-title">Award Scholarships</h1>
      <p class="page-subtitle">Choose a scholarship to see the submitted applications that are eligible for it.</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading…
    </div>

    <v-card v-else-if="scholarships.length === 0">
      <v-empty-state icon="mdi-trophy-outline" title="No scholarships to award"
        text="You haven't been assigned to award any scholarships yet." />
    </v-card>

    <template v-else>
      <v-card data-tour="scholarship-select">
        <v-card-text class="pa-6 space-y-2">
          <v-select
            v-model="selectedId"
            :items="scholarshipItems"
            item-title="title"
            item-value="value"
            label="Scholarship"
            hide-details
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
            </template>
          </v-select>
          <p v-if="selected?.Description" class="text-sm text-slate-400 whitespace-pre-line">{{ selected.Description }}</p>
          <p v-if="selected" class="text-xs text-slate-400">
            {{ selected.Eligibility === 'open' ? 'Open to every applicant.' : 'Only applicants whose answers meet its eligibility rule are listed.' }}
            <template v-if="selected.AwardAmount != null"> Award amount: {{ formatAmount(selected.AwardAmount) }}.</template>
          </p>
        </v-card-text>
      </v-card>

      <v-card v-if="selectedId && letterInfo" title="Award letter" data-tour="award-letter">
        <v-card-text class="pa-6 flex flex-wrap items-center gap-3">
          <p class="text-sm text-slate-400 flex-1 min-w-[16rem]">{{ letterText }}</p>
          <v-btn variant="outlined" prepend-icon="mdi-pencil" @click="letterEditorOpen = true">
            {{ letterInfo.own ? 'Edit letter' : 'Write its own letter' }}
          </v-btn>
          <v-btn
            color="primary"
            prepend-icon="mdi-file-pdf-box"
            :disabled="!hasLetter || awardedCount === 0"
            :loading="downloading === 'all'"
            @click="download(`/scholarships/${selectedId}/letters`, 'all')"
          >
            All letters (PDF)
          </v-btn>
        </v-card-text>
      </v-card>

      <v-card v-if="selectedId" class="overflow-hidden" data-tour="candidates" :title="`Eligible applicants (${candidates.length}) · ${awardedCount} awarded`">
        <v-data-table
          :headers="headers"
          :items="candidates"
          :loading="loadingCandidates"
          no-data-text="No submitted applications are eligible for this scholarship yet."
        >
          <template #item.applicantName="{ item }">
            <p class="font-semibold text-navy">{{ item.applicantName }}</p>
            <p class="text-xs text-slate-400">{{ item.email }}</p>
          </template>
          <template #item.schoolName="{ value }">{{ value || '—' }}</template>
          <template #item.status="{ value }"><StatusBadge :status="value" /></template>
          <template #item.averageScore="{ item }">{{ item.averageScore ?? '—' }}</template>
          <template #item.award="{ item }">
            <template v-if="item.award">
              <v-chip color="success" prepend-icon="mdi-trophy">{{ formatAmount(item.award.amount) ?? 'Awarded' }}</v-chip>
              <p v-if="item.award.note" class="text-xs text-slate-400 mt-1">{{ item.award.note }}</p>
            </template>
            <span v-else class="text-slate-400">—</span>
          </template>
          <template #item.actions="{ item }">
            <template v-if="item.award">
              <v-btn
                variant="text"
                size="small"
                prepend-icon="mdi-file-pdf-box"
                :disabled="!hasLetter"
                :loading="downloading === item.applicationId"
                @click="download(`/scholarships/${selectedId}/letters/${item.applicationId}`, item.applicationId)"
              >
                Letter
              </v-btn>
              <v-btn variant="text" color="primary" size="small" @click="openAward(item)">Edit</v-btn>
              <v-btn variant="text" color="error" size="small" @click="removeAward(item)">Remove</v-btn>
            </template>
            <v-btn v-else color="primary" size="small" prepend-icon="mdi-trophy-outline" @click="openAward(item)">Award</v-btn>
          </template>
        </v-data-table>
      </v-card>
    </template>
  </div>

  <v-dialog :model-value="!!awarding" max-width="520" @update:model-value="(open) => { if (!open) awarding = null }">
    <v-card v-if="awarding" :title="`${awarding.candidate.award ? 'Edit award' : 'Award'}: ${selected?.Name}`">
      <template #append>
        <v-btn icon="mdi-close" variant="text" @click="awarding = null" />
      </template>
      <v-card-text class="pa-6 space-y-4">
        <p class="text-sm text-navy">To <strong>{{ awarding.candidate.applicantName }}</strong></p>
        <v-text-field v-model="awarding.amount" type="number" min="0" step="0.01" prefix="$" label="Amount (optional)" hide-details="auto" />
        <v-textarea v-model="awarding.note" label="Note (optional)" rows="2" hide-details="auto" />
        <div class="flex gap-3">
          <v-btn color="primary" :loading="saving" @click="saveAward">{{ awarding.candidate.award ? 'Save' : 'Award Scholarship' }}</v-btn>
          <v-btn variant="outlined" @click="awarding = null">Cancel</v-btn>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>

  <LetterEditorDialog
    v-if="letterEditorOpen && letterInfo"
    v-model="letterEditorOpen"
    :title="`${selected?.Name}: award letter`"
    :cycle-id="letterInfo.cycleId"
    :letter="letterInfo.own"
    :base-letters="letterInfo.baseLetters"
    :preview-url="`/scholarships/${selectedId}/letter/preview`"
    :save="saveLetter"
  />
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseModal from '../../components/common/BaseModal.vue'
import BaseInput from '../../components/common/BaseInput.vue'

const router = useRouter()

const schools = ref([])
const loading = ref(true)
const alert = ref(null)
const showAddModal = ref(false)
const addingSchool = ref(false)

const newSchool = ref({
  name: '',
  address: '',
  city: '',
  state: '',
})

onMounted(async () => {
  await loadSchools()
})

async function loadSchools() {
  loading.value = true
  try {
    const response = await api.get('/schools')
    schools.value = response.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load schools.' }
  } finally {
    loading.value = false
  }
}

async function addSchool() {
  addingSchool.value = true
  try {
    const response = await api.post('/schools', newSchool.value)
    schools.value.push(response.data)
    showAddModal.value = false
    newSchool.value = { name: '', address: '', city: '', state: '' }
    alert.value = { type: 'success', message: 'School added successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to add school.' }
  } finally {
    addingSchool.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="page-title">Schools</h1>
        <p class="page-subtitle">Manage school records and counselors.</p>
      </div>
      <BaseButton @click="showAddModal = true">
        <span class="mdi mdi-plus mr-1.5"></span>
        Add School
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading schools…
    </div>

    <div v-else class="card overflow-hidden">
      <table class="dialect-table">
        <thead>
          <tr>
            <th>School Name</th>
            <th>Location</th>
            <th>Counselors</th>
            <th>Applicants</th>
            <th class="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="school in schools"
            :key="school.id"
            @click="router.push(`/schools/${school.id}`)"
          >
            <td>
              <p class="font-semibold text-navy">{{ school.name }}</p>
            </td>
            <td class="text-slate-400">
              {{ [school.city, school.state].filter(Boolean).join(', ') || '—' }}
            </td>
            <td class="text-slate-400">{{ school.counselors_count ?? '—' }}</td>
            <td class="text-slate-400">{{ school.applicants_count ?? '—' }}</td>
            <td class="text-right">
              <button
                @click.stop="router.push(`/schools/${school.id}`)"
                class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors"
              >
                Manage
              </button>
            </td>
          </tr>
          <tr v-if="schools.length === 0">
            <td colspan="5" class="px-6 py-10 text-center text-sm text-slate-400">No schools found.</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Add School Modal -->
    <BaseModal :show="showAddModal" title="Add New School" @close="showAddModal = false">
      <div class="space-y-4">
        <BaseInput v-model="newSchool.name" label="School Name" placeholder="Lincoln High School" :required="true" />
        <BaseInput v-model="newSchool.address" label="Address" placeholder="123 Main St" />
        <div class="grid grid-cols-2 gap-3">
          <BaseInput v-model="newSchool.city" label="City" />
          <BaseInput v-model="newSchool.state" label="State" />
        </div>
        <div class="flex justify-end gap-3 pt-2">
          <BaseButton variant="secondary" @click="showAddModal = false">Cancel</BaseButton>
          <BaseButton :loading="addingSchool" @click="addSchool">Add School</BaseButton>
        </div>
      </div>
    </BaseModal>
  </div>
</template>

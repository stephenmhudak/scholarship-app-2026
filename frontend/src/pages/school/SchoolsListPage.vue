<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseModal from '../../components/common/BaseModal.vue'

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
        <h1 class="text-2xl font-bold text-gray-900">Schools</h1>
        <p class="text-gray-500 mt-1">Manage school records and counselors.</p>
      </div>
      <BaseButton @click="showAddModal = true">
        <span class="mdi mdi-plus mr-1"></span>
        Add School
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading schools...
    </div>

    <div v-else class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">School Name</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Location</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Counselors</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Applicants</th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
          </tr>
        </thead>
        <tbody class="bg-white divide-y divide-gray-100">
          <tr
            v-for="school in schools"
            :key="school.id"
            class="hover:bg-gray-50 cursor-pointer transition-colors"
            @click="router.push(`/schools/${school.id}`)"
          >
            <td class="px-6 py-4">
              <p class="text-sm font-medium text-gray-900">{{ school.name }}</p>
            </td>
            <td class="px-6 py-4 text-sm text-gray-600">
              {{ [school.city, school.state].filter(Boolean).join(', ') || '—' }}
            </td>
            <td class="px-6 py-4 text-sm text-gray-600">{{ school.counselors_count ?? '—' }}</td>
            <td class="px-6 py-4 text-sm text-gray-600">{{ school.applicants_count ?? '—' }}</td>
            <td class="px-6 py-4 text-right">
              <button
                @click.stop="router.push(`/schools/${school.id}`)"
                class="text-sm text-blue-600 hover:text-blue-700 font-medium"
              >
                Manage
              </button>
            </td>
          </tr>
          <tr v-if="schools.length === 0">
            <td colspan="5" class="px-6 py-8 text-center text-sm text-gray-500">No schools found.</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Add School Modal -->
    <BaseModal :show="showAddModal" title="Add New School" @close="showAddModal = false">
      <div class="space-y-4">
        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">School Name</label>
          <input
            v-model="newSchool.name"
            type="text"
            required
            placeholder="Lincoln High School"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">Address</label>
          <input
            v-model="newSchool.address"
            type="text"
            placeholder="123 Main St"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div class="grid grid-cols-2 gap-3">
          <div class="space-y-1">
            <label class="block text-sm font-medium text-gray-700">City</label>
            <input
              v-model="newSchool.city"
              type="text"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div class="space-y-1">
            <label class="block text-sm font-medium text-gray-700">State</label>
            <input
              v-model="newSchool.state"
              type="text"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
        <div class="flex justify-end gap-3 pt-2">
          <BaseButton variant="secondary" @click="showAddModal = false">Cancel</BaseButton>
          <BaseButton :loading="addingSchool" @click="addSchool">Add School</BaseButton>
        </div>
      </div>
    </BaseModal>
  </div>
</template>

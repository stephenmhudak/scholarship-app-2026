<script setup>
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

function logout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <header class="bg-blue-700 text-white shadow-md z-10">
    <div class="flex items-center justify-between px-6 py-3">
      <div class="flex items-center gap-3">
        <span class="mdi mdi-school text-2xl"></span>
        <span class="text-xl font-semibold tracking-wide">Scholarship Portal</span>
      </div>
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-2 text-sm">
          <span class="mdi mdi-account-circle text-xl"></span>
          <span>{{ authStore.user?.name || authStore.user?.email || 'User' }}</span>
          <span v-if="authStore.role" class="bg-blue-500 text-white text-xs px-2 py-0.5 rounded-full capitalize">
            {{ authStore.role?.replace('_', ' ') }}
          </span>
        </div>
        <button
          @click="logout"
          class="flex items-center gap-1 text-sm bg-blue-800 hover:bg-blue-900 px-3 py-1.5 rounded-lg transition-colors"
        >
          <span class="mdi mdi-logout"></span>
          Logout
        </button>
      </div>
    </div>
  </header>
</template>

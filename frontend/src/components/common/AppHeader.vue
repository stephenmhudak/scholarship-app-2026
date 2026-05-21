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
  <header class="bg-white border-b border-[#E9EDF7] h-16 flex items-center px-6 z-10 shrink-0 shadow-sm">
    <div class="flex items-center justify-between w-full">
      <!-- Logo -->
      <div class="flex items-center gap-3">
        <div class="w-9 h-9 bg-primary rounded-xl flex items-center justify-center shadow-card">
          <span class="mdi mdi-school text-white text-lg"></span>
        </div>
        <span class="text-base font-bold text-navy tracking-tight">Scholarship Portal</span>
      </div>

      <!-- User info -->
      <div class="flex items-center gap-3">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center">
            <span class="mdi mdi-account text-primary text-sm"></span>
          </div>
          <div class="text-sm leading-tight hidden sm:block">
            <p class="font-semibold text-navy">{{ authStore.user?.name || authStore.user?.email || 'User' }}</p>
            <p v-if="authStore.role" class="text-slate-400 text-xs capitalize">
              {{ authStore.role?.replace('_', ' ') }}
            </p>
          </div>
        </div>
        <button
          @click="logout"
          class="flex items-center gap-1.5 text-sm font-medium text-slate-400 hover:text-danger transition-colors px-2 py-1.5 rounded-lg hover:bg-danger/5"
        >
          <span class="mdi mdi-logout text-base"></span>
          <span class="hidden sm:inline">Logout</span>
        </button>
      </div>
    </div>
  </header>
</template>

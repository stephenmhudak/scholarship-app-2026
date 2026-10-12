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
  <v-app-bar flat border="b" height="64" class="px-4">
    <!-- Logo -->
    <div class="flex items-center gap-3">
      <v-avatar color="primary" size="36" rounded="lg">
        <v-icon icon="mdi-school" size="20" />
      </v-avatar>
      <span class="text-base font-bold text-navy tracking-tight">Scholarship Portal</span>
    </div>

    <v-spacer />

    <!-- User info -->
    <div class="flex items-center gap-3">
      <div class="flex items-center gap-2.5">
        <v-avatar color="primary" variant="tonal" size="32">
          <v-icon icon="mdi-account" size="18" />
        </v-avatar>
        <div class="text-sm leading-tight hidden sm:block">
          <p class="font-semibold text-navy">{{ authStore.user?.name || authStore.user?.email || 'User' }}</p>
          <p v-if="authStore.role" class="text-slate-400 text-xs capitalize">
            {{ authStore.role?.replace('_', ' ') }}
          </p>
        </div>
      </div>
      <v-btn variant="text" prepend-icon="mdi-account-cog-outline" to="/account">
        <span class="hidden sm:inline">Account</span>
      </v-btn>
      <v-btn variant="text" color="error" prepend-icon="mdi-logout" @click="logout">
        <span class="hidden sm:inline">Logout</span>
      </v-btn>
    </div>
  </v-app-bar>
</template>

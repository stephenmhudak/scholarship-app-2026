<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useToursStore } from '../../stores/tours'
import { roleHome } from '../../utils/roleHome'
import { useNavDrawer } from '../../composables/useNavDrawer'
import { roleLabel } from '../../utils/roleLabels'

const router = useRouter()
const authStore = useAuthStore()
const tours = useToursStore()
const { isOverlay, toggle } = useNavDrawer()
const ending = ref(false)

const displayName = computed(() =>
  `${authStore.user?.firstName ?? ''} ${authStore.user?.lastName ?? ''}`.trim() || authStore.user?.email || 'User')

function logout() {
  authStore.logout()
  router.push('/login')
}

// Back to the super admin's own account; a full reload so nothing from the impersonated user stays on screen.
// Only if there's no way back (their own session has ended too) do they sign in again.
async function endImpersonation() {
  ending.value = true
  if (await authStore.endImpersonation()) {
    window.location.assign('/admin/settings')
    return
  }
  ending.value = false
  logout()
}
</script>

<template>
  <v-app-bar flat border="b" height="64" class="px-2 sm:px-4">
    <v-app-bar-nav-icon v-if="isOverlay" aria-label="Open menu" data-tour="nav-toggle" @click="toggle" />
    <!-- Logo -->
    <div class="flex items-center gap-3 min-w-0">
      <v-avatar color="primary" size="36" rounded="lg">
        <v-icon icon="mdi-school" size="20" />
      </v-avatar>
      <span class="text-base font-bold text-navy tracking-tight truncate">Scholarship Portal</span>
    </div>

    <v-spacer />

    <!-- User info -->
    <div class="flex items-center gap-1 sm:gap-3">
      <!-- Replays this page's guided tour (PageTour) -->
      <v-btn
        v-if="tours.currentTour"
        icon="mdi-help-circle-outline"
        variant="text"
        size="small"
        title="Take a tour of this page"
        aria-label="Take a tour of this page"
        data-tour="tour-button"
        @click="tours.replay()"
      />
      <div class="hidden sm:flex items-center gap-2.5">
        <v-avatar color="primary" variant="tonal" size="32">
          <v-icon icon="mdi-account" size="18" />
        </v-avatar>
        <div class="text-sm leading-tight hidden sm:block">
          <p class="font-semibold text-navy">{{ displayName }}</p>
          <p v-if="authStore.role" class="text-slate-400 text-xs">
            {{ roleLabel(authStore.role) }}
          </p>
        </div>
      </div>
      <v-btn variant="text" prepend-icon="mdi-account-cog-outline" to="/account" data-tour="account-link">
        <span class="hidden sm:inline">Account</span>
      </v-btn>
      <template v-if="authStore.isImpersonating">
        <v-chip color="warning" prepend-icon="mdi-incognito" class="hidden md:flex" :title="`Signed in as ${authStore.impersonatorName}`">
          Viewing as {{ displayName }}
        </v-chip>
        <v-btn color="warning" variant="flat" prepend-icon="mdi-account-arrow-left" :loading="ending" @click="endImpersonation">
          <span class="hidden sm:inline">End impersonation</span>
        </v-btn>
      </template>
      <v-btn v-else variant="text" color="error" prepend-icon="mdi-logout" @click="logout">
        <span class="hidden sm:inline">Logout</span>
      </v-btn>
    </div>
  </v-app-bar>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import TotpSetup from '../../components/auth/TotpSetup.vue'

const router = useRouter()
const route = useRoute()

const settingUp = ref(false)

// Only same-app paths, so the query can't send users off-site
const next = computed(() => {
  const target = route.query.next
  return typeof target === 'string' && target.startsWith('/') && !target.startsWith('//') ? target : '/'
})

function continueToApp() {
  router.replace(next.value)
}
</script>

<template>
  <div class="space-y-5">
    <div class="text-center space-y-3">
      <v-avatar color="primary" variant="tonal" size="48" rounded="xl">
        <v-icon icon="mdi-shield-lock-outline" size="26" />
      </v-avatar>
      <div>
        <h2 class="text-xl font-bold text-navy">Protect your account</h2>
        <p class="text-sm text-slate-400 mt-1">
          Add two-factor authentication so signing in also needs a code from an authenticator app on your phone.
          It keeps your account safe even if someone learns your password.
        </p>
      </div>
    </div>

    <TotpSetup v-if="settingUp" @done="continueToApp" @cancel="settingUp = false" />

    <div v-else class="space-y-3">
      <v-btn color="primary" size="large" block prepend-icon="mdi-shield-check-outline" @click="settingUp = true">
        Set up two-factor
      </v-btn>
      <v-btn variant="text" block @click="continueToApp">Not now</v-btn>
      <p class="text-xs text-center text-slate-400">You can turn it on any time from your Account page.</p>
    </div>
  </div>
</template>

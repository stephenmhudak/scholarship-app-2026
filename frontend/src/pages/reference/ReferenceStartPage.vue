<script setup>
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { normalizeReferenceCode, savePendingReferenceCode, REFERENCE_CODE_RE } from '../../utils/pendingReference'

// Where a reference link sends someone who isn't signed in with an account that can write references.
// The code is remembered so they land on it after signing in or creating an account.
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const code = normalizeReferenceCode(route.query.code)
const validCode = REFERENCE_CODE_RE.test(code)
// Signed in, but as an applicant, scorer or admin
const wrongAccount = computed(() => authStore.isAuthenticated)

onMounted(() => savePendingReferenceCode(code))

function signOut() {
  authStore.logout()
  savePendingReferenceCode(code)
  router.replace({ path: '/login' })
}
</script>

<template>
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Write a reference</h2>
      <p class="text-sm text-slate-400 mt-1">
        References are submitted from an account, so you can come back later and see the references you've written.
      </p>
    </div>

    <div v-if="validCode" class="rounded-lg bg-[#F4F7FE] pa-4 text-center">
      <p class="eyebrow">Your reference code</p>
      <p class="text-2xl font-bold text-navy font-mono tracking-widest">{{ code }}</p>
      <p class="text-xs text-slate-400 mt-1">If you finish signing up on another device, enter this code there.</p>
    </div>

    <template v-if="wrongAccount">
      <v-alert type="warning">
        You're signed in as <strong>{{ authStore.user?.email }}</strong>, which can't submit references.
        Sign out, then sign in with a Reference Provider, counselor or school admin account, or create a Reference Provider account.
      </v-alert>
      <v-btn color="primary" size="large" block @click="signOut">Sign out</v-btn>
    </template>

    <template v-else>
      <v-btn color="primary" size="large" block to="/register?as=reference-provider">Create a Reference Provider account</v-btn>
      <v-btn variant="outlined" size="large" block to="/login">I already have an account</v-btn>
      <p class="text-xs text-slate-400 text-center">
        Counselors and school admins can sign in with their existing account.
      </p>
    </template>
  </div>
</template>

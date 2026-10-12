<script setup>
import { ref } from 'vue'
import TotpSetup from './TotpSetup.vue'
import EmailOtpSetup from './EmailOtpSetup.vue'

const emit = defineEmits(['done', 'cancel'])

const method = ref(null)

const methods = [
  {
    value: 'totp',
    icon: 'mdi-cellphone-key',
    title: 'Authenticator app',
    subtitle: 'Recommended. Codes come from an app on your phone, so they stay safe even if your email is compromised.',
  },
  {
    value: 'email',
    icon: 'mdi-email-lock-outline',
    title: 'Email code',
    subtitle: "No app needed. We'll email you a code each time you sign in.",
  },
]
</script>

<template>
  <TotpSetup v-if="method === 'totp'" @done="emit('done')" @cancel="method = null" />
  <EmailOtpSetup v-else-if="method === 'email'" @done="emit('done')" @cancel="method = null" />

  <div v-else class="space-y-3">
    <p class="field-label">Choose how you'll get your sign-in codes</p>
    <v-list class="py-0" lines="three">
      <v-list-item
        v-for="m in methods"
        :key="m.value"
        :prepend-icon="m.icon"
        :title="m.title"
        :subtitle="m.subtitle"
        append-icon="mdi-chevron-right"
        border
        rounded="lg"
        class="mb-2"
        @click="method = m.value"
      />
    </v-list>
    <v-btn variant="text" @click="emit('cancel')">Cancel</v-btn>
  </div>
</template>

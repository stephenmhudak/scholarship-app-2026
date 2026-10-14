<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import DefaultLayout from './layouts/DefaultLayout.vue'
import AuthLayout from './layouts/AuthLayout.vue'
import ConfirmDialog from './components/common/ConfirmDialog.vue'

const route = useRoute()
const layouts = { auth: AuthLayout }
const layout = computed(() => layouts[route.meta.layout] ?? DefaultLayout)
</script>

<template>
  <v-app>
    <component :is="layout" />
    <ConfirmDialog />
  </v-app>
</template>

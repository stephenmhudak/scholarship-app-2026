<script setup>
import AppHeader from '../components/common/AppHeader.vue'
import AppSidebar from '../components/common/AppSidebar.vue'
</script>

<template>
  <AppHeader />
  <AppSidebar />
  <v-main>
    <div class="p-6 lg:p-8">
      <!-- Keyed by path so a page reused across routes or ids (e.g. CycleQuestionsPage for application and reference
           questions, which reads its params once in setup) gets a fresh instance instead of keeping the old one -->
      <RouterView v-slot="{ Component, route }">
        <component :is="Component" :key="route.path" />
      </RouterView>
    </div>
  </v-main>
</template>

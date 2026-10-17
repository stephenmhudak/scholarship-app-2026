<script setup>
import AppHeader from '../components/common/AppHeader.vue'
import AppSidebar from '../components/common/AppSidebar.vue'
</script>

<template>
  <AppHeader />
  <AppSidebar />
  <v-main>
    <div class="p-4 sm:p-6 lg:p-8">
      <!-- Keyed by the top-level route and its params so a page reused across routes or ids (e.g. CycleQuestionsPage for
           application and reference questions, which reads its params once in setup) gets a fresh instance instead of
           keeping the old one, while a page with tabs (child routes, e.g. CyclePage) stays put as its tabs change -->
      <RouterView v-slot="{ Component, route }">
        <component :is="Component" :key="`${route.matched[0]?.path}|${JSON.stringify(route.params)}`" />
      </RouterView>
    </div>
  </v-main>
</template>

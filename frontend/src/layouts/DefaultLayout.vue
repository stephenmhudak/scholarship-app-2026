<script setup>
import AppHeader from '../components/common/AppHeader.vue'
import AppSidebar from '../components/common/AppSidebar.vue'
</script>

<template>
  <AppHeader />
  <AppSidebar />
  <v-main>
    <div class="p-6 lg:p-8">
      <RouterView />
    </div>
  </v-main>
</template>

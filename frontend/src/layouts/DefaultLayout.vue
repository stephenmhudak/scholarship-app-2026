<script setup>
import AppHeader from '../components/common/AppHeader.vue'
import AppSidebar from '../components/common/AppSidebar.vue'
</script>

<template>
  <div class="min-h-screen flex flex-col bg-page">
    <AppHeader />
    <div class="flex flex-1 overflow-hidden">
      <AppSidebar />
      <main class="flex-1 overflow-y-auto p-6 lg:p-8">
        <RouterView />
      </main>
    </div>
  </div>
</template>

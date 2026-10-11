<script setup>
</script>

<template>
  <v-main class="flex items-center justify-center px-4 py-10">
    <div class="w-full max-w-md">
      <!-- Brand header -->
      <div class="text-center mb-8">
        <v-avatar color="primary" size="56" rounded="xl" class="shadow-card-md mb-4">
          <v-icon icon="mdi-school" size="32" />
        </v-avatar>
        <h1 class="text-2xl font-bold text-navy">Scholarship Portal</h1>
        <p class="text-slate-400 text-sm mt-1">Empowering Students to Succeed</p>
      </div>

      <v-card class="pa-8">
        <RouterView />
      </v-card>
    </div>
  </v-main>
</template>

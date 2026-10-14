<script setup>
</script>

<template>
  <!-- For people without an account filling in a longer form (reference links) -->
  <v-main class="px-4 py-10">
    <div class="w-full max-w-3xl mx-auto">
      <div class="flex items-center gap-3 mb-8">
        <v-avatar color="primary" size="44" rounded="lg" class="shadow-card-md">
          <v-icon icon="mdi-school" size="26" />
        </v-avatar>
        <div>
          <p class="text-lg font-bold text-navy leading-tight">Scholarship Portal</p>
          <p class="text-slate-400 text-sm">Empowering Students to Succeed</p>
        </div>
      </div>
      <RouterView />
    </div>
  </v-main>
</template>

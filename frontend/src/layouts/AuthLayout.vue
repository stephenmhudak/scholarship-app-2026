<script setup>
</script>

<template>
  <div class="min-h-screen bg-page flex items-center justify-center px-4">
    <div class="w-full max-w-md">
      <!-- Brand header -->
      <div class="text-center mb-8">
        <div class="inline-flex items-center justify-center w-14 h-14 bg-primary rounded-2xl shadow-card-md mb-4">
          <span class="mdi mdi-school text-white text-3xl"></span>
        </div>
        <h1 class="text-2xl font-bold text-navy">Scholarship Portal</h1>
        <p class="text-slate-400 text-sm mt-1">Empowering Students to Succeed</p>
      </div>

      <!-- Card -->
      <div class="card p-8">
        <RouterView />
      </div>
    </div>
  </div>
</template>

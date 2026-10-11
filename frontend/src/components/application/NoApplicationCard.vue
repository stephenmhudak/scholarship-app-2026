<template>
  <div class="section-card text-center py-10 space-y-4">
    <span class="mdi mdi-file-document-outline text-4xl text-slate-400 block"></span>
    <div>
      <p class="text-base font-bold text-navy">No application started</p>
      <p class="text-sm text-slate-400 mt-1">You haven't started an application yet.</p>
    </div>
    <RouterLink
      to="/application/new"
      class="inline-flex items-center gap-1.5 text-sm font-semibold bg-primary text-white px-4 py-2.5 rounded-xl hover:bg-primary-700 transition-colors shadow-sm"
    >
      <span class="mdi mdi-plus"></span>
      Start Application
    </RouterLink>
  </div>
</template>

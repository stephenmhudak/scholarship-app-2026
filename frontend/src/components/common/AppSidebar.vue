<script setup>
import { computed } from 'vue'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()

const navLinks = computed(() => {
  const role = authStore.role

  if (role === 'applicant') {
    return [
      { label: 'Dashboard', icon: 'mdi-view-dashboard-outline', to: '/dashboard' },
      { label: 'My Application', icon: 'mdi-file-document-edit-outline', to: '/application/current' },
      { label: 'Status', icon: 'mdi-clipboard-check-outline', to: '/application/current/status' },
    ]
  }
  if (role === 'scorer') {
    return [
      { label: 'Scoring Queue', icon: 'mdi-format-list-checks', to: '/scoring' },
    ]
  }
  if (role === 'app_admin') {
    return [
      { label: 'Applications', icon: 'mdi-folder-multiple-outline', to: '/admin/applications' },
      { label: 'Scoring', icon: 'mdi-chart-bar', to: '/admin/scoring' },
      { label: 'Settings', icon: 'mdi-cog-outline', to: '/admin/settings' },
    ]
  }
  if (role === 'school_admin') {
    return [
      { label: 'Schools', icon: 'mdi-domain', to: '/schools' },
    ]
  }
  if (role === 'counselor') {
    return [
      { label: 'Dashboard', icon: 'mdi-view-dashboard-outline', to: '/counselor' },
    ]
  }
  return []
})
</script>

<template>
  <aside class="w-60 bg-white border-r border-[#E9EDF7] flex flex-col shrink-0">
    <nav class="flex-1 py-5 overflow-y-auto">
      <p class="px-5 mb-2 text-[10px] font-bold uppercase tracking-widest text-slate-400">Navigation</p>
      <ul class="space-y-0.5 px-3">
        <li v-for="link in navLinks" :key="link.to">
          <RouterLink
            :to="link.to"
            class="flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-navy/70 hover:bg-[#F4F7FE] hover:text-primary transition-colors group"
            active-class="bg-primary/8 text-primary font-semibold"
          >
            <span
              :class="`mdi ${link.icon} text-xl text-navy/40 group-hover:text-primary transition-colors`"
            ></span>
            {{ link.label }}
          </RouterLink>
        </li>
      </ul>
    </nav>
    <div class="p-4 border-t border-[#E9EDF7]">
      <p class="text-[10px] text-slate-400 text-center">Scholarship App &copy; 2026</p>
    </div>
  </aside>
</template>

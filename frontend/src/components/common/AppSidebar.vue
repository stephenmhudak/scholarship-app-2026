<script setup>
import { computed } from 'vue'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const schoolId = computed(() => authStore.user?.schoolId)

const navLinks = computed(() => {
  const all = [
    { label: 'Dashboard',             icon: 'mdi-view-dashboard-outline',      to: '/dashboard',                  permission: 'view_dashboard' },
    { label: 'My Application',        icon: 'mdi-file-document-edit-outline',  to: '/application/current',        permission: 'manage_application' },
    { label: 'Status',                icon: 'mdi-clipboard-check-outline',     to: '/application/current/status', permission: 'manage_application' },
    { label: 'Scoring Queue',         icon: 'mdi-format-list-checks',          to: '/scoring',                    permission: 'view_scoring_queue' },
    { label: 'Applications',          icon: 'mdi-folder-multiple-outline',     to: '/admin/applications',         permission: 'admin_applications' },
    { label: 'Scoring',               icon: 'mdi-chart-bar',                   to: '/admin/scoring',              permission: 'view_scoring_overview' },
    { label: 'Settings',              icon: 'mdi-account-cog-outline',         to: '/admin/settings',             permission: 'admin_settings' },
    { label: 'Scholarship Settings',  icon: 'mdi-cog-outline',                 to: '/admin/scholarship-settings', permission: 'admin_scholarship' },
    { label: 'My School',             icon: 'mdi-domain',                      to: schoolId.value ? `/schools/${schoolId.value}` : '/schools', permission: 'manage_school' },
    { label: 'Dashboard',             icon: 'mdi-view-dashboard-outline',      to: '/counselor',                  permission: 'view_counselor_dashboard' },
  ]
  return all.filter(item => authStore.hasPermission(item.permission))
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
  </aside>
</template>

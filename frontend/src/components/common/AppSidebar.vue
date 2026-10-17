<script setup>
import { computed } from 'vue'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()
const schoolId = computed(() => authStore.user?.schoolId)

const navLinks = computed(() => {
  const all = [
    { label: 'Dashboard',             icon: 'mdi-view-dashboard-outline',      to: '/dashboard',                  permission: 'view_dashboard' },
    { label: 'Scoring Queue',         icon: 'mdi-format-list-checks',          to: '/scoring',                    permission: 'view_scoring_queue' },
    { label: 'Applications',          icon: 'mdi-folder-multiple-outline',     to: '/admin/applications',         permission: 'admin_applications' },
    { label: 'Scoring',               icon: 'mdi-chart-bar',                   to: '/admin/scoring',              permission: 'view_scoring_overview' },
    { label: 'Settings',              icon: 'mdi-account-cog-outline',         to: '/admin/settings',             permission: 'admin_settings' },
    { label: 'Scholarship Settings',  icon: 'mdi-cog-outline',                 to: '/admin/scholarship-settings', permission: 'admin_scholarship' },
    { label: 'My School',             icon: 'mdi-domain',                      to: schoolId.value ? `/schools/${schoolId.value}` : '/schools', permission: 'manage_school' },
    { label: 'Students',              icon: 'mdi-account-school-outline',      to: '/counselor',                  permission: 'view_counselor_dashboard' },
    { label: 'Award Scholarships',    icon: 'mdi-trophy-outline',              to: '/awards',                     permission: 'award_scholarships' },
    { label: 'My References',         icon: 'mdi-account-voice',               to: '/references',                 permission: 'submit_references' },
  ]
  return all.filter(item => authStore.hasPermission(item.permission))
})
</script>

<template>
  <v-navigation-drawer permanent width="240" border="e" floating>
    <v-list nav density="comfortable" color="primary" class="px-3 py-5">
      <v-list-subheader class="text-[10px] font-bold uppercase tracking-widest">Navigation</v-list-subheader>
      <v-list-item
        v-for="link in navLinks"
        :key="link.to"
        :to="link.to"
        :prepend-icon="link.icon"
        :title="link.label"
        slim
        rounded="xl"
      />
    </v-list>
  </v-navigation-drawer>
</template>

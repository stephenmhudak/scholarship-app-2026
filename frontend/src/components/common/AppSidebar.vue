<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useNavDrawer } from '../../composables/useNavDrawer'

const authStore = useAuthStore()
const route = useRoute()
const { drawer, isOverlay } = useNavDrawer()
const schoolId = computed(() => authStore.user?.schoolId)

// Grouped by what people come to do. `also`: a path prefix that highlights the link too (pages it leads to). Headings only show when someone has more than one group.
const groups = computed(() => {
  const all = [
    { heading: 'Apply', links: [
      { label: 'Dashboard',          icon: 'mdi-view-dashboard-outline',  to: '/dashboard',                  permission: 'view_dashboard' },
    ] },
    { heading: 'Review', links: [
      { label: 'Scoring Queue',      icon: 'mdi-format-list-checks',      to: '/scoring',                    permission: 'view_scoring_queue' },
      { label: 'Award Scholarships', icon: 'mdi-trophy-outline',          to: '/awards',                     permission: 'award_scholarships' },
    ] },
    { heading: 'Manage', links: [
      { label: 'Applications',       icon: 'mdi-folder-multiple-outline', to: '/admin/applications',         permission: 'admin_applications' },
      { label: 'Scoring Progress',   icon: 'mdi-chart-bar',               to: '/admin/scoring',              permission: 'view_scoring_overview' },
      { label: 'Application Periods', icon: 'mdi-calendar-range',         to: '/admin/scholarship-settings', permission: 'admin_scholarship', also: '/admin/cycles/' },
      { label: 'Users & Schools',    icon: 'mdi-account-cog-outline',     to: '/admin/settings',             permission: 'admin_settings' },
    ] },
    { heading: 'School', links: [
      { label: 'My School',          icon: 'mdi-domain',                  to: schoolId.value ? `/schools/${schoolId.value}` : '/schools', permission: 'manage_school' },
      { label: 'Students',           icon: 'mdi-account-school-outline',  to: '/counselor',                  permission: 'view_counselor_dashboard' },
    ] },
    { heading: 'References', links: [
      { label: 'My References',      icon: 'mdi-account-voice',           to: '/references',                 permission: 'submit_references' },
    ] },
  ]
  return all
    .map((g) => ({ ...g, links: g.links.filter((l) => authStore.hasPermission(l.permission)) }))
    .filter((g) => g.links.length > 0)
})
</script>

<template>
  <!-- Fixed beside the page on wide screens; a slide-out menu on small ones (see useNavDrawer) -->
  <v-navigation-drawer v-model="drawer" data-tour="nav" :permanent="!isOverlay" :temporary="isOverlay" width="240" border="e" floating>
    <v-list nav density="comfortable" color="primary" class="px-3 py-5">
      <template v-for="group in groups" :key="group.heading">
        <v-list-subheader v-if="groups.length > 1" class="text-[10px] font-bold uppercase tracking-widest">
          {{ group.heading }}
        </v-list-subheader>
        <v-list-item
          v-for="link in group.links"
          :key="link.to"
          :to="link.to"
          :active="link.also && route.path.startsWith(link.also) ? true : undefined"
          :prepend-icon="link.icon"
          :title="link.label"
          slim
          rounded="xl"
        />
      </template>
    </v-list>
  </v-navigation-drawer>
</template>

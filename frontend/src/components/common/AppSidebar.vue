<script setup>
import { computed } from 'vue'
import { useAuthStore } from '../../stores/auth'

const authStore = useAuthStore()

const navLinks = computed(() => {
  const role = authStore.role

  if (role === 'applicant') {
    return [
      { label: 'Dashboard', icon: 'mdi-view-dashboard', to: '/dashboard' },
      { label: 'My Application', icon: 'mdi-file-document-edit', to: '/application/current' },
      { label: 'Status', icon: 'mdi-clipboard-check', to: '/application/current/status' },
    ]
  }
  if (role === 'scorer') {
    return [
      { label: 'Scoring Queue', icon: 'mdi-format-list-checks', to: '/scoring' },
    ]
  }
  if (role === 'app_admin') {
    return [
      { label: 'Applications', icon: 'mdi-folder-multiple', to: '/admin/applications' },
      { label: 'Settings', icon: 'mdi-cog', to: '/admin/settings' },
    ]
  }
  if (role === 'school_admin') {
    return [
      { label: 'Schools', icon: 'mdi-domain', to: '/schools' },
    ]
  }
  if (role === 'counselor') {
    return [
      { label: 'Dashboard', icon: 'mdi-view-dashboard', to: '/counselor' },
    ]
  }
  return []
})
</script>

<template>
  <aside class="w-56 bg-white border-r border-gray-200 flex flex-col shrink-0">
    <nav class="flex-1 py-4">
      <ul class="space-y-1 px-2">
        <li v-for="link in navLinks" :key="link.to">
          <RouterLink
            :to="link.to"
            class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-gray-700 hover:bg-blue-50 hover:text-blue-700 transition-colors"
            active-class="bg-blue-50 text-blue-700"
          >
            <span :class="`mdi ${link.icon} text-lg`"></span>
            {{ link.label }}
          </RouterLink>
        </li>
      </ul>
    </nav>
    <div class="p-4 border-t border-gray-100 text-xs text-gray-400">
      Scholarship App &copy; 2026
    </div>
  </aside>
</template>

import { ref, computed, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { useRoute } from 'vue-router'

// The sidebar is always shown on wide screens; on phones and small tablets it's a slide-out menu opened from the
// header's menu button and closed again after picking a page. Shared by AppHeader and AppSidebar.
const open = ref(false)

export function useNavDrawer() {
  const { mdAndUp } = useDisplay()
  const route = useRoute()
  const isOverlay = computed(() => !mdAndUp.value)

  watch(() => route.fullPath, () => { if (isOverlay.value) open.value = false })

  return {
    // Wide screens: always open. Small screens: whatever the menu button says.
    drawer: computed({
      get: () => mdAndUp.value || open.value,
      set: (v) => { open.value = v },
    }),
    isOverlay,
    toggle: () => { open.value = !open.value },
  }
}

<script setup>
import { ref, watch, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { VueOnboardingTour } from 'vue-onboarding-tour'
import { useToursStore } from '../../stores/tours'
import { useAuthStore } from '../../stores/auth'

// Runs the current page's guided tour (src/tours): on its own when the user hasn't seen this version of it, or when
// the header's help button asks. Mounted once, in DefaultLayout.
const route = useRoute()
const tours = useToursStore()
const authStore = useAuthStore()

const tourRef = ref(null)
// { tour, steps, last, furthest } while a tour is showing
const active = ref(null)
const runKey = ref(0)
// Each start gets a token; a newer start (or leaving the page) makes an older one give up
let token = 0

const MAX_WAIT_MS = 8000
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

// Showing and not off to the side (a closed slide-out menu); below the fold is fine, the tour scrolls to it
function onScreen(el) {
  if (!el) return false
  const r = el.getBoundingClientRect()
  return r.width > 0 && r.height > 0 && r.right > 0 && r.left < window.innerWidth && getComputedStyle(el).visibility !== 'hidden'
}

// A step's target: the first of its data-tour names that's on screen; undefined target = middle of the screen
function resolveTarget(target) {
  if (!target) return { found: true }
  for (const name of [].concat(target)) {
    const selector = `[data-tour="${name}"]`
    // The library highlights the first match, so that's the one that has to be showing
    const el = document.querySelector(selector)
    if (onScreen(el)) return { found: true, selector }
  }
  return { found: false }
}

// Pages load their data after they appear: wait until no spinner or indeterminate bar (tables loading) has shown for
// a moment. Determinate bars, like the application form's progress, don't count.
async function waitForPage(mine) {
  const started = Date.now()
  let quiet = 0
  while (Date.now() - started < MAX_WAIT_MS) {
    await sleep(150)
    if (mine !== token) return false
    const loading = [...document.querySelectorAll(
      '.v-main .v-progress-circular--indeterminate, .v-main .v-progress-linear__indeterminate, .v-main .v-skeleton-loader',
    )].some((el) => el.getBoundingClientRect().height > 0)
    quiet = loading ? 0 : quiet + 1
    if (quiet >= 3 && Date.now() - started > 500) return true
  }
  return mine === token
}

async function start(tour) {
  const mine = ++token
  if (!await waitForPage(mine)) return
  const steps = tour.steps
    .map((step) => ({ step, target: resolveTarget(step.target) }))
    .filter(({ target }) => target.found)
  if (!steps.length) return

  active.value = {
    tour,
    last: steps.length - 1,
    furthest: 0,
    steps: steps.map(({ step, target }, i) => ({
      target: target.selector,
      title: step.title,
      description: step.description,
      // Runs as each step shows: how far they got decides completed vs dismissed
      afterScript: () => { if (active.value) active.value.furthest = Math.max(active.value.furthest, i) },
    })),
  }
  runKey.value++
  await nextTick()
  tourRef.value?.startTour()
}

function onEnd() {
  const run = active.value
  active.value = null
  if (run) tours.record(run.tour, run.furthest >= run.last ? 'completed' : 'dismissed')
}

function stop() {
  token++
  if (active.value) tourRef.value?.endTour()
}

// A new page: its tour starts on its own if this user hasn't been through this version of it
watch(
  () => [route.name, authStore.role, authStore.user?.id],
  async () => {
    stop()
    const tour = tours.currentTour
    // While impersonating, the tours are the real user's
    if (!tour || authStore.isImpersonating) return
    try {
      await tours.load()
    } catch {
      return
    }
    if (tours.currentTour?.id === tour.id && tours.isDue(tour)) start(tour)
  },
  { immediate: true },
)

watch(() => tours.replayRequest, () => {
  stop()
  if (tours.currentTour) start(tours.currentTour)
})
</script>

<template>
  <VueOnboardingTour
    v-if="active"
    ref="tourRef"
    :key="runKey"
    class="page-tour"
    :tour-id="active.tour.id"
    :steps="active.steps"
    label-terminate="Done"
    @end-tour="onEnd"
  />
</template>

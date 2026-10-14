<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue'

// Draw-to-sign box for a finger, stylus or mouse. v-model is the signature as a JPEG data URL, or null when blank.
defineProps({
  modelValue: { type: String, default: null },
  error: { type: String, default: null },
})
const emit = defineEmits(['update:modelValue'])

const HEIGHT = 180
const INK = '#2B3674'

const wrapper = ref(null)
const canvas = ref(null)
const isEmpty = ref(true)
let ctx = null
let last = null
let resizeObserver = null

// Sizes the canvas to its box at the screen's pixel density, on a white background (JPEG has no transparency)
function reset() {
  const el = canvas.value
  const ratio = window.devicePixelRatio || 1
  const width = wrapper.value.clientWidth
  el.width = Math.round(width * ratio)
  el.height = Math.round(HEIGHT * ratio)
  el.style.width = `${width}px`
  el.style.height = `${HEIGHT}px`
  ctx = el.getContext('2d')
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0)
  ctx.fillStyle = '#FFFFFF'
  ctx.fillRect(0, 0, width, HEIGHT)
  ctx.lineWidth = 2.5
  ctx.lineCap = 'round'
  ctx.lineJoin = 'round'
  ctx.strokeStyle = INK
  ctx.fillStyle = INK
  isEmpty.value = true
}

function point(e) {
  const rect = canvas.value.getBoundingClientRect()
  return { x: e.clientX - rect.left, y: e.clientY - rect.top }
}

function start(e) {
  e.preventDefault()
  canvas.value.setPointerCapture(e.pointerId)
  last = point(e)
  // A tap still leaves a mark
  ctx.beginPath()
  ctx.arc(last.x, last.y, ctx.lineWidth / 2, 0, Math.PI * 2)
  ctx.fill()
  isEmpty.value = false
}

function move(e) {
  if (!last) return
  e.preventDefault()
  const next = point(e)
  ctx.beginPath()
  ctx.moveTo(last.x, last.y)
  ctx.lineTo(next.x, next.y)
  ctx.stroke()
  last = next
}

function end() {
  if (!last) return
  last = null
  emit('update:modelValue', canvas.value.toDataURL('image/jpeg', 0.85))
}

function clear() {
  reset()
  emit('update:modelValue', null)
}

onMounted(() => {
  reset()
  // Re-fit to the new width (e.g. a tablet rotating) while blank; once signed, keep the drawing
  resizeObserver = new ResizeObserver(() => {
    if (isEmpty.value && wrapper.value && canvas.value.width !== Math.round(wrapper.value.clientWidth * (window.devicePixelRatio || 1))) reset()
  })
  resizeObserver.observe(wrapper.value)
})

onBeforeUnmount(() => resizeObserver?.disconnect())
</script>

<template>
  <div>
    <div
      ref="wrapper"
      class="relative rounded-lg border overflow-hidden"
      :class="error ? 'border-danger' : 'border-[#E9EDF7]'"
    >
      <canvas
        ref="canvas"
        class="block cursor-crosshair touch-none"
        role="img"
        aria-label="Signature area. Draw your signature with your finger or mouse."
        @pointerdown="start"
        @pointermove="move"
        @pointerup="end"
        @pointercancel="end"
        @pointerleave="end"
      />
      <p v-if="isEmpty" class="absolute inset-x-0 bottom-3 text-center text-sm text-slate-400 pointer-events-none">
        Sign here with your finger or mouse
      </p>
      <div class="absolute left-6 right-6 bottom-10 border-b border-dashed border-[#E9EDF7] pointer-events-none" />
    </div>
    <div class="flex items-center justify-between mt-1">
      <p class="text-xs" :class="error ? 'text-danger' : 'text-slate-400'">{{ error || ' ' }}</p>
      <v-btn variant="text" size="small" prepend-icon="mdi-eraser" :disabled="isEmpty" @click="clear">Clear</v-btn>
    </div>
  </div>
</template>

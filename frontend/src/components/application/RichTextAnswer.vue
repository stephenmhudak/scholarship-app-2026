<script setup>
import { computed } from 'vue'
import { sanitizeHtml } from '../../utils/richText'

const props = defineProps({
  html: { type: String, default: '' },
})

const safeHtml = computed(() => sanitizeHtml(props.html))
</script>

<template>
  <div v-if="safeHtml" class="rich-text text-sm text-navy" v-html="safeHtml"></div>
  <p v-else class="text-sm text-navy">—</p>
</template>

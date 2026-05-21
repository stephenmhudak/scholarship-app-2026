<script setup>
defineProps({
  column: { type: String, required: true },
  sortKey: { type: String, default: null },
  sortDir: { type: String, default: 'asc' },
  right: { type: Boolean, default: false },
})
defineEmits(['sort'])
</script>

<template>
  <th
    :class="['cursor-pointer select-none group', right ? 'text-right' : '']"
    @click="$emit('sort', column)"
  >
    <span :class="['inline-flex items-center gap-1', right ? 'justify-end w-full' : '']">
      <slot />
      <span class="inline-flex flex-col leading-[1] text-[9px] text-slate-400 group-hover:text-primary transition-colors">
        <span :class="sortKey === column && sortDir === 'asc' ? 'text-primary' : ''">▲</span>
        <span :class="sortKey === column && sortDir === 'desc' ? 'text-primary' : ''">▼</span>
      </span>
    </span>
  </th>
</template>

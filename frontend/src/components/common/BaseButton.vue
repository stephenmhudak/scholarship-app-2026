<script setup>
defineProps({
  variant: {
    type: String,
    default: 'primary',
    validator: (v) => ['primary', 'secondary', 'ghost', 'danger'].includes(v),
  },
  size: {
    type: String,
    default: 'md',
    validator: (v) => ['sm', 'md', 'lg'].includes(v),
  },
  loading: {
    type: Boolean,
    default: false,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  type: {
    type: String,
    default: 'button',
  },
})
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    :class="[
      'inline-flex items-center justify-center font-semibold rounded-xl transition-all duration-150 focus:outline-none focus:ring-2 focus:ring-offset-2',
      // size
      size === 'sm' && 'px-3.5 py-1.5 text-xs',
      size === 'md' && 'px-5 py-2.5 text-sm',
      size === 'lg' && 'px-6 py-3 text-base',
      // variant
      variant === 'primary'   && 'bg-primary text-white hover:bg-primary-700 active:bg-primary-800 focus:ring-primary/40 shadow-sm disabled:bg-primary/40',
      variant === 'secondary' && 'bg-white text-navy border border-[#E9EDF7] hover:bg-[#F4F7FE] focus:ring-primary/20 disabled:text-navy/30',
      variant === 'ghost'     && 'bg-transparent text-primary hover:bg-primary/8 focus:ring-primary/20 disabled:text-primary/30',
      variant === 'danger'    && 'bg-danger text-white hover:bg-danger-dark active:bg-danger-dark focus:ring-danger/40 shadow-sm disabled:bg-danger/40',
      (disabled || loading) && 'cursor-not-allowed opacity-60',
    ]"
  >
    <span v-if="loading" class="mdi mdi-loading animate-spin mr-1.5"></span>
    <slot />
  </button>
</template>

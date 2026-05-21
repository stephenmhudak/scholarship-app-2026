<script setup>
defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  title: {
    type: String,
    default: '',
  },
})

const emit = defineEmits(['close'])
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition ease-out duration-200"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition ease-in duration-150"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="show"
        class="fixed inset-0 z-50 flex items-center justify-center"
      >
        <!-- Overlay -->
        <div
          class="absolute inset-0 bg-navy/40 backdrop-blur-sm"
          @click="emit('close')"
        ></div>

        <!-- Modal panel -->
        <div class="relative bg-white rounded-2xl shadow-card-lg w-full max-w-lg mx-4 z-10 border border-[#E9EDF7]">
          <div class="flex items-center justify-between px-6 py-4 border-b border-[#E9EDF7]">
            <h2 class="text-base font-bold text-navy">{{ title }}</h2>
            <button
              @click="emit('close')"
              class="w-8 h-8 flex items-center justify-center rounded-lg text-slate-400 hover:text-navy hover:bg-[#F4F7FE] transition-colors"
            >
              <span class="mdi mdi-close text-lg"></span>
            </button>
          </div>
          <div class="px-6 py-5">
            <slot />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

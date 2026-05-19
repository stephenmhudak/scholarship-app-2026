<script setup>
defineProps({
  status: {
    type: String,
    required: true,
  },
})

const styles = {
  draft: 'bg-gray-100 text-gray-700',
  submitted: 'bg-blue-100 text-blue-700',
  under_review: 'bg-yellow-100 text-yellow-700',
  awarded: 'bg-green-100 text-green-700',
  rejected: 'bg-red-100 text-red-700',
}

const labels = {
  draft: 'Draft',
  submitted: 'Submitted',
  under_review: 'Under Review',
  awarded: 'Awarded',
  rejected: 'Rejected',
}
</script>

<template>
  <span
    :class="[
      'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium capitalize',
      styles[status] || 'bg-gray-100 text-gray-600',
    ]"
  >
    {{ labels[status] || status }}
  </span>
</template>

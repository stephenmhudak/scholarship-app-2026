<script setup>
defineProps({
  status: {
    type: String,
    required: true,
  },
})

const styles = {
  draft:        'bg-navy-50 text-navy-600 border border-navy-100',
  submitted:    'bg-primary-50 text-primary-700 border border-primary-100',
  under_review: 'bg-warning-light text-[#8C6500] border border-warning/30',
  awarded:      'bg-success-light text-success-dark border border-success/30',
  rejected:     'bg-danger-light text-danger-dark border border-danger/30',
}

const labels = {
  draft:        'Draft',
  submitted:    'Submitted',
  under_review: 'Under Review',
  awarded:      'Awarded',
  rejected:     'Rejected',
}
</script>

<template>
  <span
    :class="[
      'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold',
      styles[status] || 'bg-navy-50 text-navy-600',
    ]"
  >
    {{ labels[status] || status }}
  </span>
</template>

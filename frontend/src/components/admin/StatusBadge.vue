<script setup>
defineProps({
  status: {
    type: String,
    required: true,
  },
})

const colors = {
  draft:        'secondary',
  submitted:    'primary',
  under_review: 'warning',
  awarded:      'success',
  rejected:     'error',
  archived:     '#A3AED0',
}

const labels = {
  draft:        'Draft',
  submitted:    'Submitted',
  under_review: 'Under Review',
  awarded:      'Awarded',
  rejected:     'Rejected',
  archived:     'Archived',
}
</script>

<template>
  <v-chip :color="colors[status] || 'secondary'" class="font-semibold">
    {{ labels[status] || status }}
  </v-chip>
</template>

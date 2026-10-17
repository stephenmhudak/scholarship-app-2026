<script setup>
const props = defineProps({
  status: {
    type: String,
    required: true,
  },
  // Applicants, counselors and school staff: "Not submitted" instead of the admin term "Archived"
  plain: {
    type: Boolean,
    default: false,
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

const plainLabels = {
  archived: 'Not submitted',
}
</script>

<template>
  <v-chip :color="colors[status] || 'secondary'" class="font-semibold">
    {{ (props.plain && plainLabels[status]) || labels[status] || status }}
  </v-chip>
</template>

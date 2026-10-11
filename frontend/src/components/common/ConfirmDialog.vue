<script setup>
import { useConfirmState } from '../../composables/useConfirm'

const { state, settle } = useConfirmState()
</script>

<template>
  <v-dialog :model-value="state.open" max-width="440" @update:model-value="(open) => !open && settle(false)">
    <v-card :title="state.title">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="settle(false)" />
      </template>
      <v-card-text>{{ state.text }}</v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="outlined" @click="settle(false)">Cancel</v-btn>
        <v-btn :color="state.color" variant="flat" @click="settle(true)">{{ state.confirmText }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

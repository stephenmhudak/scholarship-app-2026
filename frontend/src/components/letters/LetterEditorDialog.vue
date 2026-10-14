<script setup>
import { ref, computed, watch } from 'vue'
import api from '../../services/api'
import RichTextEditor from '../common/RichTextEditor.vue'
import { openPdf } from '../../utils/pdf'

// Writing an award letter: rich text with merge fields ({{awardee_name}}, {{answer:Question}} …) inserted from a
// menu, and a PDF preview. Used for a cycle's base letters and for a scholarship's own letter.
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, required: true },
  cycleId: { type: String, required: true },
  // { name, body, isDefault } to edit; null for a new letter
  letter: { type: Object, default: null },
  // Base letters: name and "default" are editable. A scholarship's letter: it can start from a base letter.
  isBase: { type: Boolean, default: false },
  baseLetters: { type: Array, default: () => [] },
  previewUrl: { type: String, required: true },
  save: { type: Function, required: true },
})
const emit = defineEmits(['update:modelValue'])

const open = computed({ get: () => props.modelValue, set: (v) => emit('update:modelValue', v) })
const form = ref({ name: '', body: '', isDefault: false })
const fields = ref({ fields: [], answers: [] })
const startFrom = ref(null)
const editor = ref(null)
const saving = ref(false)
const previewing = ref(false)
const error = ref(null)

watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen) return
  error.value = null
  startFrom.value = null
  form.value = { name: props.letter?.name ?? '', body: props.letter?.body ?? '', isDefault: !!props.letter?.isDefault }
  // A new scholarship letter starts as a copy of the default base letter
  if (!props.letter && !props.isBase && props.baseLetters.length) useBase(props.baseLetters[0].id)
  try {
    const res = await api.get(`/cycles/${props.cycleId}/letter-fields`)
    fields.value = res.data
  } catch {
    fields.value = { fields: [], answers: [] }
  }
}, { immediate: true })

function useBase(id) {
  const base = props.baseLetters.find((l) => l.id === id)
  if (!base) return
  startFrom.value = id
  form.value.body = base.body
}

function insert(token) {
  editor.value?.insertText(token)
}

async function preview() {
  previewing.value = true
  error.value = null
  try {
    await openPdf(props.previewUrl, { name: form.value.name || 'Preview', body: form.value.body })
  } catch (err) {
    error.value = err.message
  } finally {
    previewing.value = false
  }
}

async function submit() {
  saving.value = true
  error.value = null
  try {
    await props.save({ ...form.value })
    open.value = false
  } catch (err) {
    error.value = err.response?.data?.error || 'Failed to save the letter.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="900" scrollable>
    <v-card :title="title">
      <template #append>
        <v-btn icon="mdi-close" variant="text" @click="open = false" />
      </template>
      <v-card-text class="pa-6 space-y-4">
        <v-alert v-if="error" type="error" :text="error" />

        <div v-if="isBase" class="flex items-center gap-4">
          <v-text-field v-model="form.name" label="Letter name *" hide-details="auto" class="flex-1" />
          <v-checkbox v-model="form.isDefault" label="Default letter" hint="Used by scholarships without their own letter" persistent-hint class="flex-none" />
        </div>
        <v-select
          v-else-if="!letter && baseLetters.length"
          :model-value="startFrom"
          :items="baseLetters"
          item-title="name"
          item-value="id"
          label="Start from"
          hint="Copies that base letter's text; you can change it below."
          persistent-hint
          @update:model-value="useBase"
        />

        <p class="text-sm text-slate-400">
          Fields in <span class="font-mono text-navy">{{ '{' }}{{ '{' }}double braces}}</span> are filled in for each awardee.
          Use <strong>Insert field</strong> to add one at the cursor, such as their name, mailing address or award amount.
        </p>

        <RichTextEditor ref="editor" v-model="form.body" min-height="min-h-[24rem]">
          <template #toolbar-append>
            <v-menu max-height="420">
              <template #activator="{ props: menuProps }">
                <v-btn v-bind="menuProps" variant="tonal" size="small" prepend-icon="mdi-code-braces" class="mr-1">Insert field</v-btn>
              </template>
              <v-list density="compact">
                <v-list-subheader>Award</v-list-subheader>
                <v-list-item v-for="f in fields.fields" :key="f.token" :title="f.label" :subtitle="f.token" @click="insert(f.token)" />
                <template v-if="fields.answers.length">
                  <v-divider />
                  <v-list-subheader>Application answers (e.g. mailing address)</v-list-subheader>
                  <v-list-item v-for="a in fields.answers" :key="a.token" :title="a.label" @click="insert(a.token)" />
                </template>
              </v-list>
            </v-menu>
          </template>
        </RichTextEditor>

        <div class="flex gap-3">
          <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="submit">Save Letter</v-btn>
          <v-btn variant="outlined" prepend-icon="mdi-file-pdf-box" :loading="previewing" :disabled="!form.body" @click="preview">Preview PDF</v-btn>
          <v-btn variant="text" @click="open = false">Cancel</v-btn>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup>
import { onBeforeUnmount, ref, watch } from 'vue'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import { sanitizeHtml } from '../../utils/richText'

// The Tiptap rich text editor in Vuetify chrome. v-model is HTML ('' when empty). Used by rich text questions and
// award letters (which add an "Insert field" menu through the toolbar-append slot and insertText).
const props = defineProps({
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
  minHeight: { type: String, default: 'min-h-[8rem]' },
})
const emit = defineEmits(['update:modelValue'])

const editor = useEditor({
  content: sanitizeHtml(props.modelValue),
  extensions: [
    StarterKit.configure({
      heading: { levels: [2, 3] },
      code: false,
      codeBlock: false,
      link: { openOnClick: false, defaultProtocol: 'https' },
    }),
  ],
  editorProps: {
    attributes: { class: `rich-text ${props.minHeight} px-4 py-3 focus:outline-none` },
  },
  onUpdate({ editor }) {
    emit('update:modelValue', editor.isEmpty ? '' : editor.getHTML())
  },
})

// Keep the editor in sync when the draft loads after mount.
watch(() => props.modelValue, (value) => {
  if (!editor.value) return
  const current = editor.value.isEmpty ? '' : editor.value.getHTML()
  if ((value || '') !== current) editor.value.commands.setContent(sanitizeHtml(value), { emitUpdate: false })
})

onBeforeUnmount(() => editor.value?.destroy())

const tools = [
  { icon: 'mdi-format-bold', title: 'Bold', mark: 'bold', run: (c) => c.toggleBold() },
  { icon: 'mdi-format-italic', title: 'Italic', mark: 'italic', run: (c) => c.toggleItalic() },
  { icon: 'mdi-format-underline', title: 'Underline', mark: 'underline', run: (c) => c.toggleUnderline() },
  { icon: 'mdi-format-strikethrough', title: 'Strikethrough', mark: 'strike', run: (c) => c.toggleStrike() },
  { divider: true },
  { icon: 'mdi-format-header-2', title: 'Heading', mark: ['heading', { level: 2 }], run: (c) => c.toggleHeading({ level: 2 }) },
  { icon: 'mdi-format-header-3', title: 'Subheading', mark: ['heading', { level: 3 }], run: (c) => c.toggleHeading({ level: 3 }) },
  { icon: 'mdi-format-list-bulleted', title: 'Bulleted list', mark: 'bulletList', run: (c) => c.toggleBulletList() },
  { icon: 'mdi-format-list-numbered', title: 'Numbered list', mark: 'orderedList', run: (c) => c.toggleOrderedList() },
  { icon: 'mdi-format-quote-close', title: 'Quote', mark: 'blockquote', run: (c) => c.toggleBlockquote() },
  { divider: true },
  { icon: 'mdi-link-variant', title: 'Link', mark: 'link', run: null },
  { icon: 'mdi-undo', title: 'Undo', run: (c) => c.undo() },
  { icon: 'mdi-redo', title: 'Redo', run: (c) => c.redo() },
]

function isActive(tool) {
  if (!editor.value || !tool.mark) return false
  return Array.isArray(tool.mark) ? editor.value.isActive(...tool.mark) : editor.value.isActive(tool.mark)
}

function apply(tool) {
  if (!editor.value) return
  if (tool.run) {
    tool.run(editor.value.chain().focus()).run()
    return
  }
  // Link: ask for a URL in a dialog; the editor keeps its selection while it's open.
  linkUrl.value = editor.value.getAttributes('link').href || ''
  linkIsExisting.value = linkUrl.value !== ''
  linkDialog.value = true
}

const linkDialog = ref(false)
const linkUrl = ref('')
const linkIsExisting = ref(false)

// An empty URL removes the link.
function saveLink(url = linkUrl.value) {
  linkDialog.value = false
  if (!editor.value) return
  const chain = editor.value.chain().focus().extendMarkRange('link')
  if (url.trim() === '') chain.unsetLink().run()
  else chain.setLink({ href: url.trim() }).run()
}

/** Inserts plain text at the cursor (e.g. a merge field like {{awardee_name}}). */
function insertText(text) {
  editor.value?.chain().focus().insertContent(text).run()
}

defineExpose({ insertText })
</script>

<template>
  <div>
    <v-sheet
      border
      rounded="lg"
      :class="['overflow-hidden', error ? 'border-danger' : 'focus-within:border-primary']"
    >
      <v-toolbar density="compact" color="surface-light" class="border-b px-1" height="44">
        <template v-for="(tool, i) in tools" :key="i">
          <v-divider v-if="tool.divider" vertical inset class="mx-1" />
          <v-btn
            v-else
            icon
            size="small"
            variant="text"
            rounded="lg"
            :color="isActive(tool) ? 'primary' : undefined"
            :active="isActive(tool)"
            :title="tool.title"
            :aria-label="tool.title"
            :aria-pressed="isActive(tool)"
            @mousedown.prevent
            @click="apply(tool)"
          >
            <v-icon :icon="tool.icon" size="20" />
          </v-btn>
        </template>
        <v-spacer />
        <slot name="toolbar-append" />
      </v-toolbar>
      <EditorContent :editor="editor" class="text-sm text-navy" />
    </v-sheet>
    <v-dialog v-model="linkDialog" max-width="440">
      <v-card :title="linkIsExisting ? 'Edit link' : 'Add link'">
        <template #append>
          <v-btn icon="mdi-close" variant="text" size="small" @click="linkDialog = false" />
        </template>
        <v-card-text>
          <v-form @submit.prevent="saveLink()">
            <v-text-field v-model="linkUrl" label="URL" placeholder="https://example.com" autofocus hide-details />
          </v-form>
        </v-card-text>
        <v-card-actions>
          <v-btn v-if="linkIsExisting" color="error" variant="text" @click="saveLink('')">Remove link</v-btn>
          <v-spacer />
          <v-btn variant="outlined" @click="linkDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" @click="saveLink()">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

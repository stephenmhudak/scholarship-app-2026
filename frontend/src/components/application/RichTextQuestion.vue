<script setup>
import { computed, onBeforeUnmount, watch } from 'vue'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import { htmlToText, sanitizeHtml } from '../../utils/richText'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const maxLength = computed(() => props.question.validationRules?.maxLength ?? null)
const charCount = computed(() => htmlToText(props.modelValue).length)

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
    attributes: { class: 'rich-text min-h-[8rem] px-4 py-3 focus:outline-none' },
  },
  onUpdate({ editor }) {
    const html = editor.isEmpty ? '' : editor.getHTML()
    emit('answer', html)
    emit('update:modelValue', html)
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
  // Link: prompt for a URL; empty input removes the link.
  const previous = editor.value.getAttributes('link').href || ''
  const url = window.prompt('Link URL', previous)
  if (url === null) return
  const chain = editor.value.chain().focus().extendMarkRange('link')
  if (url.trim() === '') chain.unsetLink().run()
  else chain.setLink({ href: url.trim() }).run()
}
</script>

<template>
  <div>
    <label class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
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
      </v-toolbar>
      <EditorContent :editor="editor" class="text-sm text-navy" />
    </v-sheet>
    <div class="flex justify-between mt-1">
      <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
      <p v-if="maxLength" class="text-xs text-slate-400 ml-auto">{{ charCount }} / {{ maxLength }}</p>
    </div>
  </div>
</template>

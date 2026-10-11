import { reactive } from 'vue'

// Shared state for the single ConfirmDialog mounted in App.vue.
const state = reactive({
  open: false,
  title: '',
  text: '',
  confirmText: 'Confirm',
  color: 'primary',
  resolve: null,
})

function settle(result) {
  state.resolve?.(result)
  state.resolve = null
  state.open = false
}

// await confirm({ title, text, confirmText?, color? }) → true if confirmed, false if cancelled/dismissed.
export function useConfirm() {
  function confirm({ title, text, confirmText = 'Confirm', color = 'primary' }) {
    settle(false) // a new request cancels any one still open
    Object.assign(state, { open: true, title, text, confirmText, color })
    return new Promise((resolve) => { state.resolve = resolve })
  }
  return confirm
}

export function useConfirmState() {
  return { state, settle }
}

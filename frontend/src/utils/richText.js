import DOMPurify from 'dompurify'

/** Long answer questions with the rich text option use the Tiptap editor and store HTML. */
export function isRichText(question) {
  return question?.type === 'long_answer' && !!question.validationRules?.richText
}

// Tags the rich text editor can produce. Anything else is stripped before display.
const ALLOWED_TAGS = [
  'p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'h2', 'h3',
  'ul', 'ol', 'li', 'blockquote', 'a', 'hr',
]

// Answers saved before a question became rich text are plain text; keep their line breaks.
function toHtml(value) {
  if (!value || /<[a-z][\s\S]*>/i.test(value)) return value || ''
  const escaped = value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  return escaped.split(/\r?\n/).map((line) => `<p>${line}</p>`).join('')
}

export function sanitizeHtml(html) {
  return DOMPurify.sanitize(toHtml(html), { ALLOWED_TAGS, ALLOWED_ATTR: ['href', 'target', 'rel'] })
}

/** Visible text of an HTML answer, used for required and length validation. */
export function htmlToText(html) {
  if (!html) return ''
  const doc = new DOMParser().parseFromString(sanitizeHtml(html), 'text/html')
  return (doc.body.textContent || '').replace(/ /g, ' ').trim()
}

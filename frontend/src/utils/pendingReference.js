// A reference code opened before signing in, kept so the referee lands on it after signing in
// (or after creating an account and verifying their email in this browser).
const KEY = 'pending_reference_code'
export const REFERENCE_CODE_RE = /^[A-Z0-9]{10}$/

export function normalizeReferenceCode(code) {
  return String(code ?? '').trim().toUpperCase()
}

export function savePendingReferenceCode(code) {
  const normalized = normalizeReferenceCode(code)
  if (!REFERENCE_CODE_RE.test(normalized)) return
  try { localStorage.setItem(KEY, normalized) } catch { /* storage unavailable: they can type the code in */ }
}

export function takePendingReferenceCode() {
  try {
    const code = localStorage.getItem(KEY)
    localStorage.removeItem(KEY)
    return code && REFERENCE_CODE_RE.test(code) ? code : null
  } catch {
    return null
  }
}

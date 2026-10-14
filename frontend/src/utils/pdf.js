import api from '../services/api'

// PDFs come from authenticated API calls, so they're fetched as blobs rather than linked to directly

/** Downloads a PDF from a GET endpoint, using the server's file name when it sends one. */
export async function downloadPdf(url, fallbackName = 'letter.pdf') {
  const res = await api.get(url, { responseType: 'blob' })
  const disposition = res.headers['content-disposition'] ?? ''
  const name = decodeURIComponent(disposition.match(/filename\*=UTF-8''([^;]+)/)?.[1] ?? '')
    || disposition.match(/filename="?([^";]+)"?/)?.[1] || fallbackName
  const href = URL.createObjectURL(res.data)
  const link = document.createElement('a')
  link.href = href
  link.download = name
  link.click()
  URL.revokeObjectURL(href)
}

/** Opens a PDF from a POST endpoint (letter previews) in a new tab. */
export async function openPdf(url, body) {
  // Opened before the request so popup blockers treat it as part of the click
  const tab = window.open('', '_blank')
  try {
    const res = await api.post(url, body, { responseType: 'blob' })
    const href = URL.createObjectURL(res.data)
    if (tab) tab.location.href = href
    else window.location.assign(href)
  } catch (err) {
    tab?.close()
    // Errors come back as a blob too; surface the API's message
    const text = err.response?.data instanceof Blob ? await err.response.data.text() : null
    throw new Error(text ? JSON.parse(text).error ?? 'Preview failed.' : 'Preview failed.')
  }
}

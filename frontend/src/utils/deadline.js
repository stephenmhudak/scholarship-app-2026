// When an application period stops accepting applications, in the viewer's own time zone.
// The server keeps a period open through the end of its close date in UTC (CycleWindow.ClosesAt), so the real
// deadline is midnight UTC after the close date, e.g. 7:00 PM CDT on the close date itself.

/** The closing moment as a Date, from a close date like '2026-10-31T00:00:00' or '2026-10-31'. */
export function closesAt(closeDate) {
  const [y, m, d] = String(closeDate).slice(0, 10).split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d + 1))
}

/** 'October 31, 2026 at 7:00 PM CDT' */
export function formatDeadline(closeDate) {
  if (!closeDate) return ''
  const at = closesAt(closeDate)
  const date = at.toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })
  const time = at.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit', timeZoneName: 'short' })
  return `${date} at ${time}`
}

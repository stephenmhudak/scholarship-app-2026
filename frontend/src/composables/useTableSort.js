import { ref } from 'vue'

export function useTableSort(defaultKey = null, defaultDir = 'asc') {
  const sortKey = ref(defaultKey)
  const sortDir = ref(defaultDir)

  function setSort(key) {
    if (sortKey.value === key) {
      sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
    } else {
      sortKey.value = key
      sortDir.value = 'asc'
    }
  }

  function applySort(arr, getters) {
    if (!sortKey.value || !getters[sortKey.value]) return arr
    const getter = getters[sortKey.value]
    return [...arr].sort((a, b) => {
      const av = getter(a)
      const bv = getter(b)
      if (av == null && bv == null) return 0
      if (av == null) return 1
      if (bv == null) return -1
      if (av < bv) return sortDir.value === 'asc' ? -1 : 1
      if (av > bv) return sortDir.value === 'asc' ? 1 : -1
      return 0
    })
  }

  return { sortKey, sortDir, setSort, applySort }
}

// Vite plugin: `import hashes from 'virtual:component-hashes'` gives, for every .vue file under src/ (keyed by its
// path from src/, e.g. 'pages/account/AccountPage.vue'), a hash of its source together with every local .vue component
// it imports, directly or indirectly. Page tours (src/tours) use it as part of their version, so a tour shows again
// when anything on its page changes.
//
// The source is normalized first: <style> blocks, comments and whitespace don't count, so cosmetic edits don't
// re-trigger tours. Anything else (markup, classes, props, logic) does.
import { createHash } from 'node:crypto'
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, relative, resolve, sep } from 'node:path'

const VIRTUAL_ID = 'virtual:component-hashes'
const RESOLVED_ID = '\0' + VIRTUAL_ID
// import X from './X.vue', import('./X.vue')
const VUE_IMPORT = /(?:\bfrom\s*|\bimport\s*\(\s*)['"](\.{1,2}\/[^'"]+\.vue)['"]/g

export function normalizeSource(source) {
  return source
    .replace(/<style[\s\S]*?<\/style>/g, '')
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    // Line comments, but not the // in a URL ("https://")
    .replace(/(^|[^:'"`\\])\/\/[^\n]*/g, '$1')
    .replace(/\s+/g, ' ')
    .trim()
}

const sha = (text) => createHash('sha256').update(text).digest('hex')

function vueFiles(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return vueFiles(path)
    return entry.name.endsWith('.vue') ? [path] : []
  })
}

export function computeComponentHashes(srcDir) {
  const own = new Map() // file -> { hash, imports }
  for (const file of vueFiles(srcDir)) {
    const source = readFileSync(file, 'utf8')
    const imports = [...source.matchAll(VUE_IMPORT)].map((m) => resolve(dirname(file), m[1]))
    own.set(file, { hash: sha(normalizeSource(source)), imports })
  }

  const result = {}
  for (const file of own.keys()) {
    // Every component reachable from this one
    const seen = new Set()
    const stack = [file]
    while (stack.length) {
      const next = stack.pop()
      if (seen.has(next) || !own.has(next)) continue
      seen.add(next)
      stack.push(...own.get(next).imports)
    }
    const key = (f) => relative(srcDir, f).split(sep).join('/')
    const combined = [...seen].sort().map((f) => `${key(f)}:${own.get(f).hash}`).join('\n')
    result[key(file)] = sha(combined).slice(0, 16)
  }
  return result
}

export default function componentHashes() {
  let srcDir
  return {
    name: 'component-hashes',
    configResolved(config) {
      srcDir = resolve(config.root, 'src')
    },
    resolveId(id) {
      return id === VIRTUAL_ID ? RESOLVED_ID : null
    },
    load(id) {
      if (id !== RESOLVED_ID) return null
      return `export default ${JSON.stringify(computeComponentHashes(srcDir))}`
    },
    // Dev server: recompute when a component changes
    handleHotUpdate({ file, server }) {
      if (!file.endsWith('.vue')) return
      const mod = server.moduleGraph.getModuleById(RESOLVED_ID)
      if (mod) server.moduleGraph.invalidateModule(mod)
    },
  }
}

export function formatarDataHora(iso?: string | null): string {
  if (!iso) return '—'
  const data = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  return data.toLocaleString()
}

export function formatarDuracaoDesde(desdeIso?: string | null): string {
  if (!desdeIso) return '—'
  const inicio = new Date(desdeIso.endsWith('Z') ? desdeIso : `${desdeIso}Z`).getTime()
  return formatarSegundos(Math.max(0, Math.floor((Date.now() - inicio) / 1000)))
}

export function formatarSegundos(segundosTotal?: number | null): string {
  if (segundosTotal === undefined || segundosTotal === null) return '—'
  const h = Math.floor(segundosTotal / 3600)
  const m = Math.floor((segundosTotal % 3600) / 60)
  const s = Math.floor(segundosTotal % 60)
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
}

import { useCallback, useEffect, useState } from 'react'
import { deleteReading, getCatalogue, getReadingPage } from '../api'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { QuickAdd } from '../components/QuickAdd'
import { formatValue } from '../reading-utils'
import type { Reading, ReadingPage, Template } from '../types'

interface HistoryPageProps { trackedTemplates: Template[]; onChanged: () => void; announce: (message: string, error?: boolean) => void }

function dateBoundary(value: string, end: boolean) {
  if (!value) return undefined
  return new Date(`${value}T${end ? '23:59:59.999' : '00:00:00'}`).toISOString()
}

export function HistoryPage({ trackedTemplates, onChanged, announce }: HistoryPageProps) {
  const [catalogue, setCatalogue] = useState<Template[]>([])
  const [result, setResult] = useState<ReadingPage | null>(null)
  const [templateId, setTemplateId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Reading | null>(null)
  const [deleting, setDeleting] = useState<Reading | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    const controller = new AbortController()
    setLoading(true)
    Promise.all([
      getCatalogue(controller.signal),
      getReadingPage({ templateId: templateId || undefined, fromUtc: dateBoundary(from, false), toUtc: dateBoundary(to, true), page, pageSize: 15 }, controller.signal),
    ]).then(([templates, readings]) => { setCatalogue(templates); setResult(readings) })
      .catch((error: unknown) => { if (!(error instanceof DOMException && error.name === 'AbortError')) announce(error instanceof Error ? error.message : 'History could not be loaded.', true) })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [announce, from, page, templateId, to])

  useEffect(() => load(), [load])

  async function remove() {
    if (!deleting) return
    try { await deleteReading(deleting.id); setDeleting(null); announce('Reading removed.'); onChanged(); load() }
    catch (error) { announce(error instanceof Error ? error.message : 'Reading could not be removed.', true) }
  }

  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / 15))
  return (
    <main className="page">
      <div className="page-heading"><div><p className="eyebrow">Recorded data</p><h1>History</h1></div></div>
      <section className="filter-line" aria-label="Reading filters">
        <label><span>Measurement</span><select value={templateId} onChange={(event) => { setTemplateId(event.target.value); setPage(1) }}><option value="">All</option>{catalogue.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}</select></label>
        <label><span>From</span><input type="date" value={from} onChange={(event) => { setFrom(event.target.value); setPage(1) }} /></label>
        <label><span>To</span><input type="date" value={to} onChange={(event) => { setTo(event.target.value); setPage(1) }} /></label>
        <button type="button" className="text-action" onClick={() => { setTemplateId(''); setFrom(''); setTo(''); setPage(1) }}>Clear</button>
      </section>

      {loading && !result ? <div className="loading-state">Loading history…</div> : result?.items.length === 0 ? <p className="quiet-empty">No readings match these filters.</p> : (
        <div className="data-list" role="table" aria-label="Readings">
          <div className="data-row data-header" role="row"><span>Measurement</span><span>Value</span><span>Recorded</span><span>Note</span><span>Actions</span></div>
          {result?.items.map((reading) => <div className="data-row" role="row" key={reading.id}>
            <strong>{reading.templateName}</strong><span>{formatValue(reading.value)} {reading.unit}</span><time>{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(reading.recordedAtUtc))}</time><span className="muted">{reading.note || '—'}</span><span className="row-actions"><button type="button" className="text-action" aria-label={`Edit ${reading.templateName} reading`} onClick={() => setEditing(reading)}>Edit</button><button type="button" className="text-action danger-text" aria-label={`Delete ${reading.templateName} reading`} onClick={() => setDeleting(reading)}>Remove</button></span>
          </div>)}
        </div>
      )}
      {result && <div className="pagination"><span>{result.totalCount} {result.totalCount === 1 ? 'reading' : 'readings'}</span><div><button type="button" className="text-action" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>{page} / {totalPages}</span><button type="button" className="text-action" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>Next</button></div></div>}

      <QuickAdd isOpen={editing !== null} templates={catalogue} selectedTemplateId={editing?.templateId ?? trackedTemplates[0]?.id ?? ''} reading={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); announce('Reading saved.'); onChanged(); load() }} />
      <ConfirmDialog open={deleting !== null} title="Remove reading?" confirmLabel="Remove" onCancel={() => setDeleting(null)} onConfirm={remove}><p>This reading will remain recoverable for 60 days.</p></ConfirmDialog>
    </main>
  )
}

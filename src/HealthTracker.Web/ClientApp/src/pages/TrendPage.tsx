import { useCallback, useEffect, useMemo, useState } from 'react'
import { getReadings } from '../api'
import { TrendChart } from '../components/TrendChart'
import { formatValue, getRangeStartUtc, sortReadingsAscending } from '../reading-utils'
import type { Reading, Template } from '../types'

const rangeOptions = [7, 30, 90, 365]
const selectedMetricKey = 'healthpulse.selected-metric'

interface TrendPageProps {
  templates: Template[]
  refreshVersion: number
  onNavigate: (path: string) => void
}

export function TrendPage({ templates, refreshVersion, onNavigate }: TrendPageProps) {
  const [selectedTemplateId, setSelectedTemplateId] = useState(() => window.localStorage.getItem(selectedMetricKey) ?? '')
  const [readings, setReadings] = useState<Reading[]>([])
  const [rangeDays, setRangeDays] = useState(30)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const selectedTemplate = useMemo(
    () => templates.find((template) => template.id === selectedTemplateId) ?? templates[0],
    [selectedTemplateId, templates],
  )
  const latestReading = readings.at(-1)

  useEffect(() => {
    if (selectedTemplate && selectedTemplate.id !== selectedTemplateId) setSelectedTemplateId(selectedTemplate.id)
  }, [selectedTemplate, selectedTemplateId])

  const load = useCallback(() => {
    if (!selectedTemplate) { setReadings([]); return () => undefined }
    const controller = new AbortController()
    const now = new Date()
    setLoading(true)
    getReadings(selectedTemplate.id, getRangeStartUtc(now, rangeDays), now.toISOString(), controller.signal)
      .then((page) => { setReadings(sortReadingsAscending(page.items)); setError(null) })
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError.message : 'Readings could not be loaded.')
      })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [rangeDays, selectedTemplate])

  useEffect(() => load(), [load, refreshVersion])

  function selectTemplate(id: string) {
    setSelectedTemplateId(id)
    window.localStorage.setItem(selectedMetricKey, id)
  }

  if (templates.length === 0) {
    return <main className="page"><section className="empty-state"><h1>No tracked measurements</h1><button className="text-action" onClick={() => onNavigate('/templates')}>Choose a measurement</button></section></main>
  }

  return (
    <main className="trend-page">
      <div className="trend-toolbar">
        <div className="metric-switcher" aria-label="Tracked measurements">
          {templates.map((template) => <button type="button" key={template.id} aria-pressed={template.id === selectedTemplate?.id} onClick={() => selectTemplate(template.id)}>{template.name}</button>)}
        </div>
        <div className="range-switcher" aria-label="Trend range">
          {rangeOptions.map((days) => <button type="button" key={days} aria-pressed={days === rangeDays} onClick={() => setRangeDays(days)}>{days === 365 ? '1Y' : `${days}D`}</button>)}
        </div>
      </div>

      <section className="trend-heading" aria-labelledby="current-metric">
        <div>
          <p id="current-metric">{selectedTemplate?.name}</p>
          {latestReading ? <div className="latest-reading"><strong>{formatValue(latestReading.value)}</strong><span>{latestReading.unit}</span></div> : <div className="latest-reading"><strong>—</strong></div>}
        </div>
        <p className="latest-time">{latestReading ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(latestReading.recordedAtUtc)) : 'No readings yet'}</p>
      </section>

      <div className="chart-heading"><span>{rangeDays === 365 ? 'One year' : `${rangeDays} days`}</span><span>{readings.length} {readings.length === 1 ? 'reading' : 'readings'}</span></div>
      {loading ? <div className="loading-state chart-loading" aria-live="polite">Loading trend…</div> : <TrendChart readings={readings} unit={selectedTemplate?.normalizedUnit ?? ''} metricName={selectedTemplate?.name ?? ''} />}
      {error && <p className="page-error" role="alert">{error}</p>}
    </main>
  )
}

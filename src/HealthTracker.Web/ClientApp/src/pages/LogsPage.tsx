import { useEffect, useState } from 'react'
import { getLogSnapshot } from '../api'
import { DiagnosticsTabs } from '../components/DiagnosticsTabs'
import type { LogSnapshot } from '../types'

interface LogsPageProps {
  announce: (message: string, error?: boolean) => void
  onNavigate: (path: string) => void
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

export function LogsPage({ announce, onNavigate }: LogsPageProps) {
  const [snapshot, setSnapshot] = useState<LogSnapshot | null>(null)
  const [refresh, setRefresh] = useState(0)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    setLoadError(null)
    getLogSnapshot(controller.signal).then((loadedSnapshot) => {
      setSnapshot(loadedSnapshot)
      setLoadError(null)
    }).catch((error: unknown) => {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        const message = error instanceof Error ? error.message : 'Application logs could not be loaded.'
        setLoadError(message)
        announce(message, true)
      }
    })
    return () => controller.abort()
  }, [announce, refresh])

  return <main className="page wide-page logs-page raw-logs-page">
    <div className="page-heading">
      <div><p className="eyebrow">Administration</p><h1>Logs</h1></div>
      <div className="heading-actions">
        <a className="line-button" href="/admin/logs/raw" target="_blank" rel="noreferrer">Open text view</a>
        <a className="line-button" href="/admin/logs/download">Download .txt</a>
        <button className="text-action" type="button" onClick={() => setRefresh((value) => value + 1)}>Refresh</button>
      </div>
    </div>
    <DiagnosticsTabs active="logs" onNavigate={onNavigate} />
    {loadError && <div className="page-error" role="alert">
      {snapshot ? `Could not refresh application logs. Showing the last loaded data. ${loadError}` : loadError}{' '}
      <button className="text-action" type="button" onClick={() => setRefresh((value) => value + 1)}>Try again</button>
    </div>}

    <section className="diagnostic-section" aria-labelledby="raw-log-title">
      <div className="section-heading">
        <h2 id="raw-log-title">Application log output</h2>
        {snapshot && <span>{snapshot.fileCount} files · updated {formatDateTime(snapshot.generatedAtUtc)}</span>}
      </div>
      <p className="quiet-empty raw-log-description">Raw diagnostic output is useful when investigating a specific issue. Use Traffic for request patterns and Security for sign-in activity.</p>
      {snapshot ? <pre className="log-viewer">{snapshot.content}</pre> : loadError ? null : <div className="loading-state">Loading application logs…</div>}
    </section>
  </main>
}

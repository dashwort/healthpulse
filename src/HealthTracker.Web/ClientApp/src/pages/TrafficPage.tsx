import { useEffect, useState } from 'react'
import { getTrafficSummary } from '../api'
import { DiagnosticsTabs } from '../components/DiagnosticsTabs'
import type { TrafficRange, TrafficSummary, TrafficTimelineBucket } from '../types'

interface TrafficPageProps {
  announce: (message: string, error?: boolean) => void
  onNavigate: (path: string) => void
}

const numberFormat = new Intl.NumberFormat()
const latencyLabels = ['< 50 ms', '50–99 ms', '100–249 ms', '250–499 ms', '500–999 ms', '1–2 s', '2–5 s', '5 s+']

function latencyLabel(band: number) {
  return band < 0 ? '—' : latencyLabels[band] ?? '—'
}

function formatBucket(value: string, range: TrafficRange) {
  return new Intl.DateTimeFormat(
    undefined,
    range === '24h' ? { hour: 'numeric' } : { month: 'short', day: 'numeric' },
  ).format(new Date(value))
}

function seriesPoints(
  timeline: TrafficTimelineBucket[],
  key: keyof Pick<TrafficTimelineBucket, 'successfulResponses' | 'clientErrorResponses' | 'rateLimitedResponses' | 'serverErrorResponses'>,
  maxCount: number,
) {
  const left = 42
  const right = 690
  const top = 13
  const bottom = 132
  const lastIndex = Math.max(1, timeline.length - 1)
  return timeline.map((bucket, index) => {
    const x = left + ((right - left) * index) / lastIndex
    const y = bottom - ((bottom - top) * bucket[key]) / maxCount
    return `${x},${y}`
  }).join(' ')
}

function rangeLabel(range: TrafficRange) {
  return range === '24h' ? 'last 24 hours' : range === '7d' ? 'last 7 days' : 'last 30 days'
}

export function TrafficPage({ announce, onNavigate }: TrafficPageProps) {
  const [range, setRange] = useState<TrafficRange>('24h')
  const [summary, setSummary] = useState<TrafficSummary | null>(null)
  const [refresh, setRefresh] = useState(0)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    setLoadError(null)
    getTrafficSummary(range, controller.signal).then((loadedSummary) => {
      setSummary(loadedSummary)
      setLoadError(null)
    }).catch((error: unknown) => {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        const message = error instanceof Error ? error.message : 'Traffic metrics could not be loaded.'
        setLoadError(message)
        announce(message, true)
      }
    })
    return () => controller.abort()
  }, [announce, range, refresh])

  const maxCount = summary
    ? Math.max(1, ...summary.timeline.flatMap((bucket) => [
      bucket.successfulResponses,
      bucket.clientErrorResponses,
      bucket.rateLimitedResponses,
      bucket.serverErrorResponses,
    ]))
    : 1
  const tickIndices = summary?.timeline.length
    ? [...new Set([
      0,
      Math.floor((summary.timeline.length - 1) / 4),
      Math.floor((summary.timeline.length - 1) / 2),
      Math.floor(((summary.timeline.length - 1) * 3) / 4),
      summary.timeline.length - 1,
    ])]
    : []

  return <main className="page wide-page logs-page traffic-page">
    <div className="page-heading">
      <div><p className="eyebrow">Administration</p><h1>Traffic</h1></div>
      <button className="text-action" type="button" onClick={() => setRefresh((value) => value + 1)}>Refresh</button>
    </div>
    <DiagnosticsTabs active="traffic" onNavigate={onNavigate} />
    {loadError && <div className="page-error" role="alert">
      {summary ? `Could not refresh traffic metrics. Showing the last loaded data. ${loadError}` : loadError}{' '}
      <button className="text-action" type="button" onClick={() => setRefresh((value) => value + 1)}>Try again</button>
    </div>}

    <section className="diagnostic-section traffic-overview" aria-labelledby="traffic-overview-title">
      <div className="access-overview-heading">
        <div><p className="eyebrow">Requests and performance</p><h2 id="traffic-overview-title">Application traffic</h2></div>
        <div className="access-overview-controls">
          <div className="range-switcher" aria-label="Traffic time range">
            {(['24h', '7d', '30d'] as const).map((value) => <button
              key={value}
              type="button"
              aria-pressed={range === value}
              onClick={() => { setSummary(null); setRange(value) }}
            >{value === '24h' ? '24 hours' : value === '7d' ? '7 days' : '30 days'}</button>)}
          </div>
        </div>
      </div>

      {!summary && !loadError ? <div className="loading-state">Loading traffic metrics…</div> : !summary ? null : <>
        <div className="traffic-summary" aria-label="HTTP request metrics">
          <div className="access-summary-item">
            <span>Requests</span>
            <strong>{numberFormat.format(summary.totalRequests)}</strong>
            <small>{summary.requestChangePercent === null
              ? 'No previous-period baseline yet'
              : `${summary.requestChangePercent > 0 ? '+' : ''}${numberFormat.format(summary.requestChangePercent)}% vs previous period`}</small>
          </div>
          <div className="access-summary-item">
            <span>Successful responses</span>
            <strong>{numberFormat.format(summary.successfulResponses)}</strong>
            <small>HTTP 2xx and 3xx</small>
          </div>
          <div className="access-summary-item">
            <span>Client errors</span>
            <strong>{numberFormat.format(summary.clientErrorResponses)}</strong>
            <small>HTTP 4xx, excluding rate limits</small>
          </div>
          <div className="access-summary-item">
            <span>Rate limited</span>
            <strong>{numberFormat.format(summary.rateLimitedResponses)}</strong>
            <small>HTTP 429 responses</small>
          </div>
          <div className="access-summary-item">
            <span>Server errors</span>
            <strong>{numberFormat.format(summary.serverErrorResponses)}</strong>
            <small>HTTP 5xx</small>
          </div>
        </div>

        {summary.totalRequests === 0
          ? <p className="quiet-empty traffic-empty">No request metrics have been recorded in this period. Aggregates are written about every 15 seconds.</p>
          : <div className="traffic-insights">
            <section className="access-timeline" aria-labelledby="traffic-timeline-title">
              <div className="access-section-heading">
                <h3 id="traffic-timeline-title">Request volume</h3>
                <span>Responses by {range === '24h' ? 'hour' : 'day'}</span>
              </div>
              <svg className="access-chart traffic-chart" viewBox="0 0 700 172" role="img" aria-label={`HTTP requests by ${range === '24h' ? 'hour' : 'day'} over the ${rangeLabel(range)}`}>
                <line className="access-chart-grid" x1="42" x2="690" y1="13" y2="13" />
                <line className="access-chart-grid" x1="42" x2="690" y1="72" y2="72" />
                <line className="access-chart-grid" x1="42" x2="690" y1="132" y2="132" />
                <text className="access-chart-label" x="4" y="17">{numberFormat.format(maxCount)}</text>
                <text className="access-chart-label" x="4" y="76">{numberFormat.format(Math.floor(maxCount / 2))}</text>
                <text className="access-chart-label" x="18" y="136">0</text>
                <polyline className="traffic-chart-success" fill="none" points={seriesPoints(summary.timeline, 'successfulResponses', maxCount)} />
                <polyline className="traffic-chart-client-error" fill="none" points={seriesPoints(summary.timeline, 'clientErrorResponses', maxCount)} />
                <polyline className="traffic-chart-rate-limit" fill="none" points={seriesPoints(summary.timeline, 'rateLimitedResponses', maxCount)} />
                <polyline className="traffic-chart-server-error" fill="none" points={seriesPoints(summary.timeline, 'serverErrorResponses', maxCount)} />
                {tickIndices.map((index) => {
                  const bucket = summary.timeline[index]
                  const x = 42 + (648 * index) / Math.max(1, summary.timeline.length - 1)
                  return <text className="access-chart-label" key={bucket.startUtc} x={x} y="159" textAnchor="middle">{formatBucket(bucket.startUtc, range)}</text>
                })}
              </svg>
              <div className="traffic-chart-legend">
                <span><i className="traffic-success-key" />2xx / 3xx</span>
                <span><i className="traffic-client-key" />4xx</span>
                <span><i className="traffic-rate-key" />429</span>
                <span><i className="traffic-server-key" />5xx</span>
              </div>
              <div className="traffic-latency">
                <div><span>Request latency p50</span><strong>{latencyLabel(summary.p50LatencyBand)}</strong></div>
                <div><span>Request latency p95</span><strong>{latencyLabel(summary.p95LatencyBand)}</strong></div>
              </div>
            </section>

            <section className="traffic-side-insights" aria-label="Route and response summaries">
              <div>
                <div className="access-section-heading"><h3>Most requested routes</h3><span>Top 10</span></div>
                {summary.topRoutes.length === 0 ? <p className="quiet-empty">No route data yet.</p> : <div className="traffic-route-list">
                  {summary.topRoutes.map((route) => <article className="traffic-route" key={`${route.method}:${route.routeTemplate}`}>
                    <div><span className="traffic-method">{route.method}</span><code>{route.routeTemplate}</code></div>
                    <small>{numberFormat.format(route.requestCount)} requests · {numberFormat.format(route.errorCount)} errors · p95 {latencyLabel(route.p95LatencyBand)}</small>
                  </article>)}
                </div>}
              </div>
              <div>
                <div className="access-section-heading"><h3>Response codes</h3><span>By count</span></div>
                {summary.topStatuses.length === 0 ? <p className="quiet-empty">No response codes yet.</p> : <ul className="traffic-status-list">
                  {summary.topStatuses.map((status) => <li key={status.statusCode}>
                    <span className={`traffic-status-code ${status.statusCode === 429 ? 'status-rate-limit' : `status-${Math.floor(status.statusCode / 100)}xx`}`}>{status.statusCode}</span>
                    <span>{numberFormat.format(status.requestCount)}</span>
                  </li>)}
                </ul>}
              </div>
            </section>
          </div>}
        <p className="access-footnote">Request counts are not visitor counts and may include application assets and automated probes. For authenticated account sign-ins, see Security. Route templates are aggregated without user, IP, query-string, or request-body data. Latency percentiles are approximate ranges.</p>
      </>}
    </section>
  </main>
}

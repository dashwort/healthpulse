import { useCallback, useEffect, useState } from 'react'
import {
  getAccessActivity, getAccessActivitySummary, getLogSnapshot, getUsers,
} from '../api'
import type {
  AccessActivityPage, AccessActivityRange, AccessActivitySummary, AllowedUser, LogSnapshot,
} from '../types'

interface LogsPageProps { announce: (message: string, error?: boolean) => void }

const numberFormat = new Intl.NumberFormat()

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

function formatBucket(value: string, range: AccessActivityRange) {
  return new Intl.DateTimeFormat(
    undefined,
    range === '24h' ? { hour: 'numeric' } : { weekday: 'short' },
  ).format(new Date(value))
}

function eventName(value: string) {
  return value === 'WebSignIn' ? 'Web sign-in' : value === 'AndroidAuthorization' ? 'Android authorization' : value
}

function failureName(value: string | null) {
  if (!value) return null
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function timelinePoints(summary: AccessActivitySummary, kind: 'successCount' | 'failureCount', maxCount: number) {
  const left = 42
  const right = 690
  const top = 13
  const bottom = 132
  const lastIndex = Math.max(1, summary.timeline.length - 1)
  return summary.timeline.map((bucket, index) => {
    const x = left + ((right - left) * index) / lastIndex
    const y = bottom - ((bottom - top) * bucket[kind]) / maxCount
    return x + ',' + y
  }).join(' ')
}

export function LogsPage({ announce }: LogsPageProps) {
  const [snapshot, setSnapshot] = useState<LogSnapshot | null>(null)
  const [summary, setSummary] = useState<AccessActivitySummary | null>(null)
  const [activity, setActivity] = useState<AccessActivityPage | null>(null)
  const [users, setUsers] = useState<AllowedUser[]>([])
  const [userId, setUserId] = useState('')
  const [type, setType] = useState('')
  const [outcome, setOutcome] = useState('')
  const [sourceIpAddress, setSourceIpAddress] = useState('')
  const [ipFilterDraft, setIpFilterDraft] = useState('')
  const [range, setRange] = useState<AccessActivityRange>('24h')
  const [page, setPage] = useState(1)
  const [refresh, setRefresh] = useState(0)

  const load = useCallback(() => {
    const controller = new AbortController()
    Promise.all([
      getLogSnapshot(controller.signal),
      getUsers(true, controller.signal),
      getAccessActivitySummary(range, controller.signal),
      getAccessActivity({
        userId: userId || undefined,
        type: type || undefined,
        outcome: outcome || undefined,
        sourceIpAddress: sourceIpAddress.trim() || undefined,
        range,
        page,
      }, controller.signal),
    ]).then(([logs, loadedUsers, loadedSummary, loadedActivity]) => {
      setSnapshot(logs)
      setUsers(loadedUsers)
      setSummary(loadedSummary)
      setActivity(loadedActivity)
    }).catch((error: unknown) => {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        announce(error instanceof Error ? error.message : 'Diagnostics could not be loaded.', true)
      }
    })
    return () => controller.abort()
  }, [announce, outcome, page, range, refresh, sourceIpAddress, type, userId])
  useEffect(() => load(), [load])

  const successes = summary
    ? summary.successfulWebSignIns + summary.successfulAndroidAuthorizations
    : 0
  const failures = summary
    ? summary.failedWebSignIns + summary.failedAndroidAuthorizations
    : 0
  const maxCount = summary
    ? Math.max(1, ...summary.timeline.flatMap((bucket) => [bucket.successCount, bucket.failureCount]))
    : 1
  const tickIndices = summary?.timeline.length
    ? [...new Set([0, Math.floor((summary.timeline.length - 1) / 4), Math.floor((summary.timeline.length - 1) / 2), Math.floor(((summary.timeline.length - 1) * 3) / 4), summary.timeline.length - 1])]
    : []

  function selectRange(value: AccessActivityRange) {
    if (range === value) return
    setSummary(null)
    setActivity(null)
    setRange(value)
    setPage(1)
  }

  function filterByIp(value: string) {
    setIpFilterDraft(value)
    setSourceIpAddress(value)
    setPage(1)
  }

  return <main className="page wide-page logs-page">
    <div className="page-heading">
      <div><p className="eyebrow">Administration</p><h1>Application logs</h1></div>
      <div className="heading-actions">
        <a className="line-button" href="/admin/logs/raw" target="_blank" rel="noreferrer">Open text view</a>
        <a className="line-button" href="/admin/logs/download">Download .txt</a>
        <button className="text-action" type="button" onClick={() => setRefresh((value) => value + 1)}>Refresh</button>
      </div>
    </div>

    <section className="diagnostic-section access-overview" aria-labelledby="access-activity-title">
      <div className="access-overview-heading">
        <div><p className="eyebrow">Security overview</p><h2 id="access-activity-title">Access activity</h2></div>
        <div className="access-overview-controls">
          <div className="range-switcher" aria-label="Activity time range">
            <button type="button" aria-pressed={range === '24h'} onClick={() => selectRange('24h')}>24 hours</button>
            <button type="button" aria-pressed={range === '7d'} onClick={() => selectRange('7d')}>7 days</button>
          </div>
        </div>
      </div>

      {!summary ? <div className="loading-state">Loading access activity…</div> : <>
        <div className="access-summary" aria-label="Authentication activity totals">
          <div className="access-summary-item">
            <span>Successful events</span>
            <strong>{numberFormat.format(successes)}</strong>
            <small>{numberFormat.format(summary.successfulWebSignIns)} web · {numberFormat.format(summary.successfulAndroidAuthorizations)} Android</small>
          </div>
          <div className="access-summary-item">
            <span>Failed attempts</span>
            <strong>{numberFormat.format(failures)}</strong>
            <small>{numberFormat.format(summary.failedWebSignIns)} web · {numberFormat.format(summary.failedAndroidAuthorizations)} Android</small>
          </div>
          <div className="access-summary-item">
            <span>Accounts signed in</span>
            <strong>{numberFormat.format(summary.distinctSuccessfulAccountCount)}</strong>
            <small>Unique accounts with a successful sign-in</small>
          </div>
          <div className="access-summary-item">
            <span>Source IPs seen</span>
            <strong>{numberFormat.format(summary.distinctSourceIpCount)}</strong>
            <small>Across recorded activity</small>
          </div>
        </div>

        <div className="access-insights">
          <section className="access-timeline" aria-labelledby="activity-timeline-title">
            <div className="access-section-heading">
              <h3 id="activity-timeline-title">Sign-in activity</h3>
              <span>Events by {range === '24h' ? 'hour' : 'day'}</span>
            </div>
            {summary.totalEvents === 0 ? <p className="quiet-empty access-chart-empty">No authentication events in this period.</p> : <>
              <svg className="access-chart" viewBox="0 0 700 172" role="img" aria-label={`Authentication events by ${range === '24h' ? 'hour' : 'day'}: ${numberFormat.format(successes)} successful and ${numberFormat.format(failures)} failed over the last ${range === '24h' ? '24 hours' : '7 days'}`}>
                <line className="access-chart-grid" x1="42" x2="690" y1="13" y2="13" />
                <line className="access-chart-grid" x1="42" x2="690" y1="72" y2="72" />
                <line className="access-chart-grid" x1="42" x2="690" y1="132" y2="132" />
                <text className="access-chart-label" x="4" y="17">{maxCount}</text>
                <text className="access-chart-label" x="4" y="76">{Math.floor(maxCount / 2)}</text>
                <text className="access-chart-label" x="18" y="136">0</text>
                <polyline className="access-chart-success" fill="none" points={timelinePoints(summary, 'successCount', maxCount)} />
                <polyline className="access-chart-failure" fill="none" points={timelinePoints(summary, 'failureCount', maxCount)} />
                {tickIndices.map((index) => {
                  const bucket = summary.timeline[index]
                  const x = 42 + (648 * index) / Math.max(1, summary.timeline.length - 1)
                  return <text className="access-chart-label" key={bucket.startUtc} x={x} y="159" textAnchor="middle">{formatBucket(bucket.startUtc, range)}</text>
                })}
              </svg>
              <div className="access-chart-legend"><span><i className="success-key" />Successful events</span><span><i className="failure-key" />Failed attempts</span></div>
            </>}
          </section>

          <section className="access-review" aria-labelledby="review-cues-title">
            <div className="access-section-heading">
              <h3 id="review-cues-title">Worth a look</h3>
              <span>{summary.reviewCues.length} {summary.reviewCues.length === 1 ? 'pattern' : 'patterns'}</span>
            </div>
            {summary.reviewCues.length === 0
              ? <p className="access-no-cues">No source IP reached the repeated-failure review threshold in this period.</p>
              : <div className="review-cue-list">
                {summary.reviewCues.map((cue, index) => <article className="review-cue" key={cue.sourceIpAddress + cue.windowStartUtc + index}>
                  <div className="review-cue-heading"><strong>Repeated failures</strong><span>Review</span></div>
                  <p>{cue.failureCount} attempts from <code>{cue.sourceIpAddress}</code> between {formatDateTime(cue.windowStartUtc)} and {formatDateTime(cue.windowEndUtc)}.</p>
                  {cue.successfulSignInAfterBurst && cue.successUtc && <p className="review-cue-followup">A successful sign-in followed at {formatDateTime(cue.successUtc)}.</p>}
                  <button type="button" className="text-action" onClick={() => filterByIp(cue.sourceIpAddress)}>View activity from this IP</button>
                </article>)}
              </div>}
            <p className="access-review-note">These patterns help focus a review; they are not proof of compromise. Unattributed failures are not linked to an account.</p>
          </section>
        </div>
        <p className="access-footnote">Source IPs are network addresses and do not reliably represent individual visitors. Activity is retained for 7 days.</p>
      </>}
    </section>

    <section className="diagnostic-section" aria-labelledby="recent-activity-title">
      <div className="section-heading"><h2 id="recent-activity-title">Recent activity</h2>{activity && <span>{numberFormat.format(activity.totalCount)} events · latest first</span>}</div>
      <form className="filter-line logs-filter-line" onSubmit={(event) => { event.preventDefault(); filterByIp(ipFilterDraft.trim()) }}>
        <label><span>User</span><select value={userId} onChange={(event) => { setUserId(event.target.value); setPage(1) }}><option value="">All users</option>{users.map((user) => <option value={user.id} key={user.id}>{user.email}</option>)}</select></label>
        <label><span>Event</span><select value={type} onChange={(event) => { setType(event.target.value); setPage(1) }}><option value="">All events</option><option value="WebSignIn">Web sign-in</option><option value="AndroidAuthorization">Android authorization</option></select></label>
        <label><span>Result</span><select value={outcome} onChange={(event) => { setOutcome(event.target.value); setPage(1) }}><option value="">All results</option><option value="Success">Success</option><option value="Failure">Failure</option></select></label>
        <label><span>Source IP</span><input aria-label="Filter by source IP" value={ipFilterDraft} onChange={(event) => setIpFilterDraft(event.target.value)} placeholder="Any source IP" /></label>
        <div className="ip-filter-actions"><button className="text-action" type="submit">Apply IP</button>{sourceIpAddress && <button className="text-action" type="button" onClick={() => filterByIp('')}>Clear</button>}</div>
      </form>
      {!activity ? <div className="loading-state">Loading activity…</div> : activity.items.length === 0 ? <p className="quiet-empty">No access activity matches these filters.</p> : <div className="data-list activity-list">
        <div className="data-row data-header"><span>When</span><span>User</span><span>Event</span><span>Result</span><span>Source</span></div>
        {activity.items.map((item) => <div className="data-row" key={item.id}>
          <time data-label="When">{formatDateTime(item.occurredUtc)}</time>
          <span data-label="User">{item.userEmail ?? 'Unattributed'}</span>
          <span data-label="Event">{eventName(item.type)}</span>
          <span data-label="Result" className={item.outcome === 'Failure' ? 'activity-failure' : 'activity-success'}>{item.failureReason ? item.outcome + ' · ' + failureName(item.failureReason) : item.outcome}</span>
          <span data-label="Source" className="activity-source"><code>{item.sourceIpAddress ?? '—'}</code>{item.userAgent && <small title={item.userAgent}>{item.userAgent}</small>}</span>
        </div>)}
      </div>}
      {activity && <div className="pagination"><span>{numberFormat.format(activity.totalCount)} events in this period</span><div><button type="button" className="text-action" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>Page {page}</span><button type="button" className="text-action" disabled={page * activity.pageSize >= activity.totalCount} onClick={() => setPage((value) => value + 1)}>Next</button></div></div>}
    </section>

    <section className="diagnostic-section">
      <div className="section-heading"><h2>Server log</h2>{snapshot && <span>{snapshot.fileCount} files · {formatDateTime(snapshot.generatedAtUtc)}</span>}</div>
      {snapshot ? <pre className="log-viewer">{snapshot.content}</pre> : <div className="loading-state">Loading log…</div>}
    </section>
  </main>
}
